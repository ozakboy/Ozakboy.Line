using System.Text.Json.Serialization;

namespace Ozakboy.Line.Liff;

/// <summary>
/// LIFF 應用程式的檢視設定:視窗大小與載入的網頁。
/// A LIFF app's view settings: the window size and the page it loads.
/// </summary>
/// <remarks>
/// 欄位全是可空的:新增時 <see cref="Type"/> 與 <see cref="Url"/> 必填,更新時只送有值的欄位。
/// Every field is nullable: <see cref="Type"/> and <see cref="Url"/> are required when adding, while an update
/// sends only the fields that have a value.
/// </remarks>
public sealed class LineLiffView
{
    /// <summary>
    /// 視窗大小。
    /// The window size.
    /// </summary>
    [JsonPropertyName("type")]
    public LineLiffViewType? Type { get; init; }

    /// <summary>
    /// LIFF 載入的網頁位址,必須是 <c>https://</c>。
    /// The address of the page the LIFF app loads, which must be <c>https://</c>.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    /// <summary>
    /// 是否啟用模組模式(視窗底部只顯示一個動作按鈕)。
    /// Whether module mode is on, which shows only an action button at the bottom of the window.
    /// </summary>
    [JsonPropertyName("moduleMode")]
    public bool? ModuleMode { get; init; }
}
