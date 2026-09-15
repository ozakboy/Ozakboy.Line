namespace Ozakboy.Line.AutoReply;

/// <summary>
/// 自動回覆的設定。
/// The auto reply settings.
/// </summary>
/// <remarks>
/// 這裡只有一個開關,而且是刻意的。歡迎訊息與「聽不懂時的回覆」都不放在設定裡,
/// 而是以 <see cref="LineAutoReplyMatchMode.Follow"/> 與 <see cref="LineAutoReplyMatchMode.Fallback"/>
/// 兩種規則表達 —— 這樣規則儲存體就是<b>唯一</b>的來源,後台改文案就是 upsert 一條規則。
/// 把文案放進設定的代價是它會被 bake 進部署:改一句話要重新發版,而且線上還會有一份與規則表不一致的內容。
/// One switch, on purpose. Neither the welcome message nor the "I did not understand that" reply lives in
/// settings; both are rules, with <see cref="LineAutoReplyMatchMode.Follow"/> and
/// <see cref="LineAutoReplyMatchMode.Fallback"/>. That leaves the rule store as the <b>single</b> source, and
/// changing the wording from an admin page is an upsert. Wording in settings gets baked into a deployment
/// instead: changing a sentence means a release, and a second copy of the text ends up disagreeing with the rule
/// table.
/// </remarks>
public sealed class LineAutoReplyOptions
{
    /// <summary>
    /// 是否啟用自動回覆,預設為 <see langword="true"/>。
    /// Whether auto reply is on; <see langword="true"/> by default.
    /// </summary>
    /// <remarks>
    /// 設為 <see langword="false"/> 時,
    /// <see cref="ILineAutoReplyService.HandleAsync"/> 一律回 <see cref="LineAutoReplyOutcome.Skipped"/>,
    /// 一個請求都不會送出。這是「出事時先把機器人閉嘴」的那個開關 —— 不必動規則,也不必重新部署端點。
    /// With <see langword="false"/>, <see cref="ILineAutoReplyService.HandleAsync"/> always answers
    /// <see cref="LineAutoReplyOutcome.Skipped"/> and sends nothing. This is the switch for keeping the bot quiet
    /// when something has gone wrong, without touching the rules or redeploying the endpoint.
    /// </remarks>
    public bool Enabled { get; set; } = true;
}
