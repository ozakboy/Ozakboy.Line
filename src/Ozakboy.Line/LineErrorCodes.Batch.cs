namespace Ozakboy.Line;

/// <summary>
/// 統計、批次與分批發送新增的錯誤代碼(<see cref="LineErrorCodes"/> 的一部分)。
/// The error codes added for statistics, batch operations and bulk sending (part of
/// <see cref="LineErrorCodes"/>).
/// </summary>
public static partial class LineErrorCodes
{
    /// <summary>
    /// 統計的日期區間不合法:起日晚於迄日,或區間超過上限。
    /// The statistics date range is invalid: the start is after the end, or the range exceeds the limit.
    /// </summary>
    public const string InvalidDateRange = "line.validation.invalid_date_range";

    /// <summary>
    /// 統計單位不合法:數量超過上限、名稱為空、過長或含有英數與底線以外的字元。
    /// The aggregation unit is invalid: too many of them, or a name that is empty, too long, or carries
    /// characters other than letters, digits and underscores.
    /// </summary>
    public const string InvalidAggregationUnit = "line.validation.invalid_aggregation_unit";

    /// <summary>
    /// 圖文選單批次作業不合法:作業數不在 1 到上限之間,或其中有空值。
    /// The rich menu batch is invalid: the operation count is outside 1 to the limit, or an entry is null.
    /// </summary>
    public const string InvalidRichMenuBatch = "line.validation.invalid_rich_menu_batch";

    /// <summary>
    /// 沒有任何收件人(分批群發至少需要一位)。
    /// There is no recipient (a bulk multicast needs at least one).
    /// </summary>
    public const string NoRecipients = "line.validation.no_recipients";
}
