namespace Ozakboy.Line.Webhook;

/// <summary>
/// 收到的文字訊息裡的提及(@)資訊。
/// The mention (@) information in a received text message.
/// </summary>
public sealed class LineWebhookMention
{
    /// <summary>
    /// 被提及者;沒有時為空清單。
    /// The mentioned parties; an empty list when there are none.
    /// </summary>
    public IReadOnlyList<LineWebhookMentionee> Mentionees { get; init; } = [];
}
