using Ozakboy.Line.Messaging.Messages;
using Ozakboy.Line.Tests.TestSupport;
using Ozakboy.Line.Webhook.Handling;

namespace Ozakboy.Line.Tests;

/// <summary>
/// <see cref="LineWebhookHandler"/> 的分派與 <see cref="LineWebhookContext"/> 的回覆測試。
/// Dispatch tests for <see cref="LineWebhookHandler"/> and reply tests for <see cref="LineWebhookContext"/>.
/// </summary>
[TestClass]
public sealed class LineWebhookHandlerTests
{
    private static readonly string[] OnlyOnMessage = ["OnMessageAsync"];
    private static readonly string[] OnlyOnUnknownEvent = ["OnUnknownEventAsync"];

    [TestMethod]
    [DataRow(LineWebhookMessageTypes.Text, "OnTextMessageAsync")]
    [DataRow(LineWebhookMessageTypes.Image, "OnImageMessageAsync")]
    [DataRow(LineWebhookMessageTypes.Video, "OnVideoMessageAsync")]
    [DataRow(LineWebhookMessageTypes.Audio, "OnAudioMessageAsync")]
    [DataRow(LineWebhookMessageTypes.File, "OnFileMessageAsync")]
    [DataRow(LineWebhookMessageTypes.Location, "OnLocationMessageAsync")]
    [DataRow(LineWebhookMessageTypes.Sticker, "OnStickerMessageAsync")]
    public async Task MessageEvent_CallsOnMessageFirstThenTheSpecificMethod(string messageType, string specific)
    {
        var handler = new RecordingHandler();

        await Dispatch(handler, Event(LineWebhookEventTypes.Message, messageType));

        CollectionAssert.AreEqual(new[] { "OnMessageAsync", specific }, handler.Calls, "OnMessageAsync 必須先於細分的方法。");
    }

    [TestMethod]
    public async Task MessageEventWithUnknownMessageType_CallsOnlyOnMessage()
    {
        var handler = new RecordingHandler();

        await Dispatch(handler, Event(LineWebhookEventTypes.Message, "hologram"));

        CollectionAssert.AreEqual(OnlyOnMessage, handler.Calls);
    }

    [TestMethod]
    public async Task MessageEventWithoutMessageBody_CallsOnlyOnMessage()
    {
        var handler = new RecordingHandler();

        await Dispatch(handler, Event(LineWebhookEventTypes.Message, messageType: null));

        CollectionAssert.AreEqual(OnlyOnMessage, handler.Calls);
    }

    [TestMethod]
    [DataRow(LineWebhookEventTypes.Postback, "OnPostbackAsync")]
    [DataRow(LineWebhookEventTypes.Follow, "OnFollowAsync")]
    [DataRow(LineWebhookEventTypes.Unfollow, "OnUnfollowAsync")]
    [DataRow(LineWebhookEventTypes.Join, "OnJoinAsync")]
    [DataRow(LineWebhookEventTypes.Leave, "OnLeaveAsync")]
    [DataRow(LineWebhookEventTypes.MemberJoined, "OnMemberJoinedAsync")]
    [DataRow(LineWebhookEventTypes.MemberLeft, "OnMemberLeftAsync")]
    [DataRow(LineWebhookEventTypes.Beacon, "OnBeaconAsync")]
    [DataRow(LineWebhookEventTypes.AccountLink, "OnAccountLinkAsync")]
    [DataRow(LineWebhookEventTypes.Unsend, "OnUnsendAsync")]
    [DataRow(LineWebhookEventTypes.VideoPlayComplete, "OnVideoPlayCompleteAsync")]
    [DataRow(LineWebhookEventTypes.Membership, "OnMembershipAsync")]
    public async Task NonMessageEvent_CallsExactlyItsMethod(string eventType, string expected)
    {
        var handler = new RecordingHandler();

        await Dispatch(handler, Event(eventType));

        CollectionAssert.AreEqual(new[] { expected }, handler.Calls);
    }

    [TestMethod]
    [DataRow("somethingLineAddedLater")]
    [DataRow(LineWebhookEventTypes.Things)]
    [DataRow("")]
    public async Task UnfamiliarEventType_CallsOnUnknownEvent(string eventType)
    {
        var handler = new RecordingHandler();

        await Dispatch(handler, Event(eventType));

        CollectionAssert.AreEqual(OnlyOnUnknownEvent, handler.Calls);
    }

    [TestMethod]
    public async Task DefaultImplementations_DoNothing()
    {
        var handler = new EmptyHandler();

        // 沒有覆寫任何方法:每一種事件都應該靜靜地完成。
        foreach (var type in new[] { "message", "postback", "follow", "unfollow", "join", "leave", "memberJoined", "memberLeft", "beacon", "accountLink", "unsend", "videoPlayComplete", "membership", "things", "x" })
        {
            await Dispatch(handler, Event(type, LineWebhookMessageTypes.Text));
        }
    }

    [TestMethod]
    public async Task HandlerReceivesTheSameEventAndContext()
    {
        var handler = new RecordingHandler();
        var webhookEvent = Event(LineWebhookEventTypes.Follow);
        var context = NewContext(webhookEvent, out _);

        await handler.HandleAsync(webhookEvent, context, CancellationToken.None);

        Assert.AreSame(webhookEvent, handler.LastEvent);
        Assert.AreSame(context, handler.LastContext);
    }

    [TestMethod]
    public async Task ExceptionInOverride_Propagates()
    {
        // 隔離是端點的責任,基底類別不吞例外。
        var handler = new ThrowingHandler();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => Dispatch(handler, Event(LineWebhookEventTypes.Follow)));
    }

    [TestMethod]
    public async Task NullArguments_Throw()
    {
        var handler = new EmptyHandler();
        var webhookEvent = Event(LineWebhookEventTypes.Follow);
        var context = NewContext(webhookEvent, out _);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => handler.HandleAsync(null!, context, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => handler.HandleAsync(webhookEvent, null!, CancellationToken.None));
    }

    [TestMethod]
    public async Task ReplyAsync_UsesTheEventReplyToken()
    {
        var webhookEvent = Event(LineWebhookEventTypes.Message, LineWebhookMessageTypes.Text, replyToken: "rt-1");
        var context = NewContext(webhookEvent, out var recorder);

        var result = await context.ReplyAsync([new TextMessage("hi")], CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        var send = recorder.Sends.Single();
        Assert.AreEqual("ReplyAsync", send.Method);
        Assert.AreEqual("rt-1", send.Target);
        Assert.AreEqual("hi", send.Text);
    }

    [TestMethod]
    public async Task ReplyTextAsync_UsesTheEventReplyToken()
    {
        var webhookEvent = Event(LineWebhookEventTypes.Follow, replyToken: "rt-2");
        var context = NewContext(webhookEvent, out var recorder);

        var result = await context.ReplyTextAsync("歡迎", CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        var send = recorder.Sends.Single();
        Assert.AreEqual("ReplyTextAsync", send.Method);
        Assert.AreEqual("rt-2", send.Target);
        Assert.AreEqual("歡迎", send.Text);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public async Task Reply_WithoutReplyToken_FailsLocallyWithoutThrowingOrSending(string? replyToken)
    {
        var webhookEvent = Event(LineWebhookEventTypes.Unfollow, replyToken: replyToken);
        var context = NewContext(webhookEvent, out var recorder);

        var text = await context.ReplyTextAsync("x", CancellationToken.None);
        var messages = await context.ReplyAsync([new TextMessage("x")], CancellationToken.None);

        Assert.IsTrue(text.IsFailure);
        Assert.IsNotNull(text.Error);
        Assert.IsNotNull(messages.Error);
        Assert.AreEqual("line.webhook.no_reply_token", text.Error.Code, "錯誤代碼字面值是契約。");
        Assert.AreEqual("line.webhook.no_reply_token", messages.Error.Code);
        Assert.AreEqual(0, recorder.Sends.Count);
    }

    [TestMethod]
    public async Task SecondReply_FailsLocallyAsTokenUsed_AndSendsNothing()
    {
        var webhookEvent = Event(LineWebhookEventTypes.Message, LineWebhookMessageTypes.Text, replyToken: "rt-3");
        var context = NewContext(webhookEvent, out var recorder);

        var first = await context.ReplyTextAsync("一", CancellationToken.None);
        var second = await context.ReplyTextAsync("二", CancellationToken.None);
        var third = await context.ReplyAsync([new TextMessage("三")], CancellationToken.None);

        Assert.IsTrue(first.IsSuccess);
        Assert.IsNotNull(second.Error);
        Assert.IsNotNull(third.Error);
        Assert.AreEqual("line.webhook.reply_token_used", second.Error.Code, "錯誤代碼字面值是契約。");
        Assert.AreEqual("line.webhook.reply_token_used", third.Error.Code);
        Assert.AreEqual(1, recorder.Sends.Count, "權杖只能用一次,第二次不送。");
    }

    [TestMethod]
    public async Task ReplyThatLineRejects_StillCountsAsUsed()
    {
        var webhookEvent = Event(LineWebhookEventTypes.Message, LineWebhookMessageTypes.Text, replyToken: "rt-4");
        var context = NewContext(webhookEvent, out var recorder);
        recorder.ReplyFailure = Ozakboy.Core.Abstractions.Error.Validation("line.api.rejected", "拒絕");

        var first = await context.ReplyTextAsync("一", CancellationToken.None);
        var second = await context.ReplyTextAsync("二", CancellationToken.None);

        Assert.IsTrue(first.IsFailure);
        Assert.IsNotNull(first.Error);
        Assert.IsNotNull(second.Error);
        Assert.AreEqual("line.api.rejected", first.Error.Code);
        Assert.AreEqual(LineErrorCodes.WebhookReplyTokenUsed, second.Error.Code);
        Assert.AreEqual(1, recorder.Sends.Count);
    }

    [TestMethod]
    public async Task ConcurrentReplies_OnlyOneGetsTheToken()
    {
        var webhookEvent = Event(LineWebhookEventTypes.Message, LineWebhookMessageTypes.Text, replyToken: "rt-5");
        var context = NewContext(webhookEvent, out var recorder);

        var results = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(i => Task.Run(() => context.ReplyTextAsync($"#{i}", CancellationToken.None))));

        Assert.AreEqual(1, results.Count(r => r.IsSuccess));
        Assert.AreEqual(1, recorder.Sends.Count);
    }

    [TestMethod]
    public void Context_DefaultsAndRequiredArguments()
    {
        var webhookEvent = Event(LineWebhookEventTypes.Follow);
        var context = NewContext(webhookEvent, out _);

        Assert.AreEqual(string.Empty, context.ChannelName);
        Assert.IsNull(context.AutoReplyOutcome);
        Assert.AreEqual(0, context.Items.Count);
        context.Items["k"] = 1;
        Assert.AreEqual(1, context.Items["k"]);
        Assert.AreSame(webhookEvent, context.Event);

        var (client, _) = RecordingLineMessagingClient.Create();
        Assert.ThrowsExactly<ArgumentNullException>(() => new LineWebhookContext(null!, client));
        Assert.ThrowsExactly<ArgumentNullException>(() => new LineWebhookContext(webhookEvent, null!));
        Assert.AreEqual("main", new LineWebhookContext(webhookEvent, client, "main").ChannelName);
    }

    /// <summary>
    /// 建立事件。
    /// Builds an event.
    /// </summary>
    /// <param name="type">事件型別。The event type.</param>
    /// <param name="messageType">訊息型別;為 <see langword="null"/> 時不帶訊息內容。The message type, or <see langword="null"/> for no message body.</param>
    /// <param name="replyToken">回覆權杖。The reply token.</param>
    /// <returns>事件。The event.</returns>
    private static LineWebhookEvent Event(string type, string? messageType = null, string? replyToken = "rt") => new()
    {
        Type = type,
        ReplyToken = replyToken,
        WebhookEventId = "evt-1",
        Source = new LineWebhookSource { Type = "user", UserId = "U1" },
        Message = messageType is null ? null : new LineWebhookMessage { Id = "m", Type = messageType, Text = "t" },
    };

    private static LineWebhookContext NewContext(LineWebhookEvent webhookEvent, out RecordingLineMessagingClient recorder)
    {
        var (client, rec) = RecordingLineMessagingClient.Create();
        recorder = rec;
        return new LineWebhookContext(webhookEvent, client);
    }

    private static Task Dispatch(LineWebhookHandler handler, LineWebhookEvent webhookEvent)
    {
        var context = NewContext(webhookEvent, out _);
        return handler.HandleAsync(webhookEvent, context, CancellationToken.None);
    }

    /// <summary>
    /// 覆寫全部虛擬方法,記錄被呼叫的順序。
    /// Overrides every virtual method and records the order they were called in.
    /// </summary>
    private sealed class RecordingHandler : LineWebhookHandler
    {
        internal List<string> Calls { get; } = [];

        internal LineWebhookEvent? LastEvent { get; private set; }

        internal LineWebhookContext? LastContext { get; private set; }

        private Task Record(string name, LineWebhookEvent webhookEvent, LineWebhookContext context)
        {
            Calls.Add(name);
            LastEvent = webhookEvent;
            LastContext = context;
            return Task.CompletedTask;
        }

        protected override Task OnTextMessageAsync(LineWebhookEvent e, LineWebhookContext c, CancellationToken ct) => Record(nameof(OnTextMessageAsync), e, c);

        protected override Task OnImageMessageAsync(LineWebhookEvent e, LineWebhookContext c, CancellationToken ct) => Record(nameof(OnImageMessageAsync), e, c);

        protected override Task OnVideoMessageAsync(LineWebhookEvent e, LineWebhookContext c, CancellationToken ct) => Record(nameof(OnVideoMessageAsync), e, c);

        protected override Task OnAudioMessageAsync(LineWebhookEvent e, LineWebhookContext c, CancellationToken ct) => Record(nameof(OnAudioMessageAsync), e, c);

        protected override Task OnFileMessageAsync(LineWebhookEvent e, LineWebhookContext c, CancellationToken ct) => Record(nameof(OnFileMessageAsync), e, c);

        protected override Task OnLocationMessageAsync(LineWebhookEvent e, LineWebhookContext c, CancellationToken ct) => Record(nameof(OnLocationMessageAsync), e, c);

        protected override Task OnStickerMessageAsync(LineWebhookEvent e, LineWebhookContext c, CancellationToken ct) => Record(nameof(OnStickerMessageAsync), e, c);

        protected override Task OnMessageAsync(LineWebhookEvent e, LineWebhookContext c, CancellationToken ct) => Record(nameof(OnMessageAsync), e, c);

        protected override Task OnPostbackAsync(LineWebhookEvent e, LineWebhookContext c, CancellationToken ct) => Record(nameof(OnPostbackAsync), e, c);

        protected override Task OnFollowAsync(LineWebhookEvent e, LineWebhookContext c, CancellationToken ct) => Record(nameof(OnFollowAsync), e, c);

        protected override Task OnUnfollowAsync(LineWebhookEvent e, LineWebhookContext c, CancellationToken ct) => Record(nameof(OnUnfollowAsync), e, c);

        protected override Task OnJoinAsync(LineWebhookEvent e, LineWebhookContext c, CancellationToken ct) => Record(nameof(OnJoinAsync), e, c);

        protected override Task OnLeaveAsync(LineWebhookEvent e, LineWebhookContext c, CancellationToken ct) => Record(nameof(OnLeaveAsync), e, c);

        protected override Task OnMemberJoinedAsync(LineWebhookEvent e, LineWebhookContext c, CancellationToken ct) => Record(nameof(OnMemberJoinedAsync), e, c);

        protected override Task OnMemberLeftAsync(LineWebhookEvent e, LineWebhookContext c, CancellationToken ct) => Record(nameof(OnMemberLeftAsync), e, c);

        protected override Task OnBeaconAsync(LineWebhookEvent e, LineWebhookContext c, CancellationToken ct) => Record(nameof(OnBeaconAsync), e, c);

        protected override Task OnAccountLinkAsync(LineWebhookEvent e, LineWebhookContext c, CancellationToken ct) => Record(nameof(OnAccountLinkAsync), e, c);

        protected override Task OnUnsendAsync(LineWebhookEvent e, LineWebhookContext c, CancellationToken ct) => Record(nameof(OnUnsendAsync), e, c);

        protected override Task OnVideoPlayCompleteAsync(LineWebhookEvent e, LineWebhookContext c, CancellationToken ct) => Record(nameof(OnVideoPlayCompleteAsync), e, c);

        protected override Task OnMembershipAsync(LineWebhookEvent e, LineWebhookContext c, CancellationToken ct) => Record(nameof(OnMembershipAsync), e, c);

        protected override Task OnUnknownEventAsync(LineWebhookEvent e, LineWebhookContext c, CancellationToken ct) => Record(nameof(OnUnknownEventAsync), e, c);
    }

    /// <summary>
    /// 什麼都不覆寫。
    /// Overrides nothing.
    /// </summary>
    private sealed class EmptyHandler : LineWebhookHandler;

    /// <summary>
    /// 在追蹤好友時擲例外。
    /// Throws on follow.
    /// </summary>
    private sealed class ThrowingHandler : LineWebhookHandler
    {
        protected override Task OnFollowAsync(LineWebhookEvent webhookEvent, LineWebhookContext context, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("boom");
    }
}
