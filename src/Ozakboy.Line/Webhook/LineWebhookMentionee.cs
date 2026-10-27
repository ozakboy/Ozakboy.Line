namespace Ozakboy.Line.Webhook;

/// <summary>
/// 文字訊息裡一個被提及者。
/// One mentioned party in a text message.
/// </summary>
public sealed class LineWebhookMentionee
{
    /// <summary>
    /// 提及在文字中的位置(從 0 起算,UTF-16 字元)。
    /// The position of the mention in the text, from 0, in UTF-16 characters.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// 提及佔用的字元數。
    /// How many characters the mention occupies.
    /// </summary>
    public int Length { get; init; }

    /// <summary>
    /// 被提及者型別:<c>user</c>(某位使用者)或 <c>all</c>(所有人)。
    /// The mentionee type: <c>user</c> (one user) or <c>all</c> (everyone).
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// 被提及的使用者識別碼;提及所有人或使用者未同意授權時為 <see langword="null"/>。
    /// The mentioned user's identifier; <see langword="null"/> when mentioning everyone or when the user has not
    /// consented.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// 被提及者是不是這個官方帳號自己;沒有這個欄位時為 <see langword="null"/>。
    /// Whether the mentioned party is this official account itself, or <see langword="null"/> when the field is absent.
    /// </summary>
    /// <remarks>
    /// 群組裡的機器人通常只在被 @ 到時才回應,這個旗標就是在判斷「被 @ 的是不是我」。
    /// A bot in a group usually answers only when it is mentioned, and this flag answers "was it me that was
    /// mentioned".
    /// </remarks>
    public bool? IsSelf { get; init; }
}
