using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// 成員人數端點的回應形狀,只用於反序列化。
/// The member count endpoints' response shape, used only for deserialisation.
/// </summary>
internal sealed class LineMemberCountResponse
{
    /// <summary>
    /// 成員人數。
    /// The member count.
    /// </summary>
    [JsonPropertyName("count")]
    public int Count { get; init; }
}
