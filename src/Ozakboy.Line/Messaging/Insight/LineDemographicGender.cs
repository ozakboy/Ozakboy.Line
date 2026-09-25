using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Insight;

/// <summary>
/// 性別分布的一項。
/// One entry of the gender breakdown.
/// </summary>
/// <remarks>對應 LINE 的 <c>gender</c> 與 <c>percentage</c>。Maps to LINE's <c>gender</c> and <c>percentage</c>.</remarks>
public sealed class LineDemographicGender
{
    /// <summary>
    /// 性別:<c>male</c>、<c>female</c> 或 <c>unknown</c>。
    /// The gender: <c>male</c>, <c>female</c> or <c>unknown</c>.
    /// </summary>
    [JsonPropertyName("gender")]
    public string Gender { get; init; } = string.Empty;

    /// <summary>
    /// 百分比。
    /// The percentage.
    /// </summary>
    [JsonPropertyName("percentage")]
    public double Percentage { get; init; }
}
