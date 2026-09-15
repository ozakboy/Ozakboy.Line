namespace Ozakboy.Line.AutoReply;

/// <summary>
/// 自動回覆規則的比對方式。
/// How an auto reply rule decides that it applies.
/// </summary>
/// <remarks>
/// 前四種看的是使用者傳來的文字,後兩種不看文字:<see cref="Follow"/> 對應「加好友」事件,
/// <see cref="Fallback"/> 對應「一則文字訊息,但沒有任何規則命中」。把這兩件事也做成規則,
/// 好處是宿主的規則表成為<b>唯一</b>的來源 —— 後台改歡迎訊息就是改那一條規則,
/// 不必再去翻設定檔或環境變數裡有沒有另一份。
/// The first four read the user's text and the last two do not: <see cref="Follow"/> covers the add-friend event
/// and <see cref="Fallback"/> covers "a text message that no rule matched". Expressing both as rules makes the
/// host's rule table the <b>single</b> source: changing the welcome message means editing that rule, with no
/// second copy hiding in a settings file or an environment variable.
/// </remarks>
public enum LineAutoReplyMatchMode
{
    /// <summary>
    /// 整句相同(比對前會先去除頭尾空白)。
    /// The whole message, once trimmed, is the pattern.
    /// </summary>
    Exact = 0,

    /// <summary>
    /// 訊息裡含有這段文字。
    /// The message contains the pattern.
    /// </summary>
    Contains = 1,

    /// <summary>
    /// 訊息以這段文字開頭。
    /// The message starts with the pattern.
    /// </summary>
    StartsWith = 2,

    /// <summary>
    /// 以正規表示式比對。
    /// The pattern is a regular expression.
    /// </summary>
    /// <remarks>
    /// 比對有 100 毫秒的時間上限,逾時或表示式無效一律視為「不符」而不是擲出例外 ——
    /// 一條寫壞的規則不該讓整個 webhook 回 500,那會讓 LINE 把整批事件重送。
    /// Matching is capped at 100 milliseconds, and a timeout or an invalid expression counts as "no match" rather
    /// than throwing: one badly written rule should not turn the whole webhook into a 500, which would have LINE
    /// redeliver the entire batch.
    /// </remarks>
    Regex = 3,

    /// <summary>
    /// 使用者加好友(或解除封鎖)時回覆,不看文字。
    /// Replies when the user adds the account or unblocks it; no text is involved.
    /// </summary>
    /// <remarks>
    /// 這是歡迎訊息。<see cref="LineAutoReplyRule.Pattern"/> 不使用,驗證時也不檢查。
    /// This is the welcome message. <see cref="LineAutoReplyRule.Pattern"/> is unused and is not validated.
    /// </remarks>
    Follow = 4,

    /// <summary>
    /// 收到文字訊息但沒有任何規則命中時回覆,不看文字。
    /// Replies to a text message that no other rule matched; no text is involved.
    /// </summary>
    /// <remarks>
    /// 同樣不使用 <see cref="LineAutoReplyRule.Pattern"/>。有多條時取
    /// <see cref="LineAutoReplyRule.Priority"/> 最小的那一條。
    /// <see cref="LineAutoReplyRule.Pattern"/> is unused here too. With several of them, the one with the
    /// smallest <see cref="LineAutoReplyRule.Priority"/> wins.
    /// </remarks>
    Fallback = 5,
}
