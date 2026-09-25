using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Insight;

/// <summary>
/// 地區分布的一項。
/// One entry of the area breakdown.
/// </summary>
/// <remarks>對應 LINE 的 <c>area</c> 與 <c>percentage</c>。Maps to LINE's <c>area</c> and <c>percentage</c>.</remarks>
public sealed class LineDemographicArea
{
    /// <summary>
    /// 地區名稱,依帳號所在國家以當地語言呈現(台灣是縣市名),或 <c>unknown</c>。
    /// The area name, in the local language of the account's country (Taiwanese counties and cities), or
    /// <c>unknown</c>.
    /// </summary>
    [JsonPropertyName("area")]
    public string Area { get; init; } = string.Empty;

    /// <summary>
    /// 百分比。
    /// The percentage.
    /// </summary>
    [JsonPropertyName("percentage")]
    public double Percentage { get; init; }
}
