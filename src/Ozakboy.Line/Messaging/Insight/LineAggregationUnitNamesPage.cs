using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Insight;

/// <summary>
/// 一頁本月使用過的統計單位名稱。
/// One page of the aggregation unit names used this month.
/// </summary>
/// <remarks>
/// 對應 LINE 的 <c>customAggregationUnits</c> 與 <c>next</c>。<see cref="Next"/> 有值代表還有下一頁,
/// 把它當作下一次呼叫的 <c>start</c>。
/// Maps to LINE's <c>customAggregationUnits</c> and <c>next</c>. A non-null <see cref="Next"/> means another page
/// follows and is passed as the next call's <c>start</c>.
/// </remarks>
public sealed class LineAggregationUnitNamesPage
{
    /// <summary>
    /// 統計單位名稱。
    /// The aggregation unit names.
    /// </summary>
    [JsonPropertyName("customAggregationUnits")]
    public IReadOnlyList<string> CustomAggregationUnits { get; init; } = [];

    /// <summary>
    /// 下一頁的起點;沒有下一頁時為 <see langword="null"/>。
    /// The start of the next page, or <see langword="null"/> when this is the last one.
    /// </summary>
    [JsonPropertyName("next")]
    public string? Next { get; init; }
}
