namespace Ozakboy.Line;

/// <summary>
/// 本套件回傳的錯誤代碼(webhook 事件回覆相關)。
/// The error codes this package returns — webhook event replies.
/// </summary>
public static partial class LineErrorCodes
{
    /// <summary>
    /// 這個事件沒有回覆權杖(例如 <c>unfollow</c>、<c>leave</c>、<c>memberLeft</c>),無法回覆。
    /// The event carries no reply token (an <c>unfollow</c>, <c>leave</c> or <c>memberLeft</c> event, for
    /// instance), so it cannot be replied to.
    /// </summary>
    public const string WebhookNoReplyToken = "line.webhook.no_reply_token";

    /// <summary>
    /// 這個事件的回覆權杖已經用過;權杖只能用一次,因此在本地就擋下、不送出請求。
    /// This event's reply token has already been spent. A token is single-use, so the call is refused locally
    /// and no request is sent.
    /// </summary>
    public const string WebhookReplyTokenUsed = "line.webhook.reply_token_used";
}
