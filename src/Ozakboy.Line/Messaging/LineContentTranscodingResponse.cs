using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// 轉檔狀態端點的回應形狀,只用於反序列化。
/// The shape of the transcoding status endpoint's response, used only for deserialisation.
/// </summary>
/// <remarks>
/// 狀態維持字串而不直接用列舉讀:遇到沒見過的值時,要回 <c>line.api.invalid_response</c> 失敗,
/// 而不是讓反序列化擲出一個看不出是哪個欄位的例外。
/// The status stays a string rather than being read straight into the enum: an unrecognised value must come back
/// as a <c>line.api.invalid_response</c> failure, not as a deserialisation exception that does not say which
/// field was wrong.
/// </remarks>
internal sealed class LineContentTranscodingResponse
{
    /// <summary>
    /// 狀態字串:<c>processing</c>、<c>succeeded</c> 或 <c>failed</c>。
    /// The status string: <c>processing</c>, <c>succeeded</c> or <c>failed</c>.
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }
}
