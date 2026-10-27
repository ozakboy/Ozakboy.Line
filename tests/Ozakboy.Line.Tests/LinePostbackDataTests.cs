using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Ozakboy.Line.Messaging.Actions;
using Ozakboy.Line.Postback;

namespace Ozakboy.Line.Tests;

/// <summary>
/// postback 超長資料暫存的測試:代碼格式、TTL 與清除、兩個內建儲存體、webhook 還原與便利建構。
/// Tests for long postback data staging: token format, TTL and purging, both built-in stores, webhook
/// resolution, and the convenience factory.
/// </summary>
[TestClass]
public sealed class LinePostbackDataTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 27, 8, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Token_Create_HasContractFormat()
    {
        var token = LinePostbackDataToken.Create();

        Assert.AreEqual(LineMessagingLimits.PostbackDataTokenLength, token.Length);
        Assert.IsTrue(token.StartsWith("ozl:", StringComparison.Ordinal));
        Assert.IsTrue(LinePostbackDataToken.IsToken(token));
        Assert.AreNotEqual(token, LinePostbackDataToken.Create());
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("ozl:")]
    [DataRow("ozl:0123456789abcdef0123456789abcde")]
    [DataRow("ozl:0123456789abcdef0123456789abcdef0")]
    [DataRow("OZL:0123456789abcdef0123456789abcdef")]
    [DataRow("ozl:0123456789ABCDEF0123456789ABCDEF")]
    [DataRow("ozl:0123456789abcdef0123456789abcdeg")]
    [DataRow("xyz:0123456789abcdef0123456789abcdef")]
    [DataRow("action=buy&id=1")]
    public void Token_IsToken_RejectsEverythingElse(string? value)
    {
        Assert.IsFalse(LinePostbackDataToken.IsToken(value));
    }

    [TestMethod]
    public async Task InMemory_StoreThenGet_RoundTripsWithinTheLifetime()
    {
        var clock = new FakeTimeProvider(Start);
        var store = new InMemoryLinePostbackDataStore(clock: clock);

        var stored = await store.StoreAsync(new string('x', 5000));
        Assert.IsTrue(stored.IsSuccess);
        Assert.IsTrue(LinePostbackDataToken.IsToken(stored.GetValueOrThrow()));

        clock.Advance(TimeSpan.FromHours(23));
        var read = await store.GetAsync(stored.GetValueOrThrow());

        Assert.IsTrue(read.IsSuccess);
        Assert.AreEqual(5000, read.GetValueOrThrow().Length);
    }

    [TestMethod]
    public async Task InMemory_DefaultLifetimeIsOneDay_ExpiredAtTheExactInstant()
    {
        var clock = new FakeTimeProvider(Start);
        var store = new InMemoryLinePostbackDataStore(clock: clock);
        var token = (await store.StoreAsync("資料")).GetValueOrThrow();

        clock.Advance(TimeSpan.FromDays(1) - TimeSpan.FromSeconds(1));
        Assert.IsTrue((await store.GetAsync(token)).IsSuccess);

        clock.Advance(TimeSpan.FromSeconds(1));
        var expired = await store.GetAsync(token);

        Assert.IsTrue(expired.IsFailure);
        Assert.AreEqual(LineErrorCodes.PostbackExpired, expired.Error!.Code);
        Assert.AreEqual("line.postback.expired", expired.Error.Code);
    }

    [TestMethod]
    public async Task InMemory_ExplicitLifetime_OverridesTheDefault()
    {
        var clock = new FakeTimeProvider(Start);
        var store = new InMemoryLinePostbackDataStore(clock: clock);
        var token = (await store.StoreAsync("資料", TimeSpan.FromMinutes(5))).GetValueOrThrow();

        clock.Advance(TimeSpan.FromMinutes(5));

        Assert.AreEqual(LineErrorCodes.PostbackExpired, (await store.GetAsync(token)).Error!.Code);
    }

    [TestMethod]
    public async Task InMemory_UnknownToken_IsTheSameFailureAsExpired()
    {
        var store = new InMemoryLinePostbackDataStore();

        var read = await store.GetAsync(LinePostbackDataToken.Create());

        Assert.AreEqual(LineErrorCodes.PostbackExpired, read.Error!.Code);
        Assert.AreEqual(ErrorCategory.NotFound, read.Error.Category);
    }

    [TestMethod]
    public async Task InMemory_DataOverTheLimit_FailsAndStoresNothing()
    {
        var store = new InMemoryLinePostbackDataStore(new LinePostbackDataOptions { MaxDataLength = 100 });

        var atLimit = await store.StoreAsync(new string('a', 100));
        var over = await store.StoreAsync(new string('a', 101));

        Assert.IsTrue(atLimit.IsSuccess);
        Assert.IsTrue(over.IsFailure);
        Assert.AreEqual(LineErrorCodes.PostbackDataTooLong, over.Error!.Code);
        Assert.AreEqual("line.validation.postback_data_too_long", over.Error.Code);
    }

    [TestMethod]
    public async Task InMemory_BadArguments_Throw()
    {
        var store = new InMemoryLinePostbackDataStore();

        await Assert.ThrowsAsync<ArgumentException>(() => store.StoreAsync(" "));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.StoreAsync("x", TimeSpan.Zero));
        await Assert.ThrowsAsync<ArgumentException>(() => store.GetAsync(""));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new InMemoryLinePostbackDataStore(new LinePostbackDataOptions { DefaultTimeToLive = TimeSpan.Zero }));
    }

    [TestMethod]
    public async Task InMemory_Purge_RemovesOnlyExpiredAndCountsThem()
    {
        var clock = new FakeTimeProvider(Start);
        var store = new InMemoryLinePostbackDataStore(clock: clock);
        await store.StoreAsync("短命一", TimeSpan.FromMinutes(1));
        await store.StoreAsync("短命二", TimeSpan.FromMinutes(1));
        var keeper = (await store.StoreAsync("長命", TimeSpan.FromHours(1))).GetValueOrThrow();

        clock.Advance(TimeSpan.FromMinutes(2));
        var first = await store.PurgeExpiredAsync();
        var second = await store.PurgeExpiredAsync();

        Assert.AreEqual(2, first.GetValueOrThrow());
        Assert.AreEqual(0, second.GetValueOrThrow());
        Assert.AreEqual("長命", (await store.GetAsync(keeper)).GetValueOrThrow());
    }

    [TestMethod]
    public async Task JsonFile_SurvivesANewInstance()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "nested", "postbacks.json");
            var clock = new FakeTimeProvider(Start);
            string token;
            using (var writer = new JsonFileLinePostbackDataStore(path, clock: clock))
            {
                token = (await writer.StoreAsync("跨重啟的資料")).GetValueOrThrow();
            }

            using var reader = new JsonFileLinePostbackDataStore(path, clock: clock);
            var read = await reader.GetAsync(token);

            Assert.AreEqual("跨重啟的資料", read.GetValueOrThrow());
            Assert.IsFalse(File.Exists(path + ".tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task JsonFile_ExpiredIsNotFound_ButTheFileIsLeftAloneUntilPurge()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "postbacks.json");
            var clock = new FakeTimeProvider(Start);
            using var store = new JsonFileLinePostbackDataStore(path, clock: clock);
            var token = (await store.StoreAsync("會過期", TimeSpan.FromMinutes(1))).GetValueOrThrow();

            clock.Advance(TimeSpan.FromMinutes(1));
            var read = await store.GetAsync(token);

            Assert.AreEqual(LineErrorCodes.PostbackExpired, read.Error!.Code);
            StringAssert.Contains(await File.ReadAllTextAsync(path), token);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task JsonFile_Purge_ReallyRemovesExpiredEntriesFromTheFile()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "postbacks.json");
            var clock = new FakeTimeProvider(Start);
            using var store = new JsonFileLinePostbackDataStore(path, clock: clock);
            var expiring = (await store.StoreAsync("過期的內容", TimeSpan.FromMinutes(1))).GetValueOrThrow();
            var keeper = (await store.StoreAsync("留下的內容", TimeSpan.FromHours(1))).GetValueOrThrow();

            clock.Advance(TimeSpan.FromMinutes(5));
            var purged = await store.PurgeExpiredAsync();

            Assert.AreEqual(1, purged.GetValueOrThrow());
            var text = await File.ReadAllTextAsync(path);
            using var document = JsonDocument.Parse(text);
            Assert.AreEqual(1, document.RootElement.GetArrayLength());
            Assert.AreEqual(keeper, document.RootElement[0].GetProperty("token").GetString());
            Assert.IsFalse(text.Contains(expiring, StringComparison.Ordinal));
            Assert.AreEqual(0, (await store.PurgeExpiredAsync()).GetValueOrThrow());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task JsonFile_Purge_WithNothingExpired_DoesNotCreateTheFile()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "postbacks.json");
            using var store = new JsonFileLinePostbackDataStore(path);

            var purged = await store.PurgeExpiredAsync();

            Assert.AreEqual(0, purged.GetValueOrThrow());
            Assert.IsFalse(File.Exists(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task JsonFile_DataOverTheLimit_FailsWithoutWriting()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "postbacks.json");
            using var store = new JsonFileLinePostbackDataStore(path, new LinePostbackDataOptions { MaxDataLength = 10 });

            var stored = await store.StoreAsync(new string('z', 11));

            Assert.AreEqual(LineErrorCodes.PostbackDataTooLong, stored.Error!.Code);
            Assert.IsFalse(File.Exists(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task Resolver_PlainData_PassesThroughWithoutTouchingTheStore()
    {
        var store = new ThrowingStore();

        var resolved = await LinePostbackDataResolver.ResolveAsync(new LineWebhookPostback { Data = "action=buy&id=7" }, store);

        Assert.AreEqual("action=buy&id=7", resolved.GetValueOrThrow());
    }

    [TestMethod]
    public async Task Resolver_Token_ReturnsTheStoredData()
    {
        var store = new InMemoryLinePostbackDataStore();
        var token = (await store.StoreAsync("很長的原資料")).GetValueOrThrow();

        var resolved = await LinePostbackDataResolver.ResolveAsync(new LineWebhookPostback { Data = token }, store);

        Assert.AreEqual("很長的原資料", resolved.GetValueOrThrow());
    }

    [TestMethod]
    public async Task Resolver_ExpiredOrUnknownToken_FailsWithPostbackExpired()
    {
        var clock = new FakeTimeProvider(Start);
        var store = new InMemoryLinePostbackDataStore(clock: clock);
        var token = (await store.StoreAsync("資料", TimeSpan.FromSeconds(30))).GetValueOrThrow();
        clock.Advance(TimeSpan.FromMinutes(1));

        var expired = await LinePostbackDataResolver.ResolveAsync(new LineWebhookPostback { Data = token }, store);
        var unknown = await LinePostbackDataResolver.ResolveAsync(
            new LineWebhookPostback { Data = LinePostbackDataToken.Create() }, store);

        Assert.AreEqual(LineErrorCodes.PostbackExpired, expired.Error!.Code);
        Assert.AreEqual(LineErrorCodes.PostbackExpired, unknown.Error!.Code);
    }

    [TestMethod]
    public async Task CreateStored_LongData_YieldsAValidActionCarryingTheToken()
    {
        var store = new InMemoryLinePostbackDataStore();
        var longData = new string('d', 2000);

        var created = await PostbackAction.CreateStoredAsync(store, "查看", longData, "查看詳情");

        var action = created.GetValueOrThrow();
        Assert.IsTrue(LinePostbackDataToken.IsToken(action.Data));
        Assert.AreEqual("查看", action.Label);
        Assert.AreEqual("查看詳情", action.DisplayText);
        Assert.IsTrue(action.Data.Length <= LineMessagingLimits.MaxPostbackDataLength, "代碼遠短於 300 字上限。");
        Assert.AreEqual(longData, (await store.GetAsync(action.Data)).GetValueOrThrow());
    }

    [TestMethod]
    public async Task CreateStored_ShortData_IsStoredToo()
    {
        var store = new InMemoryLinePostbackDataStore();

        var created = await PostbackAction.CreateStoredAsync(store, "短", "a=1");

        var action = created.GetValueOrThrow();
        Assert.AreNotEqual("a=1", action.Data, "不超過 300 字也照存,行為一致。");
        Assert.AreEqual("a=1", (await store.GetAsync(action.Data)).GetValueOrThrow());
        Assert.IsNull(action.DisplayText);
    }

    [TestMethod]
    public async Task CreateStored_HonoursTheLifetimeAndPropagatesStoreFailures()
    {
        var clock = new FakeTimeProvider(Start);
        var store = new InMemoryLinePostbackDataStore(new LinePostbackDataOptions { MaxDataLength = 50 }, clock);

        var created = await PostbackAction.CreateStoredAsync(store, "短命", "x", timeToLive: TimeSpan.FromSeconds(10));
        var tooLong = await PostbackAction.CreateStoredAsync(store, "太長", new string('x', 51));
        clock.Advance(TimeSpan.FromSeconds(10));

        Assert.AreEqual(LineErrorCodes.PostbackExpired, (await store.GetAsync(created.GetValueOrThrow().Data)).Error!.Code);
        Assert.AreEqual(LineErrorCodes.PostbackDataTooLong, tooLong.Error!.Code);
    }

    [TestMethod]
    public void AddLinePostbackData_DefaultsToASingletonInMemoryStore()
    {
        using var provider = new ServiceCollection().AddLinePostbackData().BuildServiceProvider();

        var first = provider.GetRequiredService<ILinePostbackDataStore>();

        Assert.IsInstanceOfType<InMemoryLinePostbackDataStore>(first);
        Assert.AreSame(first, provider.GetRequiredService<ILinePostbackDataStore>());
    }

    [TestMethod]
    public async Task AddLinePostbackData_UseJsonFile_RegistersTheFileStoreWithTheConfiguredLifetime()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "postbacks.json");
            var clock = new FakeTimeProvider(Start);
            var services = new ServiceCollection();
            services.AddSingleton<TimeProvider>(clock);
            services.AddLinePostbackData(options =>
            {
                options.DefaultTimeToLive = TimeSpan.FromMinutes(10);
                options.UseJsonFile(path);
            });

            await using var provider = services.BuildServiceProvider();
            var store = provider.GetRequiredService<ILinePostbackDataStore>();
            var token = (await store.StoreAsync("資料")).GetValueOrThrow();
            clock.Advance(TimeSpan.FromMinutes(10));

            Assert.IsInstanceOfType<JsonFileLinePostbackDataStore>(store);
            Assert.AreSame(store, provider.GetRequiredService<ILinePostbackDataStore>());
            Assert.AreEqual(LineErrorCodes.PostbackExpired, (await store.GetAsync(token)).Error!.Code);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ozakboy-line-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// 任何呼叫都擲例外的儲存體,用來證明某條路徑沒有碰儲存體。
    /// A store that throws on any call, proving a path never touched the store.
    /// </summary>
    private sealed class ThrowingStore : ILinePostbackDataStore
    {
        public Task<Result<string>> StoreAsync(string data, TimeSpan? timeToLive = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("不該被呼叫。");

        public Task<Result<string>> GetAsync(string token, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("不該被呼叫。");

        public Task<Result<int>> PurgeExpiredAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("不該被呼叫。");
    }
}
