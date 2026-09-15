using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// 額度使用量端點的回應形狀,只用於反序列化。
/// The quota consumption endpoint's response shape, used only for deserialisation.
/// </summary>
internal sealed class LineQuotaConsumptionResponse
{
    /// <summary>
    /// 本月已使用的則數。
    /// The number of messages already sent this month.
    /// </summary>
    [JsonPropertyName("totalUsage")]
    public long TotalUsage { get; init; }
}
