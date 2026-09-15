namespace Ozakboy.Line.Webhook;

/// <summary>
/// 加好友事件的附加資訊。
/// The extra information on a follow event.
/// </summary>
public sealed class LineWebhookFollow
{
    /// <summary>
    /// 這次是「解除封鎖」而不是第一次加好友。
    /// This was an unblock rather than a first-time follow.
    /// </summary>
    /// <remarks>
    /// 兩者都送 <c>follow</c> 事件,只差這個旗標。不看它就把每個 follow 都當新朋友,
    /// 結果是每次解除封鎖都重發一次歡迎訊息與新戶好禮。
    /// Both arrive as a <c>follow</c> event and this flag is the only difference. Ignoring it and treating every
    /// follow as a new friend means resending the welcome message, and any new-member offer, on every unblock.
    /// </remarks>
    public bool IsUnblocked { get; init; }
}
