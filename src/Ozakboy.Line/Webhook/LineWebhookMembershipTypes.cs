namespace Ozakboy.Line.Webhook;

/// <summary>
/// <c>membership</c> 事件的 <see cref="LineWebhookMembership.Type"/> 字串。
/// The <see cref="LineWebhookMembership.Type"/> strings of a <c>membership</c> event.
/// </summary>
public static class LineWebhookMembershipTypes
{
    /// <summary>加入會員方案。Joined a plan.</summary>
    public const string Joined = "joined";

    /// <summary>退出會員方案。Left a plan.</summary>
    public const string Left = "left";

    /// <summary>續訂會員方案。Renewed a plan.</summary>
    public const string Renewed = "renewed";
}
