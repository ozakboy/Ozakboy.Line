namespace Ozakboy.Line.AutoReply;

/// <summary>
/// 自動回覆處理完一個事件之後的結果。
/// The result of the auto reply service handling one event.
/// </summary>
/// <remarks>
/// 宿主的 webhook 處理常式拿得到這個結果(見
/// <c>Ozakboy.Line.AspNetCore</c> 的 <c>LineWebhookItems.AutoReplyOutcome</c>),
/// 因此可以決定「自動回覆已經處理過了,我就不再回一次」——
/// 少了這個資訊,宿主只能重做一次判斷,而兩邊的判斷遲早會不一致。
/// A host's webhook handler can read this outcome (see <c>LineWebhookItems.AutoReplyOutcome</c> in
/// <c>Ozakboy.Line.AspNetCore</c>) and decide not to answer again when the auto reply already has. Without it a
/// host has to repeat the decision, and two copies of a decision drift apart sooner or later.
/// </remarks>
public sealed class LineAutoReplyOutcome
{
    /// <summary>
    /// 建立結果。
    /// Creates an outcome.
    /// </summary>
    /// <param name="kind">結果種類。The kind.</param>
    /// <param name="ruleId">命中的規則識別碼。The matching rule's identifier.</param>
    private LineAutoReplyOutcome(LineAutoReplyOutcomeKind kind, string? ruleId)
    {
        Kind = kind;
        RuleId = ruleId;
    }

    /// <summary>
    /// 「不歸我管」的結果。
    /// The "not my business" outcome.
    /// </summary>
    public static LineAutoReplyOutcome Skipped { get; } = new(LineAutoReplyOutcomeKind.Skipped, ruleId: null);

    /// <summary>
    /// 「沒有規則命中」的結果。
    /// The "no rule matched" outcome.
    /// </summary>
    public static LineAutoReplyOutcome NoMatch { get; } = new(LineAutoReplyOutcomeKind.NoMatch, ruleId: null);

    /// <summary>
    /// 結果種類。
    /// The kind.
    /// </summary>
    public LineAutoReplyOutcomeKind Kind { get; }

    /// <summary>
    /// 命中的規則識別碼;沒有命中時為 <see langword="null"/>。
    /// The matching rule's identifier, or <see langword="null"/> when nothing matched.
    /// </summary>
    public string? RuleId { get; }

    /// <summary>
    /// 建立「已回覆」的結果。
    /// Creates a "replied" outcome.
    /// </summary>
    /// <param name="ruleId">命中的規則識別碼。The matching rule's identifier.</param>
    /// <returns>結果。The outcome.</returns>
    public static LineAutoReplyOutcome Replied(string? ruleId) => new(LineAutoReplyOutcomeKind.Replied, ruleId);
}
