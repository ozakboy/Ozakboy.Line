#pragma warning disable CA1861 // 測試裡的常數陣列只用一次,抽成欄位反而難讀。Constant arrays in tests are used once; hoisting them into fields only hurts readability.
using System.Text.Json;
using Microsoft.Extensions.Options;
using Ozakboy.Line.Liff;
using Ozakboy.Line.Mcp.Tests.TestSupport;
using Ozakboy.Line.Messaging;
using Ozakboy.Line.Messaging.Insight;
using Ozakboy.Line.Tests.TestSupport;

namespace Ozakboy.Line.Mcp.Tests;

/// <summary>
/// 0.3.0 新增的 MCP 工具測試:webhook 查詢與測試、投遞統計、互動統計、LIFF,以及送出工具的統計單位。
/// Tests for the MCP tools added in 0.3.0: webhook info and test, delivery statistics, engagement insight, LIFF,
/// and the aggregation unit on the sending tools.
/// </summary>
[TestClass]
public sealed class LineMcpStatsLiffAndUnitToolsTests
{
    // ---- contract ----

    [TestMethod]
    public void NewToolNames_AreTheContract()
    {
        // 工具名稱發佈後不改:外部 AI 的提示詞與流程綁在上面。
        var names = typeof(LineInfoTools).Assembly
            .GetTypes()
            .Where(type => type.GetCustomAttributes(typeof(ModelContextProtocol.Server.McpServerToolTypeAttribute), inherit: false).Length > 0)
            .SelectMany(type => type.GetMethods())
            .Select(method => method.GetCustomAttributes(typeof(ModelContextProtocol.Server.McpServerToolAttribute), inherit: false)
                .Cast<ModelContextProtocol.Server.McpServerToolAttribute>()
                .FirstOrDefault()?.Name)
            .Where(name => name is not null)
            .ToHashSet();

        foreach (var expected in new[]
        {
            "line_get_webhook_info",
            "line_test_webhook",
            "line_get_delivery_stats",
            "line_get_message_event_insight",
            "line_list_liff_apps",
            "line_create_liff_app",
            "line_delete_liff_app",
        })
        {
            Assert.IsTrue(names.Contains(expected), expected);
        }
    }

    // ---- webhook ----

    [TestMethod]
    public async Task GetWebhookInfo_ReturnsEndpointAndActive()
    {
        var (tools, messaging) = CreateInfoTools();
        messaging.WebhookEndpointResult = Result.Success(new LineWebhookEndpointInfo { Endpoint = "https://example.com/hook", Active = true });

        var json = Parse(await tools.GetWebhookInfoAsync());

        Assert.IsTrue(json.GetProperty("ok").GetBoolean());
        Assert.AreEqual("https://example.com/hook", json.GetProperty("webhook").GetProperty("endpoint").GetString());
        Assert.IsTrue(json.GetProperty("webhook").GetProperty("active").GetBoolean());
    }

    [TestMethod]
    public async Task GetWebhookInfo_Failure_ComesBackAsJsonWithCode()
    {
        var (tools, messaging) = CreateInfoTools();
        messaging.WebhookEndpointResult = Result.Failure<LineWebhookEndpointInfo>(Error.Validation(LineErrorCodes.ApiError, "LINE 拒絕了。"));

        var json = Parse(await tools.GetWebhookInfoAsync());

        Assert.IsFalse(json.GetProperty("ok").GetBoolean());
        Assert.AreEqual(LineErrorCodes.ApiError, json.GetProperty("code").GetString());
    }

    [TestMethod]
    public async Task TestWebhook_WithoutEndpoint_TestsTheConfiguredOne()
    {
        var (tools, messaging) = CreateInfoTools();
        messaging.WebhookTestResultValue = Result.Success(new LineWebhookTestResult { Success = true, StatusCode = 200, Reason = "OK" });

        var json = Parse(await tools.TestWebhookAsync());

        Assert.IsTrue(json.GetProperty("ok").GetBoolean());
        Assert.IsTrue(json.GetProperty("webhook").GetProperty("success").GetBoolean());
        Assert.AreEqual(1, messaging.WebhookTests.Count);
        Assert.IsNull(messaging.WebhookTests[0], "省略 endpoint 時要傳 null,讓 LINE 測目前設定的位址。");
    }

    [TestMethod]
    public async Task TestWebhook_BlankEndpoint_IsTreatedAsOmitted()
    {
        var (tools, messaging) = CreateInfoTools();
        messaging.WebhookTestResultValue = Result.Success(new LineWebhookTestResult { Success = true });

        await tools.TestWebhookAsync("  ");

        Assert.IsNull(messaging.WebhookTests[0]);
    }

    [TestMethod]
    public async Task TestWebhook_WithEndpoint_PassesItThrough()
    {
        var (tools, messaging) = CreateInfoTools();
        messaging.WebhookTestResultValue = Result.Success(new LineWebhookTestResult { Success = true });

        await tools.TestWebhookAsync("https://example.com/other");

        Assert.AreEqual("https://example.com/other", messaging.WebhookTests[0]);
    }

    [TestMethod]
    public async Task TestWebhook_RemoteAnswersNon2xx_StillOkTrueWithSuccessFalse()
    {
        // 對方伺服器沒回 2xx 時,呼叫本身仍然成功;AI 要看 webhook.success 才知道驗證沒過。
        var (tools, messaging) = CreateInfoTools();
        messaging.WebhookTestResultValue = Result.Success(new LineWebhookTestResult { Success = false, StatusCode = 500, Reason = "Internal Server Error" });

        var json = Parse(await tools.TestWebhookAsync());

        Assert.IsTrue(json.GetProperty("ok").GetBoolean());
        Assert.IsFalse(json.GetProperty("webhook").GetProperty("success").GetBoolean());
        Assert.AreEqual(500, json.GetProperty("webhook").GetProperty("statusCode").GetInt32());
    }

    // ---- delivery stats ----

    [TestMethod]
    public async Task GetDeliveryStats_ReturnsAllFourTypes_IncludingUnready()
    {
        var (tools, messaging) = CreateInfoTools();
        messaging.DeliveryCountResults[LineMessageDeliveryType.Reply] = Result.Success(new LineMessageDeliveryCount { Status = LineMessageDeliveryCountStatus.Ready, Success = 12 });
        messaging.DeliveryCountResults[LineMessageDeliveryType.Push] = Result.Success(new LineMessageDeliveryCount { Status = LineMessageDeliveryCountStatus.Ready, Success = 3 });
        messaging.DeliveryCountResults[LineMessageDeliveryType.Multicast] = Result.Success(new LineMessageDeliveryCount { Status = LineMessageDeliveryCountStatus.Unready });
        messaging.DeliveryCountResults[LineMessageDeliveryType.Broadcast] = Result.Success(new LineMessageDeliveryCount { Status = LineMessageDeliveryCountStatus.Ready, Success = 0 });

        var json = Parse(await tools.GetDeliveryStatsAsync("2026-10-26"));

        Assert.IsTrue(json.GetProperty("ok").GetBoolean());
        Assert.AreEqual("2026-10-26", json.GetProperty("date").GetString());

        var deliveries = json.GetProperty("deliveries");
        Assert.AreEqual(4, deliveries.GetArrayLength());
        CollectionAssert.AreEqual(
            new[] { "reply", "push", "multicast", "broadcast" },
            deliveries.EnumerateArray().Select(entry => entry.GetProperty("type").GetString()).ToArray());
        Assert.AreEqual(12, deliveries[0].GetProperty("success").GetInt64());
        Assert.AreEqual("ready", deliveries[0].GetProperty("status").GetString());

        Assert.AreEqual("unready", deliveries[2].GetProperty("status").GetString(), "統計未備妥要如實回報。");
        Assert.AreEqual(JsonValueKind.Null, deliveries[2].GetProperty("success").ValueKind, "unready 的 success 是 null,不是 0。");
        Assert.AreEqual(0, deliveries[3].GetProperty("success").GetInt64(), "真的是 0 時要回 0。");

        Assert.IsTrue(messaging.DeliveryCountCalls.All(call => call.Date == new DateOnly(2026, 10, 26)));
    }

    [TestMethod]
    public async Task GetDeliveryStats_PartialFailure_ReportsPerType()
    {
        var (tools, messaging) = CreateInfoTools();
        var ready = Result.Success(new LineMessageDeliveryCount { Status = LineMessageDeliveryCountStatus.Ready, Success = 1 });
        messaging.DeliveryCountResults[LineMessageDeliveryType.Reply] = ready;
        messaging.DeliveryCountResults[LineMessageDeliveryType.Push] = Result.Failure<LineMessageDeliveryCount>(Error.Validation(LineErrorCodes.ApiError, "壞了"));
        messaging.DeliveryCountResults[LineMessageDeliveryType.Multicast] = ready;
        messaging.DeliveryCountResults[LineMessageDeliveryType.Broadcast] = ready;

        var json = Parse(await tools.GetDeliveryStatsAsync("2026-10-26"));

        Assert.IsTrue(json.GetProperty("ok").GetBoolean());
        var push = json.GetProperty("deliveries")[1];
        Assert.IsFalse(push.GetProperty("ok").GetBoolean());
        Assert.AreEqual(LineErrorCodes.ApiError, push.GetProperty("code").GetString());
    }

    [TestMethod]
    public async Task GetDeliveryStats_AllFail_ReturnsTheFirstError()
    {
        var (tools, messaging) = CreateInfoTools();
        var failure = Result.Failure<LineMessageDeliveryCount>(Error.Validation(LineErrorCodes.NotConfigured, "未設定"));
        foreach (var type in Enum.GetValues<LineMessageDeliveryType>())
        {
            messaging.DeliveryCountResults[type] = failure;
        }

        var json = Parse(await tools.GetDeliveryStatsAsync("2026-10-26"));

        Assert.IsFalse(json.GetProperty("ok").GetBoolean());
        Assert.AreEqual(LineErrorCodes.NotConfigured, json.GetProperty("code").GetString());
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("2026/10/26")]
    [DataRow("20261026")]
    [DataRow("2026-13-01")]
    [DataRow("昨天")]
    public async Task GetDeliveryStats_BadDate_IsRefusedWithoutCallingLine(string date)
    {
        var (tools, messaging) = CreateInfoTools();

        var json = Parse(await tools.GetDeliveryStatsAsync(date));

        Assert.IsFalse(json.GetProperty("ok").GetBoolean());
        Assert.AreEqual(0, messaging.DeliveryCountCalls.Count);
    }

    // ---- event insight ----

    [TestMethod]
    public async Task GetMessageEventInsight_ReturnsOverview()
    {
        var (tools, messaging) = CreateInfoTools();
        messaging.EventInsightResult = Result.Success(new LineMessageEventInsight
        {
            Overview = new LineMessageEventOverview { RequestId = "req-1", Delivered = 100 },
        });

        var json = Parse(await tools.GetMessageEventInsightAsync(" req-1 "));

        Assert.IsTrue(json.GetProperty("ok").GetBoolean());
        Assert.AreEqual(100, json.GetProperty("insight").GetProperty("overview").GetProperty("delivered").GetInt64());
        CollectionAssert.AreEqual(new[] { "req-1" }, messaging.EventInsightRequests, "識別碼前後空白要修掉。");
    }

    [TestMethod]
    public async Task GetMessageEventInsight_BlankRequestId_IsRefused()
    {
        var (tools, messaging) = CreateInfoTools();

        var json = Parse(await tools.GetMessageEventInsightAsync(" "));

        Assert.IsFalse(json.GetProperty("ok").GetBoolean());
        Assert.AreEqual(0, messaging.EventInsightRequests.Count);
    }

    [TestMethod]
    public async Task StatsTools_DoNotTouchTheOutbox()
    {
        var messaging = new FakeLineMessagingClient
        {
            WebhookEndpointResult = Result.Success(new LineWebhookEndpointInfo { Endpoint = "https://example.com", Active = true }),
            EventInsightResult = Result.Success(new LineMessageEventInsight()),
        };
        var outbox = new InMemoryLineMcpOutboxStore();
        var tools = new LineInfoTools(messaging, outbox, Options.Create(new LineMcpOptions()));

        await tools.GetWebhookInfoAsync();
        await tools.GetMessageEventInsightAsync("req");

        Assert.AreEqual(0, (await outbox.ListAsync()).Count, "唯讀工具不寫待發佇列、不計每日額度。");
    }

    // ---- aggregation unit on the sending tools ----

    [TestMethod]
    public async Task SendPush_ReviewMode_StoresUnitWithTheItemAndSendsNothing()
    {
        var (tools, messaging, outbox) = CreateSendTools();

        var json = Parse(await tools.SendPushAsync("U1", "嗨", customAggregationUnit: "autumn_sale"));

        Assert.IsTrue(json.GetProperty("ok").GetBoolean());
        Assert.AreEqual(0, messaging.Pushes.Count);

        var item = (await outbox.ListAsync()).Single();
        using var payload = JsonDocument.Parse(item.PayloadJson);
        Assert.AreEqual("autumn_sale", payload.RootElement.GetProperty("customAggregationUnit").GetString());
        StringAssert.Contains(item.Summary, "autumn_sale", "審核的人要看得到統計單位。");
    }

    [TestMethod]
    public async Task SendPush_DirectMode_PassesUnitInOptions()
    {
        var (tools, messaging, _) = CreateSendTools(LineMcpSendMode.Direct);

        await tools.SendPushAsync("U1", "嗨", customAggregationUnit: "autumn_sale");

        CollectionAssert.AreEqual(new[] { "autumn_sale" }, messaging.SentOptions.Single()!.CustomAggregationUnits!.ToArray());
    }

    [TestMethod]
    public async Task SendPush_WithoutUnit_SendsNoOptions()
    {
        var (tools, messaging, _) = CreateSendTools(LineMcpSendMode.Direct);

        await tools.SendPushAsync("U1", "嗨");

        Assert.IsNull(messaging.SentOptions.Single(), "沒給統計單位就不帶選項,行為與 0.2.0 相同。");
    }

    [TestMethod]
    public async Task SendMulticastAndBroadcast_DirectMode_PassUnitInOptions()
    {
        var (tools, messaging, _) = CreateSendTools(LineMcpSendMode.Direct);

        await tools.SendMulticastAsync(["U1", "U2"], "嗨", customAggregationUnit: "unit_a");
        await tools.SendBroadcastAsync("嗨", customAggregationUnit: "unit_b");

        Assert.AreEqual(2, messaging.SentOptions.Count);
        Assert.AreEqual("unit_a", messaging.SentOptions[0]!.CustomAggregationUnits!.Single());
        Assert.AreEqual("unit_b", messaging.SentOptions[1]!.CustomAggregationUnits!.Single());
    }

    [TestMethod]
    [DataRow("has space")]
    [DataRow("中文單位")]
    [DataRow("dash-ed")]
    [DataRow("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task SendTools_BadUnit_AreRefusedAndNothingIsQueued(string unit)
    {
        var (tools, messaging, outbox) = CreateSendTools();

        var push = Parse(await tools.SendPushAsync("U1", "嗨", customAggregationUnit: unit));
        var broadcast = Parse(await tools.SendBroadcastAsync("嗨", customAggregationUnit: unit));

        Assert.IsFalse(push.GetProperty("ok").GetBoolean());
        Assert.IsFalse(broadcast.GetProperty("ok").GetBoolean());
        Assert.AreEqual(0, (await outbox.ListAsync()).Count, "註定被核心擋下的項目不該排進待審佇列。");
        Assert.AreEqual(0, messaging.Pushes.Count);
    }

    [TestMethod]
    public async Task Approval_OfItemWithUnit_SendsWithTheUnit()
    {
        var messaging = new FakeLineMessagingClient();
        var outbox = new InMemoryLineMcpOutboxStore();
        var options = Options.Create(new LineMcpOptions());
        var tools = new LineSendTools(messaging, outbox, options);
        var service = new LineMcpOutboxService(outbox, messaging, new StubHttpClientFactory([1], "image/png"));

        await tools.SendPushAsync("U1", "嗨", customAggregationUnit: "autumn_sale");
        var queued = (await outbox.ListAsync()).Single();
        var approved = await service.ApproveAndSendAsync(queued.Id);

        Assert.IsTrue(approved.IsSuccess);
        Assert.AreEqual(1, messaging.Pushes.Count);
        Assert.AreEqual("autumn_sale", messaging.SentOptions.Single()!.CustomAggregationUnits!.Single());
    }

    [TestMethod]
    public async Task Approval_OfLegacyItemWithoutUnit_StillSends()
    {
        // 0.2.0 寫下的待發項目沒有 customAggregationUnit 欄位,核准時當成不帶統計單位。
        var messaging = new FakeLineMessagingClient();
        var outbox = new InMemoryLineMcpOutboxStore();
        var service = new LineMcpOutboxService(outbox, messaging, new StubHttpClientFactory([1], "image/png"));
        var legacy = await outbox.AddAsync(new LineMcpOutboxItem
        {
            Kind = LineMcpOutboxKind.Push,
            PayloadJson = """{"to":"U1","messages":[{"type":"text","text":"舊的"}]}""",
            Summary = "舊的",
            Status = LineMcpOutboxStatus.PendingReview,
            CreatedAt = DateTimeOffset.UtcNow,
        });

        var approved = await service.ApproveAndSendAsync(legacy.Id);

        Assert.IsTrue(approved.IsSuccess);
        Assert.IsNull(messaging.SentOptions.Single());
    }

    private static (LineInfoTools Tools, FakeLineMessagingClient Messaging) CreateInfoTools()
    {
        var messaging = new FakeLineMessagingClient();

        return (new LineInfoTools(messaging, new InMemoryLineMcpOutboxStore(), Options.Create(new LineMcpOptions())), messaging);
    }

    private static (LineSendTools Tools, FakeLineMessagingClient Messaging, ILineMcpOutboxStore Outbox) CreateSendTools(
        LineMcpSendMode mode = LineMcpSendMode.Review)
    {
        var messaging = new FakeLineMessagingClient();
        var outbox = new InMemoryLineMcpOutboxStore();

        return (new LineSendTools(messaging, outbox, Options.Create(new LineMcpOptions { SendMode = mode })), messaging, outbox);
    }

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
