namespace Ozakboy.Line.Mcp.Outbox;

/// <summary>
/// 待發項目目前的狀態。
/// A queued item's current status.
/// </summary>
/// <remarks>
/// 狀態只往前走:<see cref="PendingReview"/> 之後只能是核准後的三種結果之一,或被退回 / 取消,
/// 而已經送出的項目不會回到待審。這是刻意的 —— 一個能回到待審的已送項目,
/// 意味著同一則訊息可能被核准兩次。
/// Statuses only move forward: from <see cref="PendingReview"/> an item reaches one of the post-approval states,
/// or is rejected or cancelled, and something already sent never returns to review. That is deliberate: an item
/// that could go back to review is one that could be approved twice.
/// </remarks>
public enum LineMcpOutboxStatus
{
    /// <summary>
    /// 等待宿主人工核准。
    /// Waiting for a person on the host's side to approve it.
    /// </summary>
    PendingReview = 0,

    /// <summary>
    /// 已核准但還沒送出。
    /// Approved, but not sent yet.
    /// </summary>
    /// <remarks>
    /// 本套件的核准與送出是同一個動作(<see cref="ILineMcpOutboxService.ApproveAndSendAsync"/>),
    /// 所以這個狀態多半只是過渡。保留它是給「核准與送出分開、中間排隊」的宿主用的。
    /// In this package approving and sending are one action
    /// (<see cref="ILineMcpOutboxService.ApproveAndSendAsync"/>), so this status is mostly transitional. It is
    /// kept for hosts that separate the two and queue in between.
    /// </remarks>
    Approved = 1,

    /// <summary>
    /// 已送出。
    /// Sent.
    /// </summary>
    Sent = 2,

    /// <summary>
    /// 送出失敗,原因在 <see cref="LineMcpOutboxItem.Error"/>。
    /// Sending failed; the reason is in <see cref="LineMcpOutboxItem.Error"/>.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// 被審核的人退回。
    /// Rejected by whoever reviewed it.
    /// </summary>
    Rejected = 4,

    /// <summary>
    /// 被建立它的一方取消。
    /// Cancelled by the side that created it.
    /// </summary>
    /// <remarks>
    /// 與 <see cref="Rejected"/> 分開,是因為兩者要回答的問題不同:「AI 自己改變主意的比例」與
    /// 「人工退回的比例」合在一起就看不出 AI 的提案品質。
    /// Kept apart from <see cref="Rejected"/> because they answer different questions: how often the AI changed
    /// its own mind and how often a person refused it, merged into one number, say nothing about the quality of
    /// what the AI proposed.
    /// </remarks>
    Canceled = 5,
}
