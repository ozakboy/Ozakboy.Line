namespace Ozakboy.Line;

/// <summary>
/// 本套件回傳的錯誤代碼(postback 超長資料暫存相關)。
/// The error codes this package returns — long postback data staging related.
/// </summary>
public static partial class LineErrorCodes
{
    /// <summary>
    /// 暫存的 postback 資料找不到或已過期。兩者刻意不分:對呼叫端而言結果相同 —— 資料已經取不回來,
    /// 該請使用者重新操作一次。
    /// The staged postback data cannot be found or has expired. The two are deliberately not told apart: either
    /// way the data cannot be recovered and the user has to repeat the action.
    /// </summary>
    public const string PostbackExpired = "line.postback.expired";

    /// <summary>
    /// 要暫存的 postback 資料超過 <see cref="LineMessagingLimits.DefaultMaxStoredPostbackDataLength"/>
    /// (或 <see cref="Postback.LinePostbackDataOptions.MaxDataLength"/>),本地擋下、沒有存。
    /// The postback data to stage is longer than <see cref="LineMessagingLimits.DefaultMaxStoredPostbackDataLength"/>
    /// (or <see cref="Postback.LinePostbackDataOptions.MaxDataLength"/>); it is refused locally and not stored.
    /// </summary>
    public const string PostbackDataTooLong = "line.validation.postback_data_too_long";
}
