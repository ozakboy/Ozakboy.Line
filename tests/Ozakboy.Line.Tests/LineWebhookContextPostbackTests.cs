using Ozakboy.Line.Postback;
using Ozakboy.Line.Tests.TestSupport;
using Ozakboy.Line.Webhook.Handling;

namespace Ozakboy.Line.Tests;

/// <summary>
/// <see cref="LineWebhookContext.ResolvePostbackDataAsync"/> 與 postback 暫存的接線測試。
/// Tests for <see cref="LineWebhookContext.ResolvePostbackDataAsync"/> and its wiring to the postback store.
/// </summary>
[TestClass]
public sealed class LineWebhookContextPostbackTests
{
    [TestMethod]
    public async Task NotAPostbackEvent_FailsWithNotPostback()
    {
        var context = CreateContext(new LineWebhookEvent { Type = LineWebhookEventTypes.Message }, new InMemoryLinePostbackDataStore());

        var resolved = await context.ResolvePostbackDataAsync();

        Assert.IsTrue(resolved.IsFailure);
        Assert.AreEqual("line.webhook.not_postback", resolved.Error!.Code, "錯誤碼字面值是契約。");
    }

    [TestMethod]
    public async Task PostbackEventWithoutPostbackBody_FailsWithNotPostback()
    {
        var context = CreateContext(new LineWebhookEvent { Type = LineWebhookEventTypes.Postback }, new InMemoryLinePostbackDataStore());

        var resolved = await context.ResolvePostbackDataAsync();

        Assert.AreEqual(LineErrorCodes.WebhookNotPostback, resolved.Error!.Code);
    }

    [TestMethod]
    public async Task PlainData_IsReturnedAsIs_WithOrWithoutAStore()
    {
        var withStore = CreateContext(PostbackEvent("action=buy&id=7"), new InMemoryLinePostbackDataStore());
        var withoutStore = CreateContext(PostbackEvent("action=buy&id=7"), store: null);

        Assert.AreEqual("action=buy&id=7", (await withStore.ResolvePostbackDataAsync()).GetValueOrThrow());
        Assert.AreEqual("action=buy&id=7", (await withoutStore.ResolvePostbackDataAsync()).GetValueOrThrow(), "不是代碼就不需要儲存體。");
    }

    [TestMethod]
    public async Task Token_IsResolvedThroughTheStore()
    {
        var store = new InMemoryLinePostbackDataStore();
        var token = (await store.StoreAsync("很長很長的資料")).GetValueOrThrow();
        var context = CreateContext(PostbackEvent(token), store);

        var resolved = await context.ResolvePostbackDataAsync();

        Assert.AreEqual("很長很長的資料", resolved.GetValueOrThrow());
    }

    [TestMethod]
    public async Task TokenWithoutStore_FailsWithNotConfigured()
    {
        var context = CreateContext(PostbackEvent("ozl:" + new string('0', 32)), store: null);

        var resolved = await context.ResolvePostbackDataAsync();

        Assert.IsTrue(resolved.IsFailure);
        Assert.AreEqual(LineErrorCodes.NotConfigured, resolved.Error!.Code);
    }

    [TestMethod]
    public async Task UnknownToken_FailsWithExpired_ThatIsTheSameFailureAsAnExpiredOne()
    {
        var context = CreateContext(PostbackEvent("ozl:" + new string('a', 32)), new InMemoryLinePostbackDataStore());

        var resolved = await context.ResolvePostbackDataAsync();

        Assert.AreEqual(LineErrorCodes.PostbackExpired, resolved.Error!.Code);
    }

    [TestMethod]
    public async Task StoreIsOptionalInTheConstructor_AndReplyBehaviourIsUntouched()
    {
        var (client, recorder) = RecordingLineMessagingClient.Create();
        var context = new LineWebhookContext(PostbackEvent("x"), client);

        var replied = await context.ReplyTextAsync("好");

        Assert.IsTrue(replied.IsSuccess);
        Assert.AreEqual(1, recorder.Sends.Count);
    }

    private static LineWebhookContext CreateContext(LineWebhookEvent webhookEvent, ILinePostbackDataStore? store)
    {
        var (client, _) = RecordingLineMessagingClient.Create();
        return new LineWebhookContext(webhookEvent, client, string.Empty, null, store);
    }

    private static LineWebhookEvent PostbackEvent(string data) => new()
    {
        Type = LineWebhookEventTypes.Postback,
        Mode = "active",
        ReplyToken = "rt-1",
        Source = new LineWebhookSource { Type = "user", UserId = "U1" },
        Postback = new LineWebhookPostback { Data = data },
    };
}
