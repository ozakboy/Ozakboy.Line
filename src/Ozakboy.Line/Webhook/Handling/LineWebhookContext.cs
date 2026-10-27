using Ozakboy.Core.Abstractions;
using Ozakboy.Line.AutoReply;
using Ozakboy.Line.Messaging;
using Ozakboy.Line.Messaging.Messages;

namespace Ozakboy.Line.Webhook.Handling;

/// <summary>
/// 處理<b>一個</b> webhook 事件時的執行脈絡:事件、發訊用戶端、自動回覆結果與回覆便利方法。
/// The execution context for handling <b>one</b> webhook event: the event, the messaging client, the auto
/// reply outcome and reply conveniences.
/// </summary>
/// <remarks>
/// <para>
/// 脈絡由框架為每個事件建立一個;回覆權杖一次性的記錄也存在這裡,所以不要在事件之間共用同一個實例。
/// The framework creates one context per event. The record of whether the single-use reply token has been
/// spent lives here, so an instance must not be shared between events.
/// </para>
/// <para>
/// <see cref="ReplyAsync"/> 與 <see cref="ReplyTextAsync"/> 在一個事件上<b>只能發出一次</b>:
/// 第二次呼叫在本地回 <see cref="LineErrorCodes.WebhookReplyTokenUsed"/>、不送請求。
/// 只要有一次呼叫通過了本地檢查而實際送出,就算用掉(即使 LINE 後來拒絕 —— 權杖是否已被消耗無從得知,
/// 保守地不重試)。需要多於五則時請改用 <see cref="Messaging"/> 推播。
/// <see cref="ReplyAsync"/> and <see cref="ReplyTextAsync"/> can <b>send only once</b> per event: a second call
/// answers <see cref="LineErrorCodes.WebhookReplyTokenUsed"/> locally and sends nothing. A call that passes
/// the local checks and goes out counts as using the token even if LINE then rejects it — there is no way to
/// know whether the token was consumed, so it errs on the side of not trying again. For more than five
/// messages, push through <see cref="Messaging"/> instead.
/// </para>
/// </remarks>
public sealed class LineWebhookContext
{
    private int _replyAttempted;

    /// <summary>
    /// 建立一個事件的脈絡。
    /// Creates the context for one event.
    /// </summary>
    /// <param name="webhookEvent">這個脈絡所屬的事件;回覆便利方法用它的回覆權杖。The event this context belongs to; the reply conveniences use its reply token.</param>
    /// <param name="messaging">發訊用戶端。The messaging client.</param>
    /// <param name="channelName">頻道名稱;單頻道時留空字串。The channel name; an empty string for a single channel.</param>
    /// <param name="autoReplyOutcome">自動回覆的結果;沒開自動回覆或自動回覆失敗時為 <see langword="null"/>。The auto reply outcome, or <see langword="null"/> when auto reply is off or failed.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="webhookEvent"/> 或 <paramref name="messaging"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="webhookEvent"/> or <paramref name="messaging"/> is <see langword="null"/>.
    /// </exception>
    public LineWebhookContext(
        LineWebhookEvent webhookEvent,
        ILineMessagingClient messaging,
        string channelName = "",
        LineAutoReplyOutcome? autoReplyOutcome = null)
    {
        ArgumentNullException.ThrowIfNull(webhookEvent);
        ArgumentNullException.ThrowIfNull(messaging);

        Event = webhookEvent;
        Messaging = messaging;
        ChannelName = channelName ?? string.Empty;
        AutoReplyOutcome = autoReplyOutcome;
    }

    /// <summary>
    /// 這個脈絡所屬的事件。
    /// The event this context belongs to.
    /// </summary>
    public LineWebhookEvent Event { get; }

    /// <summary>
    /// 頻道名稱;單頻道時為空字串。
    /// The channel name; an empty string for a single channel.
    /// </summary>
    public string ChannelName { get; }

    /// <summary>
    /// 發訊用戶端,用來推播或做回覆以外的事。
    /// The messaging client, for pushing or anything other than replying.
    /// </summary>
    public ILineMessagingClient Messaging { get; }

    /// <summary>
    /// 自動回覆對這個事件的處理結果;沒開自動回覆,或自動回覆本身失敗時為 <see langword="null"/>。
    /// What auto reply did with this event, or <see langword="null"/> when auto reply is off or itself failed.
    /// </summary>
    public LineAutoReplyOutcome? AutoReplyOutcome { get; }

    /// <summary>
    /// 處理過程中傳遞資料用的字典,只活在這個事件的處理期間。
    /// A bag for passing data around while this event is handled; it lives only that long.
    /// </summary>
    public IDictionary<string, object?> Items { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>
    /// 以這個事件的回覆權杖回覆訊息。
    /// Replies to this event with its reply token.
    /// </summary>
    /// <param name="messages">訊息,1 到 5 則。The messages, between one and five.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>
    /// 已送出的訊息;事件沒有回覆權杖時為 <see cref="LineErrorCodes.WebhookNoReplyToken"/> 失敗,
    /// 權杖已用過時為 <see cref="LineErrorCodes.WebhookReplyTokenUsed"/> 失敗 —— 兩者都不擲例外、不送請求。
    /// The messages that were sent; a <see cref="LineErrorCodes.WebhookNoReplyToken"/> failure when the event
    /// has no reply token and a <see cref="LineErrorCodes.WebhookReplyTokenUsed"/> failure when it was already
    /// spent — neither throws and neither sends a request.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="messages"/> 為 <see langword="null"/> 時擲出。Thrown when <paramref name="messages"/> is <see langword="null"/>.</exception>
    public Task<Result<IReadOnlyList<LineSentMessage>>> ReplyAsync(
        IReadOnlyList<LineMessage> messages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        return ReplyCoreAsync(token => Messaging.ReplyAsync(token, messages, false, cancellationToken));
    }

    /// <summary>
    /// 以這個事件的回覆權杖回覆一則文字。
    /// Replies to this event with one text message.
    /// </summary>
    /// <param name="text">訊息內容。The message text.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>見 <see cref="ReplyAsync"/>。See <see cref="ReplyAsync"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> 為 <see langword="null"/> 時擲出。Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    public Task<Result<IReadOnlyList<LineSentMessage>>> ReplyTextAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        return ReplyCoreAsync(token => Messaging.ReplyTextAsync(token, text, cancellationToken));
    }

    /// <summary>
    /// 檢查權杖、標記已用,然後交給實際的送出委派。
    /// Checks the token, marks it spent, then hands over to the actual send delegate.
    /// </summary>
    /// <param name="send">以權杖送出的委派。The delegate that sends with the token.</param>
    /// <returns>送出結果或本地失敗。The send result or a local failure.</returns>
    private Task<Result<IReadOnlyList<LineSentMessage>>> ReplyCoreAsync(
        Func<string, Task<Result<IReadOnlyList<LineSentMessage>>>> send)
    {
        var token = Event.ReplyToken;
        if (string.IsNullOrEmpty(token))
        {
            return Task.FromResult(Result.Failure<IReadOnlyList<LineSentMessage>>(Error.Validation(
                LineErrorCodes.WebhookNoReplyToken,
                "這個事件沒有回覆權杖,無法回覆。This event carries no reply token, so it cannot be replied to.")));
        }

        // 先搶旗標再送:兩個並行的呼叫只有一個拿得到權杖。
        // Claim the flag before sending so that of two concurrent calls only one gets the token.
        if (Interlocked.Exchange(ref _replyAttempted, 1) != 0)
        {
            return Task.FromResult(Result.Failure<IReadOnlyList<LineSentMessage>>(Error.Validation(
                LineErrorCodes.WebhookReplyTokenUsed,
                "這個事件的回覆權杖已經用過,每個權杖只能回覆一次。This event's reply token was already used; a token can reply only once.")));
        }

        return send(token);
    }
}
