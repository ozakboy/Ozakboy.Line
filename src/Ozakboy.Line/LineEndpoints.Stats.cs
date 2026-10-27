namespace Ozakboy.Line;

/// <summary>
/// 投遞統計、單則訊息互動統計與統計單位的端點(<see cref="LineEndpoints"/> 的一部分)。
/// The endpoints of delivery counts, per-message engagement statistics and aggregation units (part of
/// <see cref="LineEndpoints"/>).
/// </summary>
public static partial class LineEndpoints
{
    /// <summary>
    /// 某一天的投遞數前綴:後面接 <c>reply</c>、<c>push</c>、<c>multicast</c> 或 <c>broadcast</c>(GET,<c>date=yyyyMMdd</c>)。
    /// The prefix of the per-day delivery count: <c>reply</c>, <c>push</c>, <c>multicast</c> or <c>broadcast</c>
    /// follows it (GET, <c>date=yyyyMMdd</c>).
    /// </summary>
    public const string MessageDeliveryBase = "https://api.line.me/v2/bot/message/delivery/";

    /// <summary>
    /// 單則訊息(以請求識別碼指定)的互動統計(GET,<c>requestId</c>)。
    /// The engagement statistics of one sent message, selected by request id (GET, <c>requestId</c>).
    /// </summary>
    public const string InsightMessageEvent = "https://api.line.me/v2/bot/insight/message/event";

    /// <summary>
    /// 以統計單位彙總的互動統計(GET,<c>customAggregationUnit</c>、<c>from</c>、<c>to</c>)。
    /// The engagement statistics rolled up by aggregation unit (GET, <c>customAggregationUnit</c>, <c>from</c>,
    /// <c>to</c>).
    /// </summary>
    public const string InsightMessageEventAggregation = "https://api.line.me/v2/bot/insight/message/event/aggregation";

    /// <summary>
    /// 本月已使用的統計單位數(GET)。
    /// The number of aggregation units used this month (GET).
    /// </summary>
    public const string MessageAggregationInfo = "https://api.line.me/v2/bot/message/aggregation/info";

    /// <summary>
    /// 本月使用過的統計單位名稱清單(GET,<c>limit</c>、<c>start</c>)。
    /// The names of the aggregation units used this month (GET, <c>limit</c>, <c>start</c>).
    /// </summary>
    public const string MessageAggregationList = "https://api.line.me/v2/bot/message/aggregation/list";
}
