#pragma warning disable CA1861 // 測試裡的常數陣列只用一次。Constant arrays in tests are used once.
using System.Text.Json;
using Microsoft.Extensions.Options;
using Ozakboy.Line.Liff;
using Ozakboy.Line.Mcp.Tests.TestSupport;
using Ozakboy.Line.Tests.TestSupport;

namespace Ozakboy.Line.Mcp.Tests;

/// <summary>
/// LIFF 工具的測試:查詢直接查;建立與刪除與 <c>line_replace_rich_menu</c> 一樣走待審佇列。
/// Tests for the LIFF tools: listing queries directly, while creating and deleting go through the review queue
/// like <c>line_replace_rich_menu</c>.
/// </summary>
/// <remarks>
/// 這一組守的線和送出工具相同:<b>AI 不得單獨改動線上設定</b>。刪掉一個 LIFF 應用程式,
/// 所有指向它的連結都會立刻失效。
/// These guard the same line as the sending tools: <b>the AI cannot change live configuration alone</b>.
/// Deleting a LIFF app breaks every link that points at it at once.
/// </remarks>
[TestClass]
public sealed class LineMcpLiffToolsTests
{
    [TestMethod]
    public async Task LiffTools_WithoutClient_SayNotConfigured()
    {
        var (tools, _, outbox) = CreateTools(liff: null);

        foreach (var json in new[]
        {
            Parse(await tools.ListLiffAppsAsync()),
            Parse(await tools.CreateLiffAppAsync("full", "https://example.com", "測試")),
            Parse(await tools.DeleteLiffAppAsync("1234567890-AbCdEfGh")),
        })
        {
            Assert.IsFalse(json.GetProperty("ok").GetBoolean());
            Assert.AreEqual(LineErrorCodes.NotConfigured, json.GetProperty("code").GetString());
            StringAssert.Contains(json.GetProperty("error").GetString(), "AddLineLiff");
        }

        Assert.AreEqual(0, (await outbox.ListAsync()).Count);
    }

    [TestMethod]
    public async Task ListLiffApps_QueriesDirectlyEvenInReviewMode()
    {
        var liff = new FakeLineLiffClient();
        liff.Apps.Add(new LineLiffApp
        {
            LiffId = "111-aaa",
            View = new LineLiffView { Type = LineLiffViewType.Tall, Url = "https://example.com/a" },
            Description = "預約",
        });
        var (tools, _, outbox) = CreateTools(liff);

        var json = Parse(await tools.ListLiffAppsAsync());

        Assert.IsTrue(json.GetProperty("ok").GetBoolean());
        Assert.AreEqual(1, json.GetProperty("count").GetInt32());
        var app = json.GetProperty("apps")[0];
        Assert.AreEqual("111-aaa", app.GetProperty("liffId").GetString());
        Assert.AreEqual("tall", app.GetProperty("view").GetProperty("type").GetString());
        Assert.AreEqual(0, (await outbox.ListAsync()).Count, "查詢唯讀,不進佇列、不計每日額度。");
    }

    [TestMethod]
    public async Task ListLiffApps_None_IsAnEmptyListNotAnError()
    {
        var (tools, _, _) = CreateTools(new FakeLineLiffClient());

        var json = Parse(await tools.ListLiffAppsAsync());

        Assert.IsTrue(json.GetProperty("ok").GetBoolean());
        Assert.AreEqual(0, json.GetProperty("count").GetInt32());
    }

    // ---- review mode ----

    [TestMethod]
    public async Task CreateLiffApp_ReviewMode_QueuesAndCreatesNothing()
    {
        var liff = new FakeLineLiffClient();
        var (tools, _, outbox) = CreateTools(liff);

        var json = Parse(await tools.CreateLiffAppAsync(" Tall ", " https://example.com/page ", " 預約頁 ", "Normal"));

        Assert.IsTrue(json.GetProperty("ok").GetBoolean());
        Assert.AreEqual("review", json.GetProperty("mode").GetString());
        StringAssert.Contains(json.GetProperty("notice").GetString(), "待審");
        Assert.AreEqual(0, liff.Added.Count, "待審模式下一個請求都不送到 LINE。");

        var item = (await outbox.ListAsync()).Single();
        Assert.AreEqual(LineMcpOutboxKind.LiffCreate, item.Kind);
        Assert.AreEqual(LineMcpOutboxStatus.PendingReview, item.Status);
        StringAssert.Contains(item.Summary, "https://example.com/page");
        using var payload = JsonDocument.Parse(item.PayloadJson);
        Assert.AreEqual("tall", payload.RootElement.GetProperty("viewType").GetString());
        Assert.AreEqual("https://example.com/page", payload.RootElement.GetProperty("url").GetString());
        Assert.AreEqual("預約頁", payload.RootElement.GetProperty("description").GetString());
        Assert.AreEqual("normal", payload.RootElement.GetProperty("botPrompt").GetString());
    }

    [TestMethod]
    public async Task DeleteLiffApp_ReviewMode_QueuesAndDeletesNothing()
    {
        var liff = new FakeLineLiffClient();
        var (tools, _, outbox) = CreateTools(liff);

        var json = Parse(await tools.DeleteLiffAppAsync(" 111-aaa "));

        Assert.IsTrue(json.GetProperty("ok").GetBoolean());
        Assert.AreEqual("review", json.GetProperty("mode").GetString());
        Assert.AreEqual(0, liff.Deleted.Count, "AI 在待審模式下不能直接刪掉線上的 LIFF 應用程式。");

        var item = (await outbox.ListAsync()).Single();
        Assert.AreEqual(LineMcpOutboxKind.LiffDelete, item.Kind);
        Assert.AreEqual(LineMcpOutboxStatus.PendingReview, item.Status);
        using var payload = JsonDocument.Parse(item.PayloadJson);
        Assert.AreEqual("111-aaa", payload.RootElement.GetProperty("liffId").GetString());
    }

    [TestMethod]
    [DataRow("huge", "https://example.com", "x", null)]
    [DataRow("", "https://example.com", "x", null)]
    [DataRow("full", "", "x", null)]
    [DataRow("full", "https://example.com", "  ", null)]
    [DataRow("full", "https://example.com", "x", "sometimes")]
    public async Task CreateLiffApp_BadArguments_AreRefusedAndNothingIsQueued(string viewType, string url, string description, string? botPrompt)
    {
        var liff = new FakeLineLiffClient();
        var (tools, _, outbox) = CreateTools(liff);

        var json = Parse(await tools.CreateLiffAppAsync(viewType, url, description, botPrompt));

        Assert.IsFalse(json.GetProperty("ok").GetBoolean());
        Assert.AreEqual(0, liff.Added.Count);
        Assert.AreEqual(0, (await outbox.ListAsync()).Count, "註定失敗的計畫不該排進待審佇列。");
    }

    [TestMethod]
    public async Task DeleteLiffApp_BlankId_IsRefused()
    {
        var (tools, _, outbox) = CreateTools(new FakeLineLiffClient());

        Assert.IsFalse(Parse(await tools.DeleteLiffAppAsync(" ")).GetProperty("ok").GetBoolean());
        Assert.AreEqual(0, (await outbox.ListAsync()).Count);
    }

    [TestMethod]
    public async Task LiffChanges_CountAgainstTheDailyCap()
    {
        var (tools, _, outbox) = CreateTools(new FakeLineLiffClient());

        for (var index = 0; index < 10; index++)
        {
            await tools.DeleteLiffAppAsync($"{index}-aaa");
        }

        Assert.AreEqual(10, (await outbox.ListAsync(LineMcpOutboxStatus.PendingReview, limit: 100)).Count);
        Assert.IsFalse(Parse(await tools.CreateLiffAppAsync("full", "https://example.com", "x")).GetProperty("ok").GetBoolean());
    }

    // ---- direct mode ----

    [TestMethod]
    public async Task CreateLiffApp_DirectMode_CreatesThenRecordsAsSent()
    {
        var liff = new FakeLineLiffClient { AddedLiffId = "999-zzz" };
        var (tools, _, outbox) = CreateTools(liff, LineMcpSendMode.Direct);

        var json = Parse(await tools.CreateLiffAppAsync(" Tall ", " https://example.com/page ", " 預約頁 ", "Normal"));

        Assert.IsTrue(json.GetProperty("ok").GetBoolean());
        Assert.AreEqual("direct", json.GetProperty("mode").GetString());
        Assert.AreEqual("999-zzz", json.GetProperty("liffId").GetString());
        Assert.AreEqual("https://liff.line.me/999-zzz", json.GetProperty("liffUrl").GetString());

        var sent = liff.Added.Single();
        Assert.AreEqual(LineLiffViewType.Tall, sent.View!.Type);
        Assert.AreEqual("https://example.com/page", sent.View.Url);
        Assert.AreEqual("預約頁", sent.Description);
        Assert.AreEqual("normal", sent.BotPrompt);
        Assert.IsNull(sent.LiffId, "建立請求絕不帶 liffId。");

        var item = (await outbox.ListAsync()).Single();
        Assert.AreEqual(LineMcpOutboxStatus.Sent, item.Status, "直接模式一樣留紀錄。");
        Assert.IsNotNull(item.SentAt);
    }

    [TestMethod]
    public async Task CreateLiffApp_DirectModeButLineRefuses_RecordsAsFailedWithCode()
    {
        var liff = new FakeLineLiffClient { Failure = Error.Validation(LineErrorCodes.InvalidLiffApp, "網址必須是 https") };
        var (tools, _, outbox) = CreateTools(liff, LineMcpSendMode.Direct);

        var json = Parse(await tools.CreateLiffAppAsync("full", "http://example.com", "x"));

        Assert.IsFalse(json.GetProperty("ok").GetBoolean());
        Assert.AreEqual(LineErrorCodes.InvalidLiffApp, json.GetProperty("code").GetString());

        var item = (await outbox.ListAsync()).Single();
        Assert.AreEqual(LineMcpOutboxStatus.Failed, item.Status);
        Assert.IsNull(item.SentAt, "沒成功就不該有完成時間 —— 先執行再記錄。");
    }

    [TestMethod]
    public async Task DeleteLiffApp_DirectMode_DeletesThenRecordsAsSent()
    {
        var liff = new FakeLineLiffClient();
        var (tools, _, outbox) = CreateTools(liff, LineMcpSendMode.Direct);

        var json = Parse(await tools.DeleteLiffAppAsync(" 111-aaa "));

        Assert.IsTrue(json.GetProperty("ok").GetBoolean());
        Assert.AreEqual("direct", json.GetProperty("mode").GetString());
        CollectionAssert.AreEqual(new[] { "111-aaa" }, liff.Deleted);
        Assert.AreEqual(LineMcpOutboxStatus.Sent, (await outbox.ListAsync()).Single().Status);
    }

    // ---- approval ----

    [TestMethod]
    public async Task Approve_LiffCreate_CreatesTheAppAndMarksSent()
    {
        var liff = new FakeLineLiffClient { AddedLiffId = "999-zzz" };
        var (tools, service, outbox) = CreateToolsAndService(liff);
        await tools.CreateLiffAppAsync("compact", "https://example.com/page", "預約頁", "aggressive");
        var queued = (await outbox.ListAsync()).Single();

        var approved = await service.ApproveAndSendAsync(queued.Id);

        Assert.IsTrue(approved.IsSuccess);
        Assert.AreEqual(LineMcpOutboxStatus.Sent, approved.GetValueOrThrow().Status);
        var sent = liff.Added.Single();
        Assert.AreEqual(LineLiffViewType.Compact, sent.View!.Type);
        Assert.AreEqual("https://example.com/page", sent.View.Url);
        Assert.AreEqual("預約頁", sent.Description);
        Assert.AreEqual("aggressive", sent.BotPrompt);
    }

    [TestMethod]
    public async Task Approve_LiffDelete_DeletesTheApp()
    {
        var liff = new FakeLineLiffClient();
        var (tools, service, outbox) = CreateToolsAndService(liff);
        await tools.DeleteLiffAppAsync("111-aaa");
        var queued = (await outbox.ListAsync()).Single();

        var approved = await service.ApproveAndSendAsync(queued.Id);

        Assert.AreEqual(LineMcpOutboxStatus.Sent, approved.GetValueOrThrow().Status);
        CollectionAssert.AreEqual(new[] { "111-aaa" }, liff.Deleted);
    }

    [TestMethod]
    public async Task Approve_LiffItemWhenClientIsNotRegistered_MarksFailedWithNotConfigured()
    {
        var outbox = new InMemoryLineMcpOutboxStore();
        var service = new LineMcpOutboxService(outbox, new FakeLineMessagingClient(), new StubHttpClientFactory([1]), liff: null);
        var item = await outbox.AddAsync(new LineMcpOutboxItem
        {
            Kind = LineMcpOutboxKind.LiffDelete,
            PayloadJson = """{"liffId":"111-aaa"}""",
            Summary = "刪除 LIFF 應用程式 111-aaa",
            Status = LineMcpOutboxStatus.PendingReview,
            CreatedAt = DateTimeOffset.UtcNow,
        });

        var approved = await service.ApproveAndSendAsync(item.Id);

        Assert.IsTrue(approved.IsSuccess, "核准動作本身成功,送出的失敗記在項目上。");
        var result = approved.GetValueOrThrow();
        Assert.AreEqual(LineMcpOutboxStatus.Failed, result.Status);
        StringAssert.Contains(result.Error, "AddLineLiff");
    }

    [TestMethod]
    public async Task Approve_LiffCreateRefusedByLine_MarksFailed()
    {
        var liff = new FakeLineLiffClient { Failure = Error.Validation(LineErrorCodes.ApiError, "LINE 拒絕了") };
        var (tools, service, outbox) = CreateToolsAndService(liff);
        await tools.CreateLiffAppAsync("full", "https://example.com", "x");
        var queued = (await outbox.ListAsync()).Single();

        var approved = await service.ApproveAndSendAsync(queued.Id);

        Assert.AreEqual(LineMcpOutboxStatus.Failed, approved.GetValueOrThrow().Status);
        Assert.IsNull(approved.GetValueOrThrow().SentAt);
    }

    [TestMethod]
    public async Task Approve_ApprovedLiffItem_CannotBeApprovedTwice()
    {
        var liff = new FakeLineLiffClient();
        var (tools, service, outbox) = CreateToolsAndService(liff);
        await tools.DeleteLiffAppAsync("111-aaa");
        var queued = (await outbox.ListAsync()).Single();
        await service.ApproveAndSendAsync(queued.Id);

        var again = await service.ApproveAndSendAsync(queued.Id);

        Assert.IsTrue(again.IsFailure, "狀態只往前走:已執行的項目不得再次核准。");
        Assert.AreEqual(1, liff.Deleted.Count);
    }

    [TestMethod]
    public async Task JsonFileStore_ReadsA020FileAndRoundTripsLiffItems()
    {
        var path = Path.Combine(Path.GetTempPath(), $"line-mcp-outbox-{Guid.NewGuid():N}.json");
        try
        {
            using var store = new JsonFileLineMcpOutboxStore(path);
            var legacy = await store.AddAsync(new LineMcpOutboxItem
            {
                Kind = LineMcpOutboxKind.Push,
                PayloadJson = """{"to":"U1","messages":[{"type":"text","text":"舊的"}]}""",
                Summary = "舊的",
                Status = LineMcpOutboxStatus.PendingReview,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            var liffItem = await store.AddAsync(new LineMcpOutboxItem
            {
                Kind = LineMcpOutboxKind.LiffDelete,
                PayloadJson = """{"liffId":"111-aaa"}""",
                Summary = "刪除",
                Status = LineMcpOutboxStatus.PendingReview,
                CreatedAt = DateTimeOffset.UtcNow,
            });

            Assert.AreEqual(LineMcpOutboxKind.Push, (await store.GetAsync(legacy.Id))!.Kind);
            Assert.AreEqual(LineMcpOutboxKind.LiffDelete, (await store.GetAsync(liffItem.Id))!.Kind);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static (LineLiffTools Tools, FakeLineLiffClient? Liff, ILineMcpOutboxStore Outbox) CreateTools(
        FakeLineLiffClient? liff,
        LineMcpSendMode mode = LineMcpSendMode.Review)
    {
        var outbox = new InMemoryLineMcpOutboxStore();
        var tools = new LineLiffTools(outbox, Options.Create(new LineMcpOptions { SendMode = mode }), liff);

        return (tools, liff, outbox);
    }

    private static (LineLiffTools Tools, LineMcpOutboxService Service, ILineMcpOutboxStore Outbox) CreateToolsAndService(FakeLineLiffClient liff)
    {
        var (tools, _, outbox) = CreateTools(liff);
        var service = new LineMcpOutboxService(outbox, new FakeLineMessagingClient(), new StubHttpClientFactory([1]), liff: liff);

        return (tools, service, outbox);
    }

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
