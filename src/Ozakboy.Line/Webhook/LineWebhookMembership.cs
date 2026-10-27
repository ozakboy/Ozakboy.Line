namespace Ozakboy.Line.Webhook;

/// <summary>
/// <c>membership</c> 事件的附加資訊:使用者加入、退出或續訂了會員方案。
/// The details of a <c>membership</c> event: the user joined, left, or renewed a membership plan.
/// </summary>
public sealed class LineWebhookMembership
{
    /// <summary>
    /// 變動型別,見 <see cref="LineWebhookMembershipTypes"/>。
    /// The kind of change; see <see cref="LineWebhookMembershipTypes"/>.
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// 會員方案識別碼;沒有時為 <see langword="null"/>。
    /// The membership plan identifier, or <see langword="null"/> when absent.
    /// </summary>
    public long? MembershipId { get; init; }
}
