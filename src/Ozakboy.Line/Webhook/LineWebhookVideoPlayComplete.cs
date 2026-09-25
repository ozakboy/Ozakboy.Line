namespace Ozakboy.Line.Webhook;

/// <summary>
/// <c>videoPlayComplete</c> 事件的附加資訊:使用者看完的是哪一支影片。
/// The details of a <c>videoPlayComplete</c> event: which video the user finished watching.
/// </summary>
/// <remarks>
/// 欄位對應 LINE 的 <c>videoPlayComplete.trackingId</c>,值就是送影片時填在
/// <see cref="Ozakboy.Line.Messaging.Messages.VideoMessage.TrackingId"/> 的那一個。沒有填追蹤識別碼的影片
/// 不會產生這個事件,所以「想知道有沒有看完」得在送出時就決定。
/// The field maps to LINE's <c>videoPlayComplete.trackingId</c>, and the value is whatever was put in
/// <see cref="Ozakboy.Line.Messaging.Messages.VideoMessage.TrackingId"/> when the video was sent. A video sent
/// without a tracking id produces no such event, so wanting to know whether it was watched is a decision made at
/// send time.
/// </remarks>
public sealed class LineWebhookVideoPlayComplete
{
    /// <summary>
    /// 送影片時填的追蹤識別碼。
    /// The tracking id given when the video was sent.
    /// </summary>
    public string TrackingId { get; init; } = string.Empty;
}
