namespace Ozakboy.Line.Webhook;

/// <summary>
/// <c>unsend</c> 事件的附加資訊:使用者收回了哪一則訊息。
/// The details of an <c>unsend</c> event: which message the user took back.
/// </summary>
public sealed class LineWebhookUnsend
{
    /// <summary>
    /// 被收回的訊息識別碼。
    /// The identifier of the unsent message.
    /// </summary>
    /// <remarks>
    /// 收到這個事件時,應刪除或隱藏自己保存的那則訊息 —— 使用者收回是在表達「我不想讓這段內容留著」。
    /// On receiving this, delete or hide the message you stored: unsending is the user saying they do not want
    /// that content to stay around.
    /// </remarks>
    public string MessageId { get; init; } = string.Empty;
}
