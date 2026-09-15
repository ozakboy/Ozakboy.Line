namespace Ozakboy.Line;

/// <summary>
/// LINE Messaging API 的數量上限,這些值來自 LINE 的規格而非本套件的選擇。
/// The Messaging API's size limits. These come from LINE's specification, not from this package's choices.
/// </summary>
/// <remarks>
/// 在送出之前先檢查這些上限,是為了讓錯誤發生在呼叫端的機器上而不是 LINE 的伺服器上:
/// 超量的請求在 LINE 那頭會被整批拒絕,連帶浪費一次額度,而本地檢查的錯誤訊息能直接說出「幾筆、上限幾筆」。
/// These are checked before sending so the failure happens on the caller's machine rather than LINE's: an
/// oversized request is rejected wholesale at the far end, costing a slice of quota on the way, whereas a local
/// check can say exactly how many were supplied and how many are allowed.
/// </remarks>
public static class LineMessagingLimits
{
    /// <summary>
    /// 單次 multicast 的收件者人數上限。
    /// The maximum number of recipients in a single multicast.
    /// </summary>
    public const int MulticastRecipients = 500;

    /// <summary>
    /// 單次請求的訊息則數下限。
    /// The minimum number of messages in one request.
    /// </summary>
    public const int MinMessagesPerRequest = 1;

    /// <summary>
    /// 單次請求的訊息則數上限。
    /// The maximum number of messages in one request.
    /// </summary>
    public const int MaxMessagesPerRequest = 5;
}
