using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Insight;

/// <summary>
/// 統計單位用量端點的回應。
/// The response of the aggregation unit usage endpoint.
/// </summary>
internal sealed class LineAggregationUnitUsageResponse
{
    /// <summary>
    /// 本月已使用的統計單位數。
    /// The number of aggregation units used this month.
    /// </summary>
    [JsonPropertyName("numOfCustomAggregationUnits")]
    public int NumOfCustomAggregationUnits { get; init; }
}
