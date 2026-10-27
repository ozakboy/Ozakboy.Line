namespace Ozakboy.Line.Webhook;

/// <summary>
/// 群組或聊天室成員變動事件裡的一位成員。
/// One member in a group or room membership-change event.
/// </summary>
public sealed class LineWebhookMember
{
    /// <summary>
    /// 成員型別;目前 LINE 只會送 <c>user</c>。
    /// The member type; for now LINE sends only <c>user</c>.
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// 成員的使用者識別碼;沒有時為 <see langword="null"/>。
    /// The member's user identifier, or <see langword="null"/> when absent.
    /// </summary>
    public string? UserId { get; init; }
}
