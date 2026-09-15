using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Ozakboy.Line.Mcp.Tests.TestSupport;
using Ozakboy.Line.Tests.TestSupport;

namespace Ozakboy.Line.Mcp.Tests;

/// <summary>
/// 待發佇列查詢工具與圖文選單工具的測試。
/// Tests for the outbox tools and the rich menu tools.
/// </summary>
[TestClass]
public sealed class LineMcpOutboxAndRichMenuToolsTests
{
    private const string Menu = """
    {"size":{"width":2500,"height":1686},"selected":false,"name":"主選單","chatBarText":"開啟選單","areas":[{"bounds":{"x":0,"y":0,"width":2500,"height":1686},"action":{"type":"message","text":"嗨"}}]}
    """;

    [TestMethod]
    public async Task ListOutbox_FiltersByStatus()
    {
        var (outboxTools, _, outbox, _) = CreateOutboxTools();
        await outbox.AddAsync(Item(LineMcpOutboxStatus.PendingReview));
        await outbox.AddAsync(Item(LineMcpOutboxStatus.Sent));

        var pending = Parse(await outboxTools.ListOutboxAsync("PendingReview"));

        Assert.AreEqual(1, pending.GetProperty("count").GetInt32());
        Assert.AreEqual("PendingReview", pending.GetProperty("items")[0].GetProperty("status").GetString());
    }

    [TestMethod]
    public async Task ListOutbox_UnknownStatus_ListsTheValidOnes()
    {
        var (outboxTools, _, _, _) = CreateOutboxTools();

        var json = Parse(await outboxTools.ListOutboxAsync("Pending"));

        Assert.IsFalse(json.GetProperty("ok").GetBoolean());
        StringAssert.Contains(json.GetProperty("error").GetString(), "PendingReview");
    }

    [TestMethod]
    public async Task ListOutbox_OmitsThePayload()
    {
        // 二十筆訊息陣列的原文會把回應撐得很大,而列表的用途是「看一眼有哪些」。
        var (outboxTools, _, outbox, _) = CreateOutboxTools();
        await outbox.AddAsync(Item(LineMcpOutboxStatus.PendingReview));

        var json = Parse(await outboxTools.ListOutboxAsync());

        Assert.IsFalse(json.GetProperty("items")[0].TryGetProperty("payloadJson", out _));
    }

    [TestMethod]
    public async Task GetOutbox_IncludesThePayload()
    {
        var (outboxTools, _, outbox, _) = CreateOutboxTools();
        var item = await outbox.AddAsync(Item(LineMcpOutboxStatus.PendingReview));

        var json = Parse(await outboxTools.GetOutboxAsync(item.Id));

        Assert.IsTrue(json.GetProperty("ok").GetBoolean());
        StringAssert.Contains(json.GetProperty("payloadJson").GetString(), "messages");
    }

    [TestMethod]
    public async Task CancelOutbox_OnlyWorksWhilePending()
    {
        var (outboxTools, _, outbox, _) = CreateOutboxTools();
        var sent = await outbox.AddAsync(Item(LineMcpOutboxStatus.Sent));

        var json = Parse(await outboxTools.CancelOutboxAsync(sent.Id));

        Assert.IsFalse(json.GetProperty("ok").GetBoolean());
        Assert.AreEqual(LineErrorCodes.McpOutboxInvalidStatus, json.GetProperty("code").GetString());
    }

    [TestMethod]
    public async Task CancelOutbox_Pending_Succeeds()
    {
        var (outboxTools, _, outbox, _) = CreateOutboxTools();
        var pending = await outbox.AddAsync(Item(LineMcpOutboxStatus.PendingReview));

        var json = Parse(await outboxTools.CancelOutboxAsync(pending.Id));

        Assert.IsTrue(json.GetProperty("ok").GetBoolean());
        Assert.AreEqual("Canceled", json.GetProperty("item").GetProperty("status").GetString());
    }

    [TestMethod]
    public async Task ReplaceRichMenu_ReviewMode_QueuesWithoutTouchingLine()
    {
        var (_, richMenuTools, outbox, messaging) = CreateOutboxTools();

        var json = Parse(await richMenuTools.ReplaceRichMenuAsync(Menu, "https://example.com/menu.png", setAsDefault: true));

        Assert.AreEqual("review", json.GetProperty("mode").GetString());
        Assert.AreEqual(0, messaging.Replacements.Count);

        var items = await outbox.ListAsync();
        Assert.AreEqual(LineMcpOutboxKind.RichMenuReplace, items[0].Kind);
        StringAssert.Contains(items[0].Summary, "主選單");
    }

    [TestMethod]
    public async Task ReplaceRichMenu_DirectMode_Replaces()
    {
        var (_, richMenuTools, outbox, messaging) = CreateOutboxTools(LineMcpSendMode.Direct);

        var json = Parse(await richMenuTools.ReplaceRichMenuAsync(Menu, "https://example.com/menu.png"));

        Assert.AreEqual("direct", json.GetProperty("mode").GetString());
        Assert.AreEqual(1, messaging.Replacements.Count);
        Assert.AreEqual(LineMcpOutboxStatus.Sent, (await outbox.ListAsync())[0].Status);
    }

    [TestMethod]
    public async Task ReplaceRichMenu_ChangesDisallowed_IsRefused()
    {
        var (_, richMenuTools, outbox, _) = CreateOutboxTools(allowRichMenuChanges: false);

        var json = Parse(await richMenuTools.ReplaceRichMenuAsync(Menu, "https://example.com/menu.png"));

        Assert.IsFalse(json.GetProperty("ok").GetBoolean());
        Assert.AreEqual(0, (await outbox.ListAsync()).Count);
    }

    [TestMethod]
    public async Task ReplaceRichMenu_MenuWithoutAreas_IsRefused()
    {
        // 沒有區塊的選單 LINE 照收不誤,而使用者看到的是一張完全按不動的圖。
        var (_, richMenuTools, outbox, _) = CreateOutboxTools();
        const string NoAreas = """{"size":{"width":2500,"height":1686},"name":"主選單","chatBarText":"開啟選單"}""";

        var json = Parse(await richMenuTools.ReplaceRichMenuAsync(NoAreas, "https://example.com/menu.png"));

        Assert.IsFalse(json.GetProperty("ok").GetBoolean());
        Assert.AreEqual(0, (await outbox.ListAsync()).Count);
    }

    [TestMethod]
    public async Task ReplaceRichMenu_BlankImageUrl_IsRefused()
    {
        var (_, richMenuTools, _, _) = CreateOutboxTools();

        Assert.IsFalse(Parse(await richMenuTools.ReplaceRichMenuAsync(Menu, " ")).GetProperty("ok").GetBoolean());
    }

    [TestMethod]
    public async Task ReplaceRichMenu_PayloadKeepsTheOriginalMenuJson()
    {
        // 本套件沒建模的欄位(LINE 之後新增的東西)不該在這一趟往返裡被洗掉。
        var (_, richMenuTools, outbox, _) = CreateOutboxTools();
        var menuWithExtra = Menu.Replace("\"selected\":false", "\"selected\":false,\"futureField\":\"x\"", StringComparison.Ordinal);

        await richMenuTools.ReplaceRichMenuAsync(menuWithExtra, "https://example.com/menu.png");

        var items = await outbox.ListAsync();
        using var payload = JsonDocument.Parse(items[0].PayloadJson);
        Assert.AreEqual("x", payload.RootElement.GetProperty("menu").GetProperty("futureField").GetString());
    }

    [TestMethod]
    public async Task ValidateRichMenu_BrokenJson_IsRefusedWithoutCallingLine()
    {
        var (_, richMenuTools, _, _) = CreateOutboxTools();

        var json = Parse(await richMenuTools.ValidateRichMenuAsync("{ 不是 JSON"));

        Assert.IsFalse(json.GetProperty("ok").GetBoolean());
    }

    [TestMethod]
    public async Task ValidateRichMenu_ValidMenu_ReportsValid()
    {
        var (_, richMenuTools, _, _) = CreateOutboxTools();

        var json = Parse(await richMenuTools.ValidateRichMenuAsync(Menu));

        Assert.IsTrue(json.GetProperty("ok").GetBoolean());
        Assert.IsTrue(json.GetProperty("valid").GetBoolean());
    }

    [TestMethod]
    public async Task ListRichMenuAliases_ReturnsTheList()
    {
        var (_, richMenuTools, _, _) = CreateOutboxTools();

        var json = Parse(await richMenuTools.ListRichMenuAliasesAsync());

        Assert.IsTrue(json.GetProperty("ok").GetBoolean());
        Assert.AreEqual(0, json.GetProperty("count").GetInt32());
    }

    /// <summary>
    /// 建立一筆待發項目。
    /// Builds an outbox item.
    /// </summary>
    /// <param name="status">狀態。The status.</param>
    /// <returns>項目。The item.</returns>
    private static LineMcpOutboxItem Item(LineMcpOutboxStatus status) => new()
    {
        Kind = LineMcpOutboxKind.Broadcast,
        PayloadJson = """{"messages":[{"type":"text","text":"嗨"}]}""",
        Summary = "測試項目",
        Status = status,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    /// <summary>
    /// 解析工具回傳的 JSON。
    /// Parses a tool's JSON result.
    /// </summary>
    /// <param name="json">JSON 字串。The JSON string.</param>
    /// <returns>根元素。The root element.</returns>
    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    /// <summary>
    /// 建立佇列工具、選單工具與它們的假相依。
    /// Creates the outbox tools, the rich menu tools, and their fakes.
    /// </summary>
    /// <param name="mode">送出模式。The send mode.</param>
    /// <param name="allowRichMenuChanges">是否允許變更選單。Whether menu changes are allowed.</param>
    /// <returns>兩組工具、佇列與假用戶端。Both tool sets, the outbox, and the fake client.</returns>
    private static (LineOutboxTools Outbox, LineRichMenuTools RichMenu, ILineMcpOutboxStore Store, FakeLineMessagingClient Messaging) CreateOutboxTools(
        LineMcpSendMode mode = LineMcpSendMode.Review,
        bool allowRichMenuChanges = true)
    {
        var messaging = new FakeLineMessagingClient();
        var store = new InMemoryLineMcpOutboxStore();
        var fetcher = new StubHttpClientFactory([1, 2, 3], "image/png", HttpStatusCode.OK);
        var service = new LineMcpOutboxService(store, messaging, fetcher);
        var options = Options.Create(new LineMcpOptions { SendMode = mode, AllowRichMenuChanges = allowRichMenuChanges });

        return (
            new LineOutboxTools(store, service),
            new LineRichMenuTools(messaging, store, service, options),
            store,
            messaging);
    }
}
