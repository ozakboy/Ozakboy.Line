using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// 頻道目前設定的 webhook 位址與啟用狀態。
/// The webhook address currently set on the channel, and whether it is in use.
/// </summary>
public sealed class LineWebhookEndpointInfo
{
    /// <summary>
    /// webhook 位址。
    /// The webhook address.
    /// </summary>
    [JsonPropertyName("endpoint")]
    public string Endpoint { get; init; } = string.Empty;

    /// <summary>
    /// 是否啟用「使用 webhook」。為 <see langword="false"/> 時,LINE 不會送出任何事件。
    /// Whether "Use webhook" is switched on. When <see langword="false"/> LINE sends no events at all.
    /// </summary>
    [JsonPropertyName("active")]
    public bool Active { get; init; }
}
