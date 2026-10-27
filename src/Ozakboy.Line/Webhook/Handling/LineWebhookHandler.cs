namespace Ozakboy.Line.Webhook.Handling;

/// <summary>
/// webhook 事件分派的基底類別:依事件型別(message 再依訊息型別)呼叫對應的虛擬方法。
/// A base class for webhook event dispatch: it calls the virtual method matching the event type — and, for a
/// message event, the message type.
/// </summary>
/// <remarks>
/// <para>
/// 所有 <c>On*Async</c> 預設都是<b>什麼都不做</b>,只覆寫關心的事件即可。對外唯一的入口是
/// <see cref="HandleAsync"/>(非虛擬),端點(<c>MapLineWebhook&lt;THandler&gt;</c>)或測試直接呼叫它。
/// Every <c>On*Async</c> method does <b>nothing</b> by default; override only the events you care about. The one
/// public entry point is <see cref="HandleAsync"/> (non-virtual), which the endpoint
/// (<c>MapLineWebhook&lt;THandler&gt;</c>) or a test calls directly.
/// </para>
/// <para>
/// <b>message 事件的呼叫順序</b>:先 <see cref="OnMessageAsync"/>,等它完成後再呼叫依訊息型別細分的方法
/// (<see cref="OnTextMessageAsync"/>、<see cref="OnImageMessageAsync"/> …)。訊息型別不是已知的七種,
/// 或事件沒有訊息內容時,只有 <see cref="OnMessageAsync"/> 會被呼叫。
/// <b>Order for a message event</b>: <see cref="OnMessageAsync"/> first, and once it completes the method for
/// the specific message type (<see cref="OnTextMessageAsync"/>, <see cref="OnImageMessageAsync"/> …). When the
/// message type is not one of the seven known ones, or the event has no message body, only
/// <see cref="OnMessageAsync"/> is called.
/// </para>
/// <para>
/// 未列在 <see cref="LineWebhookEventTypes"/> 的型別,以及 <c>things</c>,走 <see cref="OnUnknownEventAsync"/>。
/// 例外不會在這裡被吞掉 —— 端點才負責「一個事件失敗不影響同一批」的隔離。
/// Types not listed in <see cref="LineWebhookEventTypes"/>, and <c>things</c>, go to
/// <see cref="OnUnknownEventAsync"/>. Exceptions are not swallowed here; isolating "one event failing must not
/// affect the batch" is the endpoint's job.
/// </para>
/// <para>
/// <b>去重</b>:LINE 重送時 <see cref="LineWebhookEvent.WebhookEventId"/> 不變,有副作用的處理請自行以它去重。
/// <b>Deduplication</b>: <see cref="LineWebhookEvent.WebhookEventId"/> stays the same across a redelivery, so a
/// handler with side effects must deduplicate on it.
/// </para>
/// </remarks>
public abstract class LineWebhookHandler
{
    /// <summary>
    /// 依事件型別分派到對應的虛擬方法。
    /// Dispatches to the virtual method matching the event type.
    /// </summary>
    /// <param name="webhookEvent">事件。The event.</param>
    /// <param name="context">這個事件的脈絡。The context for this event.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>處理完成時的工作。A task that completes when handling is done.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="webhookEvent"/> 或 <paramref name="context"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="webhookEvent"/> or <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    public async Task HandleAsync(LineWebhookEvent webhookEvent, LineWebhookContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(webhookEvent);
        ArgumentNullException.ThrowIfNull(context);

        switch (webhookEvent.Type)
        {
            case LineWebhookEventTypes.Message:
                await OnMessageAsync(webhookEvent, context, cancellationToken).ConfigureAwait(false);
                await DispatchMessageAsync(webhookEvent, context, cancellationToken).ConfigureAwait(false);
                break;
            case LineWebhookEventTypes.Postback:
                await OnPostbackAsync(webhookEvent, context, cancellationToken).ConfigureAwait(false);
                break;
            case LineWebhookEventTypes.Follow:
                await OnFollowAsync(webhookEvent, context, cancellationToken).ConfigureAwait(false);
                break;
            case LineWebhookEventTypes.Unfollow:
                await OnUnfollowAsync(webhookEvent, context, cancellationToken).ConfigureAwait(false);
                break;
            case LineWebhookEventTypes.Join:
                await OnJoinAsync(webhookEvent, context, cancellationToken).ConfigureAwait(false);
                break;
            case LineWebhookEventTypes.Leave:
                await OnLeaveAsync(webhookEvent, context, cancellationToken).ConfigureAwait(false);
                break;
            case LineWebhookEventTypes.MemberJoined:
                await OnMemberJoinedAsync(webhookEvent, context, cancellationToken).ConfigureAwait(false);
                break;
            case LineWebhookEventTypes.MemberLeft:
                await OnMemberLeftAsync(webhookEvent, context, cancellationToken).ConfigureAwait(false);
                break;
            case LineWebhookEventTypes.Beacon:
                await OnBeaconAsync(webhookEvent, context, cancellationToken).ConfigureAwait(false);
                break;
            case LineWebhookEventTypes.AccountLink:
                await OnAccountLinkAsync(webhookEvent, context, cancellationToken).ConfigureAwait(false);
                break;
            case LineWebhookEventTypes.Unsend:
                await OnUnsendAsync(webhookEvent, context, cancellationToken).ConfigureAwait(false);
                break;
            case LineWebhookEventTypes.VideoPlayComplete:
                await OnVideoPlayCompleteAsync(webhookEvent, context, cancellationToken).ConfigureAwait(false);
                break;
            case LineWebhookEventTypes.Membership:
                await OnMembershipAsync(webhookEvent, context, cancellationToken).ConfigureAwait(false);
                break;
            default:
                await OnUnknownEventAsync(webhookEvent, context, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    /// 依訊息型別呼叫細分的方法;型別未知或沒有訊息內容時不呼叫任何方法。
    /// Calls the method for the specific message type, and none when the type is unfamiliar or the body is missing.
    /// </summary>
    /// <param name="webhookEvent">事件。The event.</param>
    /// <param name="context">這個事件的脈絡。The context for this event.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>處理完成時的工作。A task that completes when handling is done.</returns>
    private Task DispatchMessageAsync(LineWebhookEvent webhookEvent, LineWebhookContext context, CancellationToken cancellationToken) =>
        webhookEvent.Message?.Type switch
        {
            LineWebhookMessageTypes.Text => OnTextMessageAsync(webhookEvent, context, cancellationToken),
            LineWebhookMessageTypes.Image => OnImageMessageAsync(webhookEvent, context, cancellationToken),
            LineWebhookMessageTypes.Video => OnVideoMessageAsync(webhookEvent, context, cancellationToken),
            LineWebhookMessageTypes.Audio => OnAudioMessageAsync(webhookEvent, context, cancellationToken),
            LineWebhookMessageTypes.File => OnFileMessageAsync(webhookEvent, context, cancellationToken),
            LineWebhookMessageTypes.Location => OnLocationMessageAsync(webhookEvent, context, cancellationToken),
            LineWebhookMessageTypes.Sticker => OnStickerMessageAsync(webhookEvent, context, cancellationToken),
            _ => Task.CompletedTask,
        };

    /// <summary>
    /// 文字訊息(message 事件,訊息型別 text)。
    /// A text message (a message event whose message type is text).
    /// </summary>
    /// <param name="webhookEvent">事件。The event.</param>
    /// <param name="context">這個事件的脈絡。The context for this event.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>完成時的工作;預設什麼都不做。A task that completes when done; does nothing by default.</returns>
    protected virtual Task OnTextMessageAsync(LineWebhookEvent webhookEvent, LineWebhookContext context, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <summary>
    /// 圖片訊息。
    /// An image message.
    /// </summary>
    /// <param name="webhookEvent">事件。The event.</param>
    /// <param name="context">這個事件的脈絡。The context for this event.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>完成時的工作;預設什麼都不做。A task that completes when done; does nothing by default.</returns>
    protected virtual Task OnImageMessageAsync(LineWebhookEvent webhookEvent, LineWebhookContext context, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <summary>
    /// 影片訊息。
    /// A video message.
    /// </summary>
    /// <param name="webhookEvent">事件。The event.</param>
    /// <param name="context">這個事件的脈絡。The context for this event.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>完成時的工作;預設什麼都不做。A task that completes when done; does nothing by default.</returns>
    protected virtual Task OnVideoMessageAsync(LineWebhookEvent webhookEvent, LineWebhookContext context, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <summary>
    /// 語音訊息。
    /// An audio message.
    /// </summary>
    /// <param name="webhookEvent">事件。The event.</param>
    /// <param name="context">這個事件的脈絡。The context for this event.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>完成時的工作;預設什麼都不做。A task that completes when done; does nothing by default.</returns>
    protected virtual Task OnAudioMessageAsync(LineWebhookEvent webhookEvent, LineWebhookContext context, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <summary>
    /// 檔案訊息。
    /// A file message.
    /// </summary>
    /// <param name="webhookEvent">事件。The event.</param>
    /// <param name="context">這個事件的脈絡。The context for this event.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>完成時的工作;預設什麼都不做。A task that completes when done; does nothing by default.</returns>
    protected virtual Task OnFileMessageAsync(LineWebhookEvent webhookEvent, LineWebhookContext context, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <summary>
    /// 位置訊息。
    /// A location message.
    /// </summary>
    /// <param name="webhookEvent">事件。The event.</param>
    /// <param name="context">這個事件的脈絡。The context for this event.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>完成時的工作;預設什麼都不做。A task that completes when done; does nothing by default.</returns>
    protected virtual Task OnLocationMessageAsync(LineWebhookEvent webhookEvent, LineWebhookContext context, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <summary>
    /// 貼圖訊息。
    /// A sticker message.
    /// </summary>
    /// <param name="webhookEvent">事件。The event.</param>
    /// <param name="context">這個事件的脈絡。The context for this event.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>完成時的工作;預設什麼都不做。A task that completes when done; does nothing by default.</returns>
    protected virtual Task OnStickerMessageAsync(LineWebhookEvent webhookEvent, LineWebhookContext context, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <summary>
    /// 任何 message 事件;<b>先於</b>依訊息型別細分的 On*MessageAsync 被呼叫,訊息型別未知或沒有訊息內容時只會呼叫這一個。
    /// Any message event. It is called <b>before</b> the On*MessageAsync method for the specific message type, and is the only one called when the message type is unfamiliar or the message body is missing.
    /// </summary>
    /// <param name="webhookEvent">事件。The event.</param>
    /// <param name="context">這個事件的脈絡。The context for this event.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>完成時的工作;預設什麼都不做。A task that completes when done; does nothing by default.</returns>
    protected virtual Task OnMessageAsync(LineWebhookEvent webhookEvent, LineWebhookContext context, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <summary>
    /// 使用者觸發了回傳動作(postback)。
    /// The user triggered a postback action.
    /// </summary>
    /// <param name="webhookEvent">事件。The event.</param>
    /// <param name="context">這個事件的脈絡。The context for this event.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>完成時的工作;預設什麼都不做。A task that completes when done; does nothing by default.</returns>
    protected virtual Task OnPostbackAsync(LineWebhookEvent webhookEvent, LineWebhookContext context, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <summary>
    /// 使用者加入好友或解除封鎖(follow)。
    /// The user added the account or unblocked it (follow).
    /// </summary>
    /// <param name="webhookEvent">事件。The event.</param>
    /// <param name="context">這個事件的脈絡。The context for this event.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>完成時的工作;預設什麼都不做。A task that completes when done; does nothing by default.</returns>
    protected virtual Task OnFollowAsync(LineWebhookEvent webhookEvent, LineWebhookContext context, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <summary>
    /// 使用者封鎖了官方帳號(unfollow);此事件沒有回覆權杖。
    /// The user blocked the account (unfollow); this event has no reply token.
    /// </summary>
    /// <param name="webhookEvent">事件。The event.</param>
    /// <param name="context">這個事件的脈絡。The context for this event.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>完成時的工作;預設什麼都不做。A task that completes when done; does nothing by default.</returns>
    protected virtual Task OnUnfollowAsync(LineWebhookEvent webhookEvent, LineWebhookContext context, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <summary>
    /// 官方帳號被加入群組或聊天室(join)。
    /// The account was added to a group or room (join).
    /// </summary>
    /// <param name="webhookEvent">事件。The event.</param>
    /// <param name="context">這個事件的脈絡。The context for this event.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>完成時的工作;預設什麼都不做。A task that completes when done; does nothing by default.</returns>
    protected virtual Task OnJoinAsync(LineWebhookEvent webhookEvent, LineWebhookContext context, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <summary>
    /// 官方帳號被移出群組或聊天室(leave);此事件沒有回覆權杖。
    /// The account was removed from a group or room (leave); this event has no reply token.
    /// </summary>
    /// <param name="webhookEvent">事件。The event.</param>
    /// <param name="context">這個事件的脈絡。The context for this event.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>完成時的工作;預設什麼都不做。A task that completes when done; does nothing by default.</returns>
    protected virtual Task OnLeaveAsync(LineWebhookEvent webhookEvent, LineWebhookContext context, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <summary>
    /// 有成員加入群組或聊天室(memberJoined)。
    /// A member joined a group or room (memberJoined).
    /// </summary>
    /// <param name="webhookEvent">事件。The event.</param>
    /// <param name="context">這個事件的脈絡。The context for this event.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>完成時的工作;預設什麼都不做。A task that completes when done; does nothing by default.</returns>
    protected virtual Task OnMemberJoinedAsync(LineWebhookEvent webhookEvent, LineWebhookContext context, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <summary>
    /// 有成員離開群組或聊天室(memberLeft);此事件沒有回覆權杖。
    /// A member left a group or room (memberLeft); this event has no reply token.
    /// </summary>
    /// <param name="webhookEvent">事件。The event.</param>
    /// <param name="context">這個事件的脈絡。The context for this event.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>完成時的工作;預設什麼都不做。A task that completes when done; does nothing by default.</returns>
    protected virtual Task OnMemberLeftAsync(LineWebhookEvent webhookEvent, LineWebhookContext context, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <summary>
    /// 使用者進入或離開 beacon 範圍(beacon)。
    /// The user entered or left a beacon's range (beacon).
    /// </summary>
    /// <param name="webhookEvent">事件。The event.</param>
    /// <param name="context">這個事件的脈絡。The context for this event.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>完成時的工作;預設什麼都不做。A task that completes when done; does nothing by default.</returns>
    protected virtual Task OnBeaconAsync(LineWebhookEvent webhookEvent, LineWebhookContext context, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <summary>
    /// 帳號連結完成(accountLink)。
    /// An account link completed (accountLink).
    /// </summary>
    /// <param name="webhookEvent">事件。The event.</param>
    /// <param name="context">這個事件的脈絡。The context for this event.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>完成時的工作;預設什麼都不做。A task that completes when done; does nothing by default.</returns>
    protected virtual Task OnAccountLinkAsync(LineWebhookEvent webhookEvent, LineWebhookContext context, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <summary>
    /// 使用者收回了訊息(unsend);此事件沒有回覆權杖。
    /// The user unsent a message (unsend); this event has no reply token.
    /// </summary>
    /// <param name="webhookEvent">事件。The event.</param>
    /// <param name="context">這個事件的脈絡。The context for this event.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>完成時的工作;預設什麼都不做。A task that completes when done; does nothing by default.</returns>
    protected virtual Task OnUnsendAsync(LineWebhookEvent webhookEvent, LineWebhookContext context, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <summary>
    /// 使用者看完了影片訊息(videoPlayComplete)。
    /// The user finished watching a video message (videoPlayComplete).
    /// </summary>
    /// <param name="webhookEvent">事件。The event.</param>
    /// <param name="context">這個事件的脈絡。The context for this event.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>完成時的工作;預設什麼都不做。A task that completes when done; does nothing by default.</returns>
    protected virtual Task OnVideoPlayCompleteAsync(LineWebhookEvent webhookEvent, LineWebhookContext context, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <summary>
    /// 使用者加入、退出或續訂了會員方案(membership)。
    /// The user joined, left, or renewed a membership plan (membership).
    /// </summary>
    /// <param name="webhookEvent">事件。The event.</param>
    /// <param name="context">這個事件的脈絡。The context for this event.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>完成時的工作;預設什麼都不做。A task that completes when done; does nothing by default.</returns>
    protected virtual Task OnMembershipAsync(LineWebhookEvent webhookEvent, LineWebhookContext context, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <summary>
    /// 本類別沒有對應方法的事件型別(LINE 新增的型別,以及 things);原始 JSON 在 Raw。
    /// An event type this class has no method for — a type LINE added later, and things; the original JSON is in Raw.
    /// </summary>
    /// <param name="webhookEvent">事件。The event.</param>
    /// <param name="context">這個事件的脈絡。The context for this event.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>完成時的工作;預設什麼都不做。A task that completes when done; does nothing by default.</returns>
    protected virtual Task OnUnknownEventAsync(LineWebhookEvent webhookEvent, LineWebhookContext context, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
