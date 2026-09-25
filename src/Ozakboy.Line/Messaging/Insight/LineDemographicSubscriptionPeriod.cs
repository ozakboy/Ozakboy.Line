using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Insight;

/// <summary>
/// 加好友時間長短分布的一項。
/// One entry of the subscription period breakdown.
/// </summary>
/// <remarks>
/// 對應 LINE 的 <c>subscriptionPeriod</c> 與 <c>percentage</c>。
/// Maps to LINE's <c>subscriptionPeriod</c> and <c>percentage</c>.
/// </remarks>
public sealed class LineDemographicSubscriptionPeriod
{
    /// <summary>
    /// 期間:<c>within7days</c>、<c>within30days</c>、<c>within90days</c>、<c>within180days</c>、
    /// <c>within365days</c>、<c>over365days</c> 或 <c>unknown</c>。
    /// The period: <c>within7days</c>, <c>within30days</c>, <c>within90days</c>, <c>within180days</c>,
    /// <c>within365days</c>, <c>over365days</c> or <c>unknown</c>.
    /// </summary>
    [JsonPropertyName("subscriptionPeriod")]
    public string SubscriptionPeriod { get; init; } = string.Empty;

    /// <summary>
    /// 百分比。
    /// The percentage.
    /// </summary>
    [JsonPropertyName("percentage")]
    public double Percentage { get; init; }
}
