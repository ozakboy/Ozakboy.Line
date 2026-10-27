using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ozakboy.Line.Liff;
using Ozakboy.Line.Messaging;
using Ozakboy.Line.Tests.TestSupport;

namespace Ozakboy.Line.Tests;

/// <summary>
/// 多 channel 註冊(具名 Messaging 與 LIFF 用戶端、工廠)的測試。全部離線。
/// Tests for multi-channel registration — named Messaging and LIFF clients, and their factories. Entirely offline.
/// </summary>
[TestClass]
public sealed class LineMultiChannelTests
{
    private const string TokenA = "test-channel-access-token-a";
    private const string TokenB = "test-channel-access-token-b";
    private const string TokenDefault = "test-channel-access-token";
    private const string PushResponse = """{"sentMessages":[{"id":"1"}]}""";

    // ---------- Messaging ----------

    [TestMethod]
    public async Task Messaging_TwoNamedChannels_EachSendsItsOwnToken()
    {
        var handlerA = FakeHttpMessageHandler.Json(PushResponse);
        var handlerB = FakeHttpMessageHandler.Json(PushResponse);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLineMessaging("a", o => o.ChannelAccessToken = TokenA);
        services.AddLineMessaging("b", o => o.ChannelAccessToken = TokenB);
        services.AddHttpClient(LineHttpClientNames.ForMessaging("a")).ConfigurePrimaryHttpMessageHandler(() => handlerA);
        services.AddHttpClient(LineHttpClientNames.ForMessaging("b")).ConfigurePrimaryHttpMessageHandler(() => handlerB);

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ILineMessagingClientFactory>();

        Assert.IsTrue((await factory.Get("a").PushTextAsync("U1", "hi")).IsSuccess);
        Assert.IsTrue((await factory.Get("b").PushTextAsync("U1", "hi")).IsSuccess);

        Assert.AreEqual("Bearer " + TokenA, handlerA.Last.Headers["Authorization"]);
        Assert.AreEqual("Bearer " + TokenB, handlerB.Last.Headers["Authorization"]);
        Assert.AreEqual(1, handlerA.Requests.Count);
        Assert.AreEqual(1, handlerB.Requests.Count);
        Assert.AreEqual("a", factory.Get("a").ChannelName);
        Assert.AreEqual("b", factory.Get("b").ChannelName);
    }

    [TestMethod]
    public void Messaging_Factory_GetContainsAndEnumerate()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLineMessaging("a", o => o.ChannelAccessToken = TokenA);
        services.AddLineMessaging("b", o => o.ChannelAccessToken = TokenB);

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ILineMessagingClientFactory>();

        Assert.AreEqual("a,b", Names(factory));
        Assert.IsTrue(factory.Contains("a"));
        Assert.IsFalse(factory.Contains("zzz"));
        Assert.AreSame(factory.Get("a"), factory.Get("a"));
        Assert.AreSame(factory.Get("a"), provider.GetRequiredKeyedService<ILineMessagingClient>("a"));
        Assert.AreNotSame(factory.Get("a"), factory.Get("b"));
    }

    [TestMethod]
    public void Messaging_Factory_UnknownName_ThrowsListingRegisteredNames()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLineMessaging("a", o => o.ChannelAccessToken = TokenA);
        services.AddLineMessaging("b", o => o.ChannelAccessToken = TokenB);

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ILineMessagingClientFactory>();

        var thrown = Assert.Throws<InvalidOperationException>(() => factory.Get("nope"));
        StringAssert.Contains(thrown.Message, "nope");
        StringAssert.Contains(thrown.Message, "\"a\"");
        StringAssert.Contains(thrown.Message, "\"b\"");
        Assert.Throws<ArgumentNullException>(() => factory.Get(null!));
        Assert.Throws<ArgumentNullException>(() => factory.Contains(null!));
    }

    [TestMethod]
    public async Task Messaging_Unnamed_StillResolvesUnkeyed_WithEmptyChannelName()
    {
        var handler = FakeHttpMessageHandler.Json(PushResponse);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLineMessaging(o => o.ChannelAccessToken = TokenDefault);
        services.AddHttpClient(LineHttpClientNames.Messaging).ConfigurePrimaryHttpMessageHandler(() => handler);

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ILineMessagingClient>();
        var factory = provider.GetRequiredService<ILineMessagingClientFactory>();

        Assert.AreEqual(string.Empty, client.ChannelName);
        Assert.AreSame(client, provider.GetRequiredKeyedService<ILineMessagingClient>(string.Empty));
        Assert.AreSame(client, factory.Get(string.Empty));
        Assert.AreEqual("", Names(factory));

        Assert.IsTrue((await client.PushTextAsync("U1", "hi")).IsSuccess);
        Assert.AreEqual("Bearer " + TokenDefault, handler.Last.Headers["Authorization"]);
    }

    [TestMethod]
    public void Messaging_Named_RegistersKeyedOnly()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLineMessaging("a", o => o.ChannelAccessToken = TokenA);
        services.AddLineMessaging(string.Empty, o => o.ChannelAccessToken = TokenDefault);

        using var provider = services.BuildServiceProvider();

        Assert.IsNull(provider.GetService<ILineMessagingClient>());
        Assert.IsNotNull(provider.GetKeyedService<ILineMessagingClient>("a"));
        Assert.IsNotNull(provider.GetKeyedService<ILineMessagingClient>(string.Empty));
    }

    [TestMethod]
    public async Task Messaging_MixedNamedAndUnnamed_KeepTheirOwnTokens()
    {
        var handlerDefault = FakeHttpMessageHandler.Json(PushResponse);
        var handlerA = FakeHttpMessageHandler.Json(PushResponse);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLineMessaging(o => o.ChannelAccessToken = TokenDefault);
        services.AddLineMessaging("a", o => o.ChannelAccessToken = TokenA);
        services.AddHttpClient(LineHttpClientNames.Messaging).ConfigurePrimaryHttpMessageHandler(() => handlerDefault);
        services.AddHttpClient(LineHttpClientNames.ForMessaging("a")).ConfigurePrimaryHttpMessageHandler(() => handlerA);

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ILineMessagingClientFactory>();

        await provider.GetRequiredService<ILineMessagingClient>().PushTextAsync("U1", "hi");
        await factory.Get("a").PushTextAsync("U1", "hi");

        Assert.AreEqual("Bearer " + TokenDefault, handlerDefault.Last.Headers["Authorization"]);
        Assert.AreEqual("Bearer " + TokenA, handlerA.Last.Headers["Authorization"]);
        Assert.AreEqual(",a", Names(factory));
    }

    [TestMethod]
    public async Task Messaging_NamedChannelWithoutToken_FailsNotConfigured_AndSendsNothing()
    {
        var configured = FakeHttpMessageHandler.Json(PushResponse);
        var unconfigured = FakeHttpMessageHandler.Json(PushResponse);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLineMessaging("ok", o => o.ChannelAccessToken = TokenA);
        services.AddLineMessaging("empty", _ => { });
        services.AddHttpClient(LineHttpClientNames.ForMessaging("ok")).ConfigurePrimaryHttpMessageHandler(() => configured);
        services.AddHttpClient(LineHttpClientNames.ForMessaging("empty")).ConfigurePrimaryHttpMessageHandler(() => unconfigured);

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ILineMessagingClientFactory>();

        var result = await factory.Get("empty").PushTextAsync("U1", "hi");

        Assert.AreEqual(LineErrorCodes.NotConfigured, result.Error!.Code);
        Assert.AreEqual(0, unconfigured.Requests.Count);
        Assert.IsTrue((await factory.Get("ok").PushTextAsync("U1", "hi")).IsSuccess);
    }

    [TestMethod]
    public async Task Messaging_DuplicateName_LaterRegistrationReplacesEarlierWholesale()
    {
        var handler = FakeHttpMessageHandler.Json(PushResponse);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLineMessaging("a", o =>
        {
            o.ChannelAccessToken = TokenA;
            o.ChannelSecret = "first-channel-secret";
        });
        services.AddLineMessaging("a", o => o.ChannelAccessToken = TokenB);
        services.AddHttpClient(LineHttpClientNames.ForMessaging("a")).ConfigurePrimaryHttpMessageHandler(() => handler);

        Assert.AreEqual(
            1,
            services.Count(d => d.IsKeyedService && d.ServiceType == typeof(ILineMessagingClient) && Equals(d.ServiceKey, "a")));

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ILineMessagingClientFactory>();

        Assert.AreEqual("a", Names(factory));
        var result = await factory.Get("a").PushTextAsync("U1", "hi");

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("Bearer " + TokenB, handler.Last.Headers["Authorization"]);
        Assert.AreEqual(1, handler.Requests.Count);

        // 前一次註冊的 ChannelSecret 不能殘留(是取代而不是疊加)。
        // The earlier registration's ChannelSecret must not linger — replacement, not stacking.
        var monitor = provider.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<LineMessagingOptions>>();
        Assert.AreEqual(string.Empty, monitor.Get("a").ChannelSecret);
    }

    [TestMethod]
    public void Messaging_FromConfigurationSection_BindsPerChannel()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Line:Shop:ChannelAccessToken"] = TokenA,
                ["Line:Care:ChannelAccessToken"] = TokenB,
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLineMessaging("shop", configuration.GetSection("Line:Shop"));
        services.AddLineMessaging("care", configuration.GetSection("Line:Care"));

        using var provider = services.BuildServiceProvider();
        var monitor = provider.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<LineMessagingOptions>>();

        Assert.AreEqual(TokenA, monitor.Get("shop").ChannelAccessToken);
        Assert.AreEqual(TokenB, monitor.Get("care").ChannelAccessToken);
    }

    [TestMethod]
    public void Messaging_NullArguments_Throw()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddLineMessaging((string)null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => services.AddLineMessaging("a", (Action<LineMessagingOptions>)null!));
        Assert.Throws<ArgumentNullException>(() => services.AddLineMessaging("a", (IConfiguration)null!));
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddLineMessaging("a", _ => { }));
    }

    [TestMethod]
    public void HttpClientNames_ForChannel_EmptyIsTheDefaultName()
    {
        Assert.AreEqual(LineHttpClientNames.Messaging, LineHttpClientNames.ForMessaging(string.Empty));
        Assert.AreEqual("Ozakboy.Line.Messaging:shop", LineHttpClientNames.ForMessaging("shop"));
        Assert.AreEqual(LineHttpClientNames.Liff, LineHttpClientNames.ForLiff(string.Empty));
        Assert.AreEqual("Ozakboy.Line.Liff:shop", LineHttpClientNames.ForLiff("shop"));
        Assert.Throws<ArgumentNullException>(() => LineHttpClientNames.ForMessaging(null!));
        Assert.Throws<ArgumentNullException>(() => LineHttpClientNames.ForLiff(null!));
    }

    // ---------- LIFF ----------

    [TestMethod]
    public async Task Liff_TwoNamedChannels_EachSendsItsOwnToken()
    {
        var handlerA = FakeHttpMessageHandler.Json(Samples.Body("liff-apps"));
        var handlerB = FakeHttpMessageHandler.Json(Samples.Body("liff-apps"));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLineLiff("a", o => o.ChannelAccessToken = TokenA);
        services.AddLineLiff("b", o => o.ChannelAccessToken = TokenB);
        services.AddHttpClient(LineHttpClientNames.ForLiff("a")).ConfigurePrimaryHttpMessageHandler(() => handlerA);
        services.AddHttpClient(LineHttpClientNames.ForLiff("b")).ConfigurePrimaryHttpMessageHandler(() => handlerB);

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ILineLiffClientFactory>();

        Assert.IsTrue((await factory.Get("a").GetAllAsync()).IsSuccess);
        Assert.IsTrue((await factory.Get("b").GetAllAsync()).IsSuccess);

        Assert.AreEqual("Bearer " + TokenA, handlerA.Last.Headers["Authorization"]);
        Assert.AreEqual("Bearer " + TokenB, handlerB.Last.Headers["Authorization"]);
        Assert.AreEqual("a", factory.Get("a").ChannelName);
        Assert.AreEqual("a,b", Names(factory));
        Assert.IsTrue(factory.Contains("b"));
        Assert.IsFalse(factory.Contains("c"));
    }

    [TestMethod]
    public void Liff_Factory_UnknownName_Throws()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLineLiff("a", o => o.ChannelAccessToken = TokenA);

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ILineLiffClientFactory>();

        var thrown = Assert.Throws<InvalidOperationException>(() => factory.Get("nope"));
        StringAssert.Contains(thrown.Message, "nope");
        StringAssert.Contains(thrown.Message, "\"a\"");
    }

    [TestMethod]
    public async Task Liff_Unnamed_StillResolvesUnkeyed_WithEmptyChannelName()
    {
        var handler = FakeHttpMessageHandler.Json(Samples.Body("liff-apps"));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLineLiff(o => o.ChannelAccessToken = TokenDefault);
        services.AddHttpClient(LineHttpClientNames.Liff).ConfigurePrimaryHttpMessageHandler(() => handler);

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ILineLiffClient>();

        Assert.AreEqual(string.Empty, client.ChannelName);
        Assert.AreSame(client, provider.GetRequiredService<ILineLiffClientFactory>().Get(string.Empty));
        Assert.IsTrue((await client.GetAllAsync()).IsSuccess);
        Assert.AreEqual("Bearer " + TokenDefault, handler.Last.Headers["Authorization"]);
    }

    [TestMethod]
    public void Liff_Named_RegistersKeyedOnly()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLineLiff("a", o => o.ChannelAccessToken = TokenA);

        using var provider = services.BuildServiceProvider();

        Assert.IsNull(provider.GetService<ILineLiffClient>());
        Assert.IsNotNull(provider.GetKeyedService<ILineLiffClient>("a"));
    }

    [TestMethod]
    public async Task Liff_NamedChannelWithoutToken_FailsNotConfigured_AndSendsNothing()
    {
        var handler = FakeHttpMessageHandler.Json(Samples.Body("liff-apps"));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLineLiff("empty", _ => { });
        services.AddHttpClient(LineHttpClientNames.ForLiff("empty")).ConfigurePrimaryHttpMessageHandler(() => handler);

        using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<ILineLiffClientFactory>().Get("empty").GetAllAsync();

        Assert.AreEqual(LineErrorCodes.NotConfigured, result.Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task Liff_MixedWithMessaging_UseSeparateClientsAndTokens()
    {
        var liff = FakeHttpMessageHandler.Json(Samples.Body("liff-apps"));
        var messaging = FakeHttpMessageHandler.Json(PushResponse);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLineLiff("shop", o => o.ChannelAccessToken = TokenA);
        services.AddLineMessaging("shop", o => o.ChannelAccessToken = TokenB);
        services.AddHttpClient(LineHttpClientNames.ForLiff("shop")).ConfigurePrimaryHttpMessageHandler(() => liff);
        services.AddHttpClient(LineHttpClientNames.ForMessaging("shop")).ConfigurePrimaryHttpMessageHandler(() => messaging);

        using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<ILineLiffClientFactory>().Get("shop").GetAllAsync();
        await provider.GetRequiredService<ILineMessagingClientFactory>().Get("shop").PushTextAsync("U1", "hi");

        Assert.AreEqual("Bearer " + TokenA, liff.Last.Headers["Authorization"]);
        Assert.AreEqual("Bearer " + TokenB, messaging.Last.Headers["Authorization"]);
    }

    [TestMethod]
    public async Task Liff_DuplicateName_LaterRegistrationReplacesEarlier()
    {
        var handler = FakeHttpMessageHandler.Json(Samples.Body("liff-apps"));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLineLiff("a", o => o.ChannelAccessToken = TokenA);
        services.AddLineLiff("a", o => o.ChannelAccessToken = TokenB);
        services.AddHttpClient(LineHttpClientNames.ForLiff("a")).ConfigurePrimaryHttpMessageHandler(() => handler);

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ILineLiffClientFactory>();

        Assert.AreEqual("a", Names(factory));
        await factory.Get("a").GetAllAsync();

        Assert.AreEqual("Bearer " + TokenB, handler.Last.Headers["Authorization"]);
        Assert.AreEqual(1, handler.Requests.Count);
    }

    [TestMethod]
    public void Liff_NullArguments_Throw()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddLineLiff((string)null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => services.AddLineLiff("a", (Action<LineLiffOptions>)null!));
        Assert.Throws<ArgumentNullException>(() => services.AddLineLiff("a", (IConfiguration)null!));
    }

    /// <summary>
    /// 把工廠的名稱清單用逗號串起來,便於比對順序;空字串名稱寫成空段。
    /// Joins a factory's name list with commas to compare order; the empty-string name becomes an empty segment.
    /// </summary>
    private static string Names(ILineMessagingClientFactory factory) => string.Join(',', factory.ChannelNames);

    private static string Names(ILineLiffClientFactory factory) => string.Join(',', factory.ChannelNames);
}
