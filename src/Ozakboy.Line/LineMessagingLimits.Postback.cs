namespace Ozakboy.Line;

/// <summary>
/// postback 超長資料暫存的上限(<see cref="LineMessagingLimits"/> 的一部分)。
/// The limits of long postback data staging (part of <see cref="LineMessagingLimits"/>).
/// </summary>
public static partial class LineMessagingLimits
{
    /// <summary>
    /// 暫存的 postback 資料預設最多幾個字元(LINE 自己的 <c>data</c> 上限是
    /// <see cref="MaxPostbackDataLength"/>,暫存是為了繞過那個上限,但仍需要一道防止濫用的邊界)。
    /// The default maximum number of characters of staged postback data. LINE's own <c>data</c> limit is
    /// <see cref="MaxPostbackDataLength"/>; staging exists to get around it, yet still needs a bound against abuse.
    /// </summary>
    public const int DefaultMaxStoredPostbackDataLength = 10_000;

    /// <summary>
    /// 暫存代碼的固定長度:<c>ozl:</c> 加 32 個十六進位字元。必須小於 <see cref="MaxPostbackDataLength"/>。
    /// The fixed length of a staging token: <c>ozl:</c> plus 32 hex characters. It must stay under
    /// <see cref="MaxPostbackDataLength"/>.
    /// </summary>
    public const int PostbackDataTokenLength = 36;
}
