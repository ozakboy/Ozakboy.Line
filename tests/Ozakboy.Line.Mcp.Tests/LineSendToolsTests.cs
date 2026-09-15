using System.Text.Json;
using Microsoft.Extensions.Options;
using Ozakboy.Line.Tests.TestSupport;

namespace Ozakboy.Line.Mcp.Tests;

/// <summary>
/// 送出類工具的測試:待審模式一則訊息都不送、直接模式送了也留紀錄、每日上限擋得住。
/// Tests for the sending tools: review mode sends nothing, direct mode sends and still records, and the daily cap
/// holds.
/// </summary>
/// <remarks>
/// 這一組測試守的是本套件最重要的一條線:<b>AI 不得直接發送</b>。這條線一旦破了,
/// 一個讀到奇怪文字的 AI 就能對全體好友廣播,而且沒有任何人有機會看一眼。
/// These tests guard the package's most important line: <b>the AI cannot send</b>. Once it goes, an AI that read
/// something odd can broadcast to every friend of the account with nobody given a chance to look.
/// </remarks>
[TestClass]
public sealed class LineSendToolsTests
{
    [TestMethod]
    public async Task SendPush_ReviewMode_QueuesWithoutCallingLine()
    {
        var (tools, messaging, outbox) = CreateTools();

        var json = Parse(await tools.SendPushAsync("U1", "嗨"));

        Assert.IsTrue(json.GetProperty("ok").GetBoolean());
        Assert.AreEqual("review", json.GetProperty("mode").GetString());
        Assert.AreEqual(0, messaging.Pushes.Count, "待審模式下一個請求都不送到 LINE。");

        var items = await outbox.ListAsync();
        Assert.AreEqual(1, items.Count);
        Assert.AreEqual(LineMcpOutboxStatus.PendingReview, items[0].Status);
        Assert.AreEqual(LineMcpOutboxKind.Push, items[0].Kind);
        Assert.AreEqual("嗨", items[0].Summary);
    }

    [TestMethod]
    public async Task SendPush_ReviewMode_ReturnsTheNoticeSoTheAiTellsTheTruth()
    {
        // 少了這一句,AI 很容易把 ok:true 讀成「已經發出去了」,然後這樣告訴使用者。
        var (tools, _, _) = CreateTools();

        var json = Parse(await tools.SendPushAsync("U1", "嗨"));

        StringAssert.Contains(json.GetProperty("notice").GetString(), "待審");
    }

    [TestMethod]
    public async Task SendPush_DirectMode_SendsAndRecordsAsSent()
    {
        var (tools, messaging, outbox) = CreateTools(LineMcpSendMode.Direct);

        var json = Parse(await tools.SendPushAsync("U1", "嗨"));

        Assert.AreEqual("direct", json.GetProperty("mode").GetString());
        Assert.AreEqual(1, messaging.Pushes.Count);
        Assert.AreEqual("U1", messaging.Pushes[0].To);

        var items = await outbox.ListAsync();
        Assert.AreEqual(LineMcpOutboxStatus.Sent, items[0].Status, "直接模式一樣留紀錄,否則事後查不出訊息是誰發的。");
        Assert.IsNotNull(items[0].SentAt);
    }

    [TestMethod]
    public async Task SendPush_DirectModeButLineRefuses_RecordsAsFailed()
    {
        var (tools, messaging, outbox) = CreateTools(LineMcpSendMode.Direct);
        messaging.SendFailure = Error.Validation(LineErrorCodes.ApiError, "LINE 拒絕了。");

        var json = Parse(await tools.SendPushAsync("U1", "嗨"));

        Assert.IsFalse(json.GetProperty("ok").GetBoolean());

        var items = await outbox.ListAsync();
        Assert.AreEqual(LineMcpOutboxStatus.Failed, items[0].Status);
        Assert.IsNotNull(items[0].Error);
        Assert.IsNull(items[0].SentAt, "沒送出去就不該有送出時間 —— 稽核紀錄說謊比沒有稽核紀錄更糟。");
    }

    [TestMethod]
    public async Task SendPush_BothTextAndMessagesJson_IsRefused()
    {
        // 挑一個來用的話,AI 送了兩份不同的內容、系統安靜地用了其中一份,而那種落差事後沒有線索可循。
        var (tools, _, outbox) = CreateTools();

        var json = Parse(await tools.SendPushAsync("U1", "嗨", """[{"type":"text","text":"別的"}]"""));

        Assert.IsFalse(json.GetProperty("ok").GetBoolean());
        Assert.AreEqual(0, (await outbox.ListAsync()).Count);
    }

    [TestMethod]
    public async Task SendPush_NeitherTextNorMessagesJson_IsRefused()
    {
        var (tools, _, _) = CreateTools();

        Assert.IsFalse(Parse(await tools.SendPushAsync("U1")).GetProperty("ok").GetBoolean());
    }

    [TestMethod]
    public async Task SendPush_SixMessages_IsRefused()
    {
        var (tools, _, _) = CreateTools();
        var six = "[" + string.Join(',', Enumerable.Repeat("""{"type":"text","text":"x"}""", 6)) + "]";

        var json = Parse(await tools.SendPushAsync("U1", messagesJson: six));

        Assert.IsFalse(json.GetProperty("ok").GetBoolean());
    }

    [TestMethod]
    public async Task SendPush_PayloadKeepsRecipientAndMessages()
    {
        var (tools, _, outbox) = CreateTools();

        await tools.SendPushAsync("U1", "嗨");
        var items = await outbox.ListAsync();

        using var payload = JsonDocument.Parse(items[0].PayloadJson);
        Assert.AreEqual("U1", payload.RootElement.GetProperty("to").GetString());
        Assert.AreEqual("嗨", payload.RootElement.GetProperty("messages")[0].GetProperty("text").GetString());
    }

    [TestMethod]
    public async Task SendMulticast_OverFiveHundred_IsRefused()
    {
        var (tools, _, outbox) = CreateTools();
        var userIds = Enumerable.Range(0, 501).Select(index => $"U{index}").ToArray();

        var json = Parse(await tools.SendMulticastAsync(userIds, "嗨"));

        Assert.IsFalse(json.GetProperty("ok").GetBoolean());
        Assert.AreEqual(0, (await outbox.ListAsync()).Count);
    }

    [TestMethod]
    public async Task SendMulticast_DirectMode_CallsMulticast()
    {
        var (tools, messaging, _) = CreateTools(LineMcpSendMode.Direct);

        await tools.SendMulticastAsync(["U1", "U2"], "嗨");

        Assert.AreEqual(1, messaging.Multicasts.Count);
        Assert.AreEqual(2, messaging.Multicasts[0].To.Count);
        Assert.AreEqual(1, messaging.Multicasts[0].MessageCount);
    }

    [TestMethod]
    public async Task SendBroadcast_DirectMode_CallsBroadcast()
    {
        var (tools, messaging, _) = CreateTools(LineMcpSendMode.Direct);

        await tools.SendBroadcastAsync("公告");

        Assert.AreEqual(1, messaging.Broadcasts.Count);

        using var document = JsonDocument.Parse(messaging.Broadcasts[0]);
        Assert.AreEqual("公告", document.RootElement[0].GetProperty("text").GetString());
    }

    [TestMethod]
    public async Task DailyLimit_EleventhRequest_IsRefused()
    {
        // AI 在迴圈裡重試同一個工具是常見的失敗模式,而沒有上限時那個迴圈會把待審佇列灌到沒有人願意看。
        var (tools, _, outbox) = CreateTools();

        for (var index = 0; index < 10; index++)
        {
            Assert.IsTrue(Parse(await tools.SendPushAsync("U1", $"第 {index} 則")).GetProperty("ok").GetBoolean());
        }

        var refused = Parse(await tools.SendPushAsync("U1", "第 11 則"));

        Assert.IsFalse(refused.GetProperty("ok").GetBoolean());
        StringAssert.Contains(refused.GetProperty("error").GetString(), "10", "訊息要說出上限,否則 AI 多半會原樣再試一次。");
        Assert.AreEqual(10, (await outbox.ListAsync(limit: 100)).Count);
    }

    [TestMethod]
    public async Task DailyLimit_CountsCreatedRatherThanSent()
    {
        // 以送出計數的話,一個從不核准的佇列可以被無限灌下去。
        var (tools, _, outbox) = CreateTools();

        for (var index = 0; index < 10; index++)
        {
            await tools.SendPushAsync("U1", $"第 {index} 則");
        }

        var pending = await outbox.ListAsync(LineMcpOutboxStatus.PendingReview, limit: 100);
        Assert.AreEqual(10, pending.Count, "十筆都還在待審,一則都沒送出,但額度已經用完。");
        Assert.IsFalse(Parse(await tools.SendPushAsync("U1", "再一則")).GetProperty("ok").GetBoolean());
    }

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
    /// 建立送出工具與它的假相依。
    /// Creates the sending tools and their fakes.
    /// </summary>
    /// <param name="mode">送出模式。The send mode.</param>
    /// <returns>工具、假用戶端與待發佇列。The tools, the fake client, and the outbox.</returns>
    private static (LineSendTools Tools, FakeLineMessagingClient Messaging, ILineMcpOutboxStore Outbox) CreateTools(
        LineMcpSendMode mode = LineMcpSendMode.Review)
    {
        var messaging = new FakeLineMessagingClient();
        var outbox = new InMemoryLineMcpOutboxStore();
        var options = Options.Create(new LineMcpOptions { SendMode = mode });

        return (new LineSendTools(messaging, outbox, options), messaging, outbox);
    }
}
