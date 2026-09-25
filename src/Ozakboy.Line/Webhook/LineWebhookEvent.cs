using System.Text.Json;

namespace Ozakboy.Line.Webhook;

/// <summary>
/// 一個 webhook 事件。
/// One webhook event.
/// </summary>
public sealed class LineWebhookEvent
{
    /// <summary>
    /// 事件型別,見 <see cref="LineWebhookEventTypes"/>。
    /// The event type; see <see cref="LineWebhookEventTypes"/>.
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// 處理模式:<c>active</c> 表示可以回應,<c>standby</c> 表示這個事件由別的模組處理。
    /// The mode: <c>active</c> means it can be answered, <c>standby</c> means another module owns it.
    /// </summary>
    /// <remarks>
    /// <c>standby</c> 的事件<b>不要回覆</b>。它出現在有 LINE 模組(例如人工客服介面)接手對話的時候,
    /// 這時回覆會與真人搶著講話。
    /// A <c>standby</c> event should <b>not be answered</b>. It appears when a LINE module — a human operator
    /// console, say — has taken the conversation, and replying then means talking over a person.
    /// </remarks>
    public string Mode { get; init; } = string.Empty;

    /// <summary>
    /// 事件發生時間。
    /// When the event happened.
    /// </summary>
    /// <remarks>
    /// LINE 送的是 Unix <b>毫秒</b>,這裡已經轉好。當成秒去解會得到 1970 年附近的時間,
    /// 而那種錯誤在畫面上通常只表現為「時間怪怪的」。
    /// LINE sends Unix <b>milliseconds</b>, already converted here. Reading them as seconds lands near 1970, an
    /// error that usually shows up on screen as nothing more specific than a time that looks wrong.
    /// </remarks>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// 事件識別碼,同一個事件重送時不變。
    /// The event identifier, which stays the same when an event is redelivered.
    /// </summary>
    public string WebhookEventId { get; init; } = string.Empty;

    /// <summary>
    /// 這是一次重送。
    /// This is a redelivery.
    /// </summary>
    /// <remarks>
    /// LINE 在先前那次沒有及時收到 200 時會重送。處理常式若有副作用(記帳、發訊、扣點),
    /// 必須以 <see cref="WebhookEventId"/> 去重 —— 不去重的代價是同一件事做兩次。
    /// LINE redelivers when it did not get a 200 in time. A handler with side effects — recording, sending,
    /// deducting — must deduplicate on <see cref="WebhookEventId"/>, or it does the same thing twice.
    /// </remarks>
    public bool IsRedelivery { get; init; }

    /// <summary>
    /// 回覆權杖;不是所有事件都有。
    /// The reply token; not every event carries one.
    /// </summary>
    /// <remarks>
    /// 只能用一次,而且效期很短(LINE 標示為數十秒)。重送的事件帶的權杖多半已經失效。
    /// It is single-use and short-lived — LINE documents it in tens of seconds — and the token on a redelivered
    /// event has usually expired already.
    /// </remarks>
    public string? ReplyToken { get; init; }

    /// <summary>
    /// 事件來源。
    /// The event's source.
    /// </summary>
    public LineWebhookSource Source { get; init; } = new();

    /// <summary>
    /// 訊息內容;只有 <c>message</c> 事件有。
    /// The message, present only on a <c>message</c> event.
    /// </summary>
    public LineWebhookMessage? Message { get; init; }

    /// <summary>
    /// 回傳內容;只有 <c>postback</c> 事件有。
    /// The postback, present only on a <c>postback</c> event.
    /// </summary>
    public LineWebhookPostback? Postback { get; init; }

    /// <summary>
    /// 加好友的附加資訊;只有 <c>follow</c> 事件有。
    /// The follow details, present only on a <c>follow</c> event.
    /// </summary>
    public LineWebhookFollow? Follow { get; init; }

    /// <summary>
    /// 看完影片的附加資訊;只有 <c>videoPlayComplete</c> 事件有。
    /// The video completion details, present only on a <c>videoPlayComplete</c> event.
    /// </summary>
    public LineWebhookVideoPlayComplete? VideoPlayComplete { get; init; }

    /// <summary>
    /// 這個事件的原始 JSON。
    /// This event's original JSON.
    /// </summary>
    /// <remarks>
    /// 本套件沒有建模的欄位(beacon、things、accountLink 的內容,以及 LINE 之後新增的任何東西)
    /// 都在這裡。有這一份,套件跟不上 LINE 的腳步就不會變成「收得到但拿不到」。
    /// Everything this package does not model — the contents of beacon, things, and accountLink events, and
    /// whatever LINE adds later — is here. With it, a package that has not caught up with LINE never turns into
    /// "the event arrives but the data is out of reach".
    /// </remarks>
    public JsonElement Raw { get; init; }
}
