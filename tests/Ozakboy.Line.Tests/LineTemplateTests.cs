using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Ozakboy.Line.Templates;

namespace Ozakboy.Line.Tests;

/// <summary>
/// 訊息範本的抽變數、渲染與儲存測試。
/// Tests for extracting a template's variables, rendering it, and storing it.
/// </summary>
[TestClass]
public sealed class LineTemplateTests
{
    private const string Greeting = """[{"type":"text","text":"{{name}} 你好,你的編號是 {{code}}。"}]""";

    [TestMethod]
    public void Extract_ReturnsNamesInOrderWithoutDuplicates()
    {
        const string Json = """[{"type":"text","text":"{{b}} {{a}} {{ b }} {{a}}"}]""";

        var names = LineTemplateVariables.Extract(Json);

        CollectionAssert.AreEqual(new List<string> { "b", "a" }, names.ToList(), "去重且保留出現順序。");
    }

    [TestMethod]
    public void Extract_ToleratesSpacesInsideBraces()
    {
        CollectionAssert.AreEqual(
            new List<string> { "name" },
            LineTemplateVariables.Extract("""[{"type":"text","text":"{{  name  }}"}]""").ToList());
    }

    [TestMethod]
    public void Extract_IgnoresNamesThatDoNotLookLikeIdentifiers()
    {
        // 名字若能含空白或標點,同一個變數就會有好幾種寫法,而漏掉其中一種就是把 {{...}} 送到使用者眼前。
        var names = LineTemplateVariables.Extract("""[{"type":"text","text":"{{1a}} {{a-b}} {{ }}"}]""");

        Assert.AreEqual(0, names.Count);
    }

    [TestMethod]
    public void Render_SubstitutesEveryVariable()
    {
        var rendered = LineTemplateRenderer.Render(Greeting, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = "阿明",
            ["code"] = "A-1",
        });

        Assert.IsTrue(rendered.IsSuccess);
        using var document = JsonDocument.Parse(rendered.GetValueOrThrow());
        Assert.AreEqual("阿明 你好,你的編號是 A-1。", document.RootElement[0].GetProperty("text").GetString());
    }

    [TestMethod]
    public void Render_MissingVariables_FailsAndNamesThem()
    {
        var rendered = LineTemplateRenderer.Render(Greeting, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = "阿明",
        });

        Assert.IsTrue(rendered.IsFailure);
        Assert.AreEqual(LineErrorCodes.MissingTemplateVariables, rendered.Error!.Code);
        StringAssert.Contains(rendered.Error.Message, "code", "訊息要列出缺了哪幾個,否則長範本得自己一個一個比對。");
    }

    [TestMethod]
    public void Render_ValueWithQuotesAndNewlines_KeepsTheJsonValid()
    {
        // 一個暱稱裡的引號就足以把整份 JSON 弄壞,而症狀是 LINE 回一個沒指名欄位的 400。
        var rendered = LineTemplateRenderer.Render(
            """[{"type":"text","text":"{{name}} 你好"}]""",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = "他說\"嗨\"\n然後\\離開" });

        Assert.IsTrue(rendered.IsSuccess, rendered.IsFailure ? rendered.Error!.Message : string.Empty);

        using var document = JsonDocument.Parse(rendered.GetValueOrThrow());
        Assert.AreEqual("他說\"嗨\"\n然後\\離開 你好", document.RootElement[0].GetProperty("text").GetString());
    }

    [TestMethod]
    public void Render_ChineseValue_IsNotEscapedToUnicodeSequences()
    {
        var rendered = LineTemplateRenderer.Render(
            """[{"type":"text","text":"{{name}}"}]""",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = "阿明" });

        StringAssert.Contains(rendered.GetValueOrThrow(), "阿明", "中文維持原樣,渲染後的內容仍然讀得懂。");
    }

    [TestMethod]
    public void Render_SixMessages_FailsOnTheCount()
    {
        var six = "[" + string.Join(',', Enumerable.Repeat("""{"type":"text","text":"x"}""", 6)) + "]";

        var rendered = LineTemplateRenderer.Render(six, new Dictionary<string, string>(StringComparer.Ordinal));

        Assert.IsTrue(rendered.IsFailure);
        Assert.AreEqual(LineErrorCodes.TooManyMessages, rendered.Error!.Code);
    }

    [TestMethod]
    public void Validate_NonArray_Fails()
    {
        var validated = LineTemplateRenderer.Validate("""{"type":"text","text":"x"}""");

        Assert.IsTrue(validated.IsFailure);
        Assert.AreEqual(LineErrorCodes.InvalidJson, validated.Error!.Code);
    }

    [TestMethod]
    public void RenderMessages_ReturnsOneRawMessagePerEntry()
    {
        var messages = LineTemplateRenderer.RenderMessages(
            """[{"type":"text","text":"{{a}}"},{"type":"sticker","packageId":"1","stickerId":"2"}]""",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["a"] = "嗨" });

        Assert.IsTrue(messages.IsSuccess);
        Assert.AreEqual(2, messages.GetValueOrThrow().Count);
        Assert.AreEqual("text", messages.GetValueOrThrow()[0].Type);
        Assert.AreEqual("sticker", messages.GetValueOrThrow()[1].Type);
    }

    [TestMethod]
    public void RenderMessages_EntryWithoutType_Fails()
    {
        var messages = LineTemplateRenderer.RenderMessages(
            """[{"text":"沒有 type"}]""",
            new Dictionary<string, string>(StringComparer.Ordinal));

        Assert.IsTrue(messages.IsFailure);
        Assert.AreEqual(LineErrorCodes.InvalidJson, messages.Error!.Code);
    }

    [TestMethod]
    public async Task InMemoryStore_Upsert_GeneratesIdAndStampsTimes()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 15, 8, 0, 0, TimeSpan.Zero));
        var store = new InMemoryLineTemplateStore(clock);

        var created = await store.UpsertAsync(new LineMessageTemplate { Name = "歡迎", MessagesJson = Greeting });

        Assert.AreEqual(32, created.Id.Length, "識別碼是不含連字號的 GUID。");
        Assert.AreEqual(clock.GetUtcNow(), created.CreatedAt);
        Assert.AreEqual(clock.GetUtcNow(), created.UpdatedAt);
    }

    [TestMethod]
    public async Task InMemoryStore_Upsert_KeepsTheOriginalCreatedAt()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 15, 8, 0, 0, TimeSpan.Zero));
        var store = new InMemoryLineTemplateStore(clock);
        var created = await store.UpsertAsync(new LineMessageTemplate { Name = "歡迎", MessagesJson = Greeting });

        clock.Advance(TimeSpan.FromHours(2));
        created.Name = "歡迎(改)";
        var updated = await store.UpsertAsync(created);

        Assert.AreEqual(created.Id, updated.Id);
        Assert.AreEqual(new DateTimeOffset(2026, 9, 15, 8, 0, 0, TimeSpan.Zero), updated.CreatedAt, "建立時間不會被編輯洗掉。");
        Assert.AreEqual(new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.Zero), updated.UpdatedAt);
    }

    [TestMethod]
    public async Task InMemoryStore_HandsOutCopies()
    {
        var store = new InMemoryLineTemplateStore();
        var created = await store.UpsertAsync(new LineMessageTemplate { Name = "歡迎", MessagesJson = Greeting });

        created.Name = "被呼叫端改掉了";
        var reread = await store.GetAsync(created.Id);

        Assert.AreEqual("歡迎", reread!.Name, "呼叫端改手上的物件,不該連帶改到已儲存的資料。");
    }

    [TestMethod]
    public async Task JsonFileStore_RoundTripsAndLeavesNoTemporaryFile()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "templates.json");
            using var store = new JsonFileLineTemplateStore(path);

            var created = await store.UpsertAsync(new LineMessageTemplate { Name = "歡迎", MessagesJson = Greeting });
            var listed = await store.ListAsync();

            Assert.AreEqual(1, listed.Count);
            Assert.AreEqual(created.Id, listed[0].Id);
            Assert.AreEqual(Greeting, listed[0].MessagesJson);
            Assert.IsTrue(File.Exists(path));
            Assert.IsFalse(File.Exists(path + ".tmp"), "原子寫入完成後不留 .tmp 殘檔。");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task JsonFileStore_MissingFile_ReadsAsEmpty()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            using var store = new JsonFileLineTemplateStore(Path.Combine(directory, "nested", "templates.json"));

            Assert.AreEqual(0, (await store.ListAsync()).Count, "檔案還沒有代表還沒存過東西,不是錯誤。");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task JsonFileStore_BrokenFile_Throws()
    {
        // 靜靜回空清單更糟:下一次寫入就會把壞掉的檔案覆蓋掉,原本的資料再也救不回來。
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "templates.json");
            await File.WriteAllTextAsync(path, "{ 這不是陣列");
            using var store = new JsonFileLineTemplateStore(path);

            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => store.ListAsync());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task JsonFileStore_Delete_RemovesOnlyTheNamedTemplate()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            using var store = new JsonFileLineTemplateStore(Path.Combine(directory, "templates.json"));
            var first = await store.UpsertAsync(new LineMessageTemplate { Name = "甲", MessagesJson = Greeting });
            await store.UpsertAsync(new LineMessageTemplate { Name = "乙", MessagesJson = Greeting });

            Assert.IsTrue(await store.DeleteAsync(first.Id));
            Assert.IsFalse(await store.DeleteAsync(first.Id), "刪一個不存在的識別碼回 false,不是失敗。");
            Assert.AreEqual(1, (await store.ListAsync()).Count);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// 建立一個臨時目錄。
    /// Creates a temporary directory.
    /// </summary>
    /// <returns>目錄路徑。The directory path.</returns>
    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ozakboy-line-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
