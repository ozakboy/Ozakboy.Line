using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// 推播與回覆端點的回應形狀,只用於反序列化。
/// The response shape of the push and reply endpoints, used only for deserialisation.
/// </summary>
internal sealed class LineSentMessagesResponse
{
    /// <summary>
    /// 已送出的訊息。
    /// The messages that were sent.
    /// </summary>
    [JsonPropertyName("sentMessages")]
    public IReadOnlyList<LineSentMessage>? SentMessages { get; init; }
}
