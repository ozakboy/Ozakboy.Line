namespace Ozakboy.Line.Webhook;

/// <summary>
/// <c>memberJoined</c> 與 <c>memberLeft</c> 事件的附加資訊:有哪些成員加入或離開。
/// The details of <c>memberJoined</c> and <c>memberLeft</c> events: which members joined or left.
/// </summary>
/// <remarks>
/// 在 LINE 的 JSON 裡,加入的成員放在事件的 <c>joined.members</c>,離開的放在 <c>left.members</c>;
/// 對應 <see cref="LineWebhookEvent.Joined"/> 與 <see cref="LineWebhookEvent.Left"/>。
/// In LINE's JSON, joiners sit under the event's <c>joined.members</c> and leavers under <c>left.members</c>;
/// they map to <see cref="LineWebhookEvent.Joined"/> and <see cref="LineWebhookEvent.Left"/>.
/// </remarks>
public sealed class LineWebhookMemberChange
{
    /// <summary>
    /// 成員清單;沒有時為空清單。
    /// The members; an empty list when there are none.
    /// </summary>
    public IReadOnlyList<LineWebhookMember> Members { get; init; } = [];
}
