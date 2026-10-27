using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// 簽發帳號連結權杖的回應形狀,只用於反序列化。
/// The shape of the response to issuing an account link token, used only for deserialisation.
/// </summary>
internal sealed class LineLinkTokenResponse
{
    /// <summary>
    /// 連結權杖。
    /// The link token.
    /// </summary>
    [JsonPropertyName("linkToken")]
    public string? LinkToken { get; init; }
}
