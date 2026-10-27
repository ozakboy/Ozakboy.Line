using System.Text.Json.Serialization;

namespace Ozakboy.Line.Liff;

/// <summary>
/// 列出 LIFF 應用程式的回應形狀,只用於反序列化。
/// The shape of the response listing LIFF apps, used only for deserialisation.
/// </summary>
internal sealed class LineLiffListResponse
{
    /// <summary>
    /// 應用程式清單。
    /// The apps.
    /// </summary>
    [JsonPropertyName("apps")]
    public List<LineLiffApp>? Apps { get; init; }
}

/// <summary>
/// 新增 LIFF 應用程式的回應形狀,只用於反序列化。
/// The shape of the response to adding a LIFF app, used only for deserialisation.
/// </summary>
internal sealed class LineLiffAddResponse
{
    /// <summary>
    /// 新應用程式的 LIFF ID。
    /// The new app's LIFF ID.
    /// </summary>
    [JsonPropertyName("liffId")]
    public string? LiffId { get; init; }
}
