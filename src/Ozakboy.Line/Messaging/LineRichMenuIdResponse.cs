using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// 只回一個圖文選單識別碼的端點的回應形狀,只用於反序列化。
/// The shape of responses carrying nothing but a rich menu identifier, used only for deserialisation.
/// </summary>
internal sealed class LineRichMenuIdResponse
{
    /// <summary>
    /// 圖文選單識別碼。
    /// The rich menu identifier.
    /// </summary>
    [JsonPropertyName("richMenuId")]
    public string? RichMenuId { get; init; }
}
