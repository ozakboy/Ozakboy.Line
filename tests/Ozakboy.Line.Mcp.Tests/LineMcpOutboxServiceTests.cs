using System.Net;
using System.Text.Json;
using Ozakboy.Line.Mcp.Tests.TestSupport;
using Ozakboy.Line.Tests.TestSupport;

namespace Ozakboy.Line.Mcp.Tests;

/// <summary>
/// 待發項目的核准、退回與取消測試。
/// Tests for approving, rejecting, and cancelling an outbox item.
/// </summary>
[TestClass]
public sealed class LineMcpOutboxServiceTests
{
    private const string Messages = """[{"type":"text","text":"嗨"}]""";

    [TestMethod]
    public async Task ApproveAndSend_Push_CallsPushAndMarksSent()
    {
        var (service, messaging, outbox, _) = CreateService();
        var item = await outbox.AddAsync(Item(LineMcpOutboxKind.Push, $$"""{"to":"U1","messages":{{Messages}}}"""));

        var result = await service.ApproveAndSendAsync(item.Id);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(LineMcpOutboxStatus.Sent, result.GetValueOrThrow().Status);
        Assert.AreEqual(1, messaging.Pushes.Count);
        Assert.AreEqual("U1", messaging.Pushes[0].To);
    }

    [TestMethod]
    public async Task ApproveAndSend_Multicast_CallsMulticast()
    {
        var (service, messaging, outbox, _) = CreateService();
        var item = await outbox.AddAsync(Item(LineMcpOutboxKind.Multicast, $$"""{"userIds":["U1","U2"],"messages":{{Messages}}}"""));

        await service.ApproveAndSendAsync(item.Id);

        Assert.AreEqual(1, messaging.Multicasts.Count);
        Assert.AreEqual(2, messaging.Multicasts[0].To.Count);
    }

    [TestMethod]
    public async Task ApproveAndSend_Broadcast_CallsBroadcast()
    {
        var (service, messaging, outbox, _) = CreateService();
        var item = await outbox.AddAsync(Item(LineMcpOutboxKind.Broadcast, $$"""{"messages":{{Messages}}}"""));

        await service.ApproveAndSendAsync(item.Id);

        Assert.AreEqual(1, messaging.Broadcasts.Count);
    }

    [TestMethod]
    public async Task ApproveAndSend_RichMenuReplace_FetchesTheImageThenReplaces()
    {
        var (service, messaging, outbox, fetcher) = CreateService();
        var payload = $$"""
        {"menu":{{Menu}},"imageUrl":"https://example.com/menu.png","setAsDefault":true,"aliasId":"main"}
        """;
        var item = await outbox.AddAsync(Item(LineMcpOutboxKind.RichMenuReplace, payload));

        var result = await service.ApproveAndSendAsync(item.Id);

        Assert.AreEqual(LineMcpOutboxStatus.Sent, result.GetValueOrThrow().Status, result.GetValueOrThrow().Error);
        Assert.AreEqual(1, fetcher.Requests.Count, "圖片在核准的當下才抓,不是排入佇列時。");
        Assert.AreEqual(1, messaging.Replacements.Count);
        Assert.AreEqual("image/png", messaging.Replacements[0].ContentType);
        Assert.IsTrue(messaging.Replacements[0].Options!.SetAsDefault);
        Assert.AreEqual("main", messaging.Replacements[0].Options!.AliasId);
        Assert.AreEqual(1, messaging.Replacements[0].Menu.Areas.Count, "區塊必須讀得出來,少了區塊的選單是一張按不動的圖。");
    }

    [TestMethod]
    public async Task ApproveAndSend_ImageTooLarge_FailsWithoutReplacing()
    {
        var (service, messaging, outbox, _) = CreateService(imageBytes: new byte[(1024 * 1024) + 1]);
        var payload = $$"""{"menu":{{Menu}},"imageUrl":"https://example.com/menu.png"}""";
        var item = await outbox.AddAsync(Item(LineMcpOutboxKind.RichMenuReplace, payload));

        var result = await service.ApproveAndSendAsync(item.Id);

        Assert.AreEqual(LineMcpOutboxStatus.Failed, result.GetValueOrThrow().Status);
        Assert.AreEqual(0, messaging.Replacements.Count);
    }

    [TestMethod]
    public async Task ApproveAndSend_ImageWrongContentType_Fails()
    {
        var (service, messaging, outbox, _) = CreateService(contentType: "text/html");
        var payload = $$"""{"menu":{{Menu}},"imageUrl":"https://example.com/menu.png"}""";
        var item = await outbox.AddAsync(Item(LineMcpOutboxKind.RichMenuReplace, payload));

        var result = await service.ApproveAndSendAsync(item.Id);

        Assert.AreEqual(LineMcpOutboxStatus.Failed, result.GetValueOrThrow().Status);
        Assert.AreEqual(0, messaging.Replacements.Count);
    }

    [TestMethod]
    public async Task ApproveAndSend_NonHttpImageUrl_FailsWithoutFetching()
    {
        // 位址由外部 AI 提供,擋掉 file:// 這類本機協定是這條路徑最基本的一道檢查。
        var (service, _, outbox, fetcher) = CreateService();
        var payload = $$"""{"menu":{{Menu}},"imageUrl":"file:///etc/hosts"}""";
        var item = await outbox.AddAsync(Item(LineMcpOutboxKind.RichMenuReplace, payload));

        var result = await service.ApproveAndSendAsync(item.Id);

        Assert.AreEqual(LineMcpOutboxStatus.Failed, result.GetValueOrThrow().Status);
        Assert.AreEqual(0, fetcher.Requests.Count);
    }

    [TestMethod]
    public async Task ApproveAndSend_LineRefuses_RecordsTheError()
    {
        var (service, messaging, outbox, _) = CreateService();
        messaging.SendFailure = Error.Validation(LineErrorCodes.ApiError, "LINE 拒絕了。");
        var item = await outbox.AddAsync(Item(LineMcpOutboxKind.Broadcast, $$"""{"messages":{{Messages}}}"""));

        var result = await service.ApproveAndSendAsync(item.Id);

        Assert.AreEqual(LineMcpOutboxStatus.Failed, result.GetValueOrThrow().Status);
        StringAssert.Contains(result.GetValueOrThrow().Error, "LINE 拒絕了。");
        Assert.IsNull(result.GetValueOrThrow().SentAt);
    }

    [TestMethod]
    public async Task ApproveAndSend_AlreadySent_IsRefused()
    {
        // 留在待審的話,下一個看到它的人不知道它已經被試過一次,而重按核准可能是重複發送。
        var (service, _, outbox, _) = CreateService();
        var item = await outbox.AddAsync(Item(LineMcpOutboxKind.Broadcast, $$"""{"messages":{{Messages}}}"""));
        await service.ApproveAndSendAsync(item.Id);

        var again = await service.ApproveAndSendAsync(item.Id);

        Assert.IsTrue(again.IsFailure);
        Assert.AreEqual(LineErrorCodes.McpOutboxInvalidStatus, again.Error!.Code);
    }

    [TestMethod]
    public async Task ApproveAndSend_UnknownId_IsNotFound()
    {
        var (service, _, _, _) = CreateService();

        var result = await service.ApproveAndSendAsync("nope");

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.McpOutboxNotFound, result.Error!.Code);
        Assert.AreEqual(ErrorCategory.NotFound, result.Error.Category);
    }

    [TestMethod]
    public async Task Reject_MarksRejectedWithTheReason()
    {
        var (service, messaging, outbox, _) = CreateService();
        var item = await outbox.AddAsync(Item(LineMcpOutboxKind.Broadcast, $$"""{"messages":{{Messages}}}"""));

        var result = await service.RejectAsync(item.Id, "文案還要改");

        Assert.AreEqual(LineMcpOutboxStatus.Rejected, result.GetValueOrThrow().Status);
        Assert.AreEqual("文案還要改", result.GetValueOrThrow().Error);
        Assert.AreEqual(0, messaging.Broadcasts.Count, "退回不會送出任何訊息。");
    }

    [TestMethod]
    public async Task Cancel_OnlyWorksWhilePending()
    {
        var (service, _, outbox, _) = CreateService();
        var item = await outbox.AddAsync(Item(LineMcpOutboxKind.Broadcast, $$"""{"messages":{{Messages}}}"""));

        Assert.AreEqual(LineMcpOutboxStatus.Canceled, (await service.CancelAsync(item.Id)).GetValueOrThrow().Status);

        var again = await service.CancelAsync(item.Id);
        Assert.IsTrue(again.IsFailure, "已送出的訊息收不回來,接受一個做不到的請求再回一個含糊的成功更糟。");
        Assert.AreEqual(LineErrorCodes.McpOutboxInvalidStatus, again.Error!.Code);
    }

    /// <summary>
    /// 測試用的最小圖文選單定義。
    /// The minimal rich menu definition used by the tests.
    /// </summary>
    private const string Menu = """
    {"size":{"width":2500,"height":1686},"selected":false,"name":"主選單","chatBarText":"開啟選單","areas":[{"bounds":{"x":0,"y":0,"width":2500,"height":1686},"action":{"type":"message","text":"嗨"}}]}
    """;

    /// <summary>
    /// 建立一筆待審項目。
    /// Builds a pending item.
    /// </summary>
    /// <param name="kind">項目種類。The item's kind.</param>
    /// <param name="payloadJson">內容。The payload.</param>
    /// <returns>項目。The item.</returns>
    private static LineMcpOutboxItem Item(LineMcpOutboxKind kind, string payloadJson) => new()
    {
        Kind = kind,
        PayloadJson = payloadJson,
        Summary = "測試項目",
        Status = LineMcpOutboxStatus.PendingReview,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    /// <summary>
    /// 建立待發項目服務與它的假相依。
    /// Creates the outbox service and its fakes.
    /// </summary>
    /// <param name="imageBytes">抓圖要回的內容。What the image fetch answers with.</param>
    /// <param name="contentType">抓圖要回的型別。The type the image fetch answers with.</param>
    /// <returns>服務、假用戶端、佇列與抓圖工廠。The service, the fake client, the outbox, and the fetch factory.</returns>
    private static (ILineMcpOutboxService Service, FakeLineMessagingClient Messaging, ILineMcpOutboxStore Outbox, StubHttpClientFactory Fetcher) CreateService(
        byte[]? imageBytes = null,
        string contentType = "image/png")
    {
        var messaging = new FakeLineMessagingClient();
        var outbox = new InMemoryLineMcpOutboxStore();
        var fetcher = new StubHttpClientFactory(imageBytes ?? [1, 2, 3], contentType, HttpStatusCode.OK);

        return (new LineMcpOutboxService(outbox, messaging, fetcher), messaging, outbox, fetcher);
    }
}
