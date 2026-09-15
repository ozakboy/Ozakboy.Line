using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Webhook;

namespace Ozakboy.Line.AutoReply;

/// <summary>
/// 對一個 webhook 事件套用自動回覆規則。
/// Applies the auto reply rules to one webhook event.
/// </summary>
public interface ILineAutoReplyService
{
    /// <summary>
    /// 處理一個事件:該回就回,不該回就說明為什麼不回。
    /// Handles one event: answers when it should, and says why when it does not.
    /// </summary>
    /// <param name="webhookEvent">事件。The event.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>
    /// 處理結果;規則渲染失敗或 LINE 回覆失敗時為失敗的 <see cref="Result{T}"/>。
    /// The outcome, or a failed <see cref="Result{T}"/> when a rule would not render or LINE refused the reply.
    /// </returns>
    /// <remarks>
    /// 「沒有規則命中」是<b>成功</b>(<see cref="LineAutoReplyOutcomeKind.NoMatch"/>)而不是失敗:
    /// 使用者講了一句沒有規則對應的話,那是日常,不是錯誤。真正的失敗只有兩種 ——
    /// 規則本身渲染不出來,以及 LINE 拒絕了這次回覆。
    /// "Nothing matched" is a <b>success</b> — <see cref="LineAutoReplyOutcomeKind.NoMatch"/> — rather than a
    /// failure: a user saying something no rule covers is an ordinary day, not an error. Only two things are real
    /// failures: a rule that will not render, and a reply LINE refused.
    /// </remarks>
    Task<Result<LineAutoReplyOutcome>> HandleAsync(LineWebhookEvent webhookEvent, CancellationToken cancellationToken = default);
}
