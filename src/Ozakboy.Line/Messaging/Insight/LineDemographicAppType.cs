using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Insight;

/// <summary>
/// 作業系統分布的一項。
/// One entry of the OS breakdown.
/// </summary>
/// <remarks>對應 LINE 的 <c>appType</c> 與 <c>percentage</c>。Maps to LINE's <c>appType</c> and <c>percentage</c>.</remarks>
public sealed class LineDemographicAppType
{
    /// <summary>
    /// 作業系統:<c>ios</c>、<c>android</c> 或 <c>others</c>。
    /// The OS: <c>ios</c>, <c>android</c> or <c>others</c>.
    /// </summary>
    [JsonPropertyName("appType")]
    public string AppType { get; init; } = string.Empty;

    /// <summary>
    /// 百分比。
    /// The percentage.
    /// </summary>
    [JsonPropertyName("percentage")]
    public double Percentage { get; init; }
}
