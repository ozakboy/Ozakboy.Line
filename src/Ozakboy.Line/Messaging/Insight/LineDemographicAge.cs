using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Insight;

/// <summary>
/// 年齡分布的一項。
/// One entry of the age breakdown.
/// </summary>
/// <remarks>對應 LINE 的 <c>age</c> 與 <c>percentage</c>。Maps to LINE's <c>age</c> and <c>percentage</c>.</remarks>
public sealed class LineDemographicAge
{
    /// <summary>
    /// 年齡層:<c>from0to14</c>、<c>from15to19</c>、…、<c>from50</c> 或 <c>unknown</c>。
    /// The age band: <c>from0to14</c>, <c>from15to19</c>, …, <c>from50</c> or <c>unknown</c>.
    /// </summary>
    [JsonPropertyName("age")]
    public string Age { get; init; } = string.Empty;

    /// <summary>
    /// 百分比。
    /// The percentage.
    /// </summary>
    [JsonPropertyName("percentage")]
    public double Percentage { get; init; }
}
