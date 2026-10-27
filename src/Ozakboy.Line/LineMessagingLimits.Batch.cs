namespace Ozakboy.Line;

/// <summary>
/// 統計、圖文選單批次作業的上限(<see cref="LineMessagingLimits"/> 的一部分)。
/// The limits of statistics and rich menu batch operations (part of <see cref="LineMessagingLimits"/>).
/// </summary>
public static partial class LineMessagingLimits
{
    /// <summary>
    /// 一次圖文選單批次作業最多的作業數。
    /// The maximum number of operations in one rich menu batch.
    /// </summary>
    public const int MaxRichMenuBatchOperations = 1000;

    /// <summary>
    /// 以統計單位彙總時,迄日與起日最多相隔幾天。
    /// The most days the end date may lie after the start date when rolling up by aggregation unit.
    /// </summary>
    public const int MaxAggregationRangeDays = 30;

    /// <summary>
    /// 一次請求最多帶幾個統計單位(LINE 目前只允許 1 個)。
    /// The most aggregation units one request may carry (LINE currently allows exactly one).
    /// </summary>
    public const int MaxAggregationUnitsPerRequest = 1;

    /// <summary>
    /// 統計單位名稱的長度上限。
    /// The maximum length of an aggregation unit's name.
    /// </summary>
    public const int MaxAggregationUnitNameLength = 30;

    /// <summary>
    /// 統計單位名稱清單每頁最多的筆數。
    /// The most entries per page of the aggregation unit name list.
    /// </summary>
    public const int MaxAggregationUnitNamesPerPage = 100;
}
