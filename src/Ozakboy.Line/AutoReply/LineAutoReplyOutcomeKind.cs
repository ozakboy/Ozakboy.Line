namespace Ozakboy.Line.AutoReply;

/// <summary>
/// 自動回覆處理完一個事件之後的結果種類。
/// What the auto reply service did with one event.
/// </summary>
public enum LineAutoReplyOutcomeKind
{
    /// <summary>
    /// 這個事件不歸自動回覆管:不是文字訊息、不是加好友,或沒有回覆權杖。
    /// The event is not the auto reply's business: not a text message, not an add-friend, or with no reply token.
    /// </summary>
    Skipped = 0,

    /// <summary>
    /// 是該管的事件,但沒有任何規則命中,也沒有備援規則。
    /// The event was one to handle, but no rule matched and there was no fallback rule either.
    /// </summary>
    NoMatch = 1,

    /// <summary>
    /// 已經回覆。
    /// A reply was sent.
    /// </summary>
    Replied = 2,
}
