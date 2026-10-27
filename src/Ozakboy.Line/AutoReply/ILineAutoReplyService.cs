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

    /// <summary>
    /// 處理指定 channel 收到的一個事件:只比對適用於該 channel 的規則,並用該 channel 的用戶端回覆。
    /// Handles an event received on a given channel: only the rules that apply to that channel are matched, and
    /// the reply goes out through that channel's client.
    /// </summary>
    /// <param name="webhookEvent">事件。The event.</param>
    /// <param name="channelName">channel 名稱;空字串是預設 channel。The channel name; the empty string is the default channel.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>
    /// 同 <see cref="HandleAsync(LineWebhookEvent, CancellationToken)"/>;名稱未註冊為
    /// <see cref="LineErrorCodes.NotConfigured"/> 失敗。
    /// As <see cref="HandleAsync(LineWebhookEvent, CancellationToken)"/>; an unregistered name is a
    /// <see cref="LineErrorCodes.NotConfigured"/> failure.
    /// </returns>
    /// <remarks>
    /// 預設實作只認得預設 channel(空字串轉給單參數多載),其他名稱回 <see cref="LineErrorCodes.NotConfigured"/> 失敗 ——
    /// 讓 0.4.0 之前自行實作此介面的宿主不必修改也能編譯,同時不會把別的 channel 的事件悄悄當成預設 channel 處理。
    /// The default implementation knows only the default channel (the empty string goes to the other overload)
    /// and fails other names with <see cref="LineErrorCodes.NotConfigured"/>: a host that implemented this
    /// interface before 0.4.0 still compiles, and an event from another channel is never quietly handled as the
    /// default channel's.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// 任一參數為 <see langword="null"/> 時擲出。
    /// Thrown when a reference argument is <see langword="null"/>.
    /// </exception>
    Task<Result<LineAutoReplyOutcome>> HandleAsync(
        LineWebhookEvent webhookEvent,
        string channelName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(webhookEvent);
        ArgumentNullException.ThrowIfNull(channelName);

        return channelName.Length == 0
            ? HandleAsync(webhookEvent, cancellationToken)
            : Task.FromResult(Result.Failure<LineAutoReplyOutcome>(Error.Validation(
                LineErrorCodes.NotConfigured,
                $"這個自動回覆實作只支援預設 channel,不認得 \"{channelName}\"。This auto reply implementation supports only the default channel and does not know \"{channelName}\".")));
    }
}
