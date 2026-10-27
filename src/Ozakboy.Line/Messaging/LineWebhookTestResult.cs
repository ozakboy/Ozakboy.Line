using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// LINE 對 webhook 位址送出測試請求的結果。
/// The outcome of the test request LINE sent to a webhook address.
/// </summary>
/// <remarks>
/// 注意:測試「送出去了」不等於這個物件的 <see cref="Success"/> 為真 —— 它代表你的伺服器回了 2xx。
/// 伺服器沒回應或回錯誤時,API 呼叫本身仍然是成功的,結果寫在這裡。
/// Note that the API call succeeding is not the same as <see cref="Success"/> being true: that means your server
/// answered 2xx. When the server does not answer, or answers with an error, the API call itself still succeeds
/// and the outcome is described here.
/// </remarks>
public sealed class LineWebhookTestResult
{
    /// <summary>
    /// 你的伺服器是否回了 2xx。
    /// Whether your server answered with a 2xx status.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    /// <summary>
    /// LINE 送出測試請求的時間(LINE 回的是 ISO 8601 字串)。
    /// When LINE sent the test request (LINE answers an ISO 8601 string).
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// 你的伺服器回的 HTTP 狀態碼;沒收到回應時,LINE 回 0 或負數。
    /// The HTTP status code your server answered with; LINE answers zero or a negative number when no response
    /// was received.
    /// </summary>
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; init; }

    /// <summary>
    /// 結果的簡短原因,例如 <c>OK</c>、<c>COULD_NOT_CONNECT</c>、<c>REQUEST_TIMEOUT</c>。
    /// A short reason for the outcome, such as <c>OK</c>, <c>COULD_NOT_CONNECT</c> or <c>REQUEST_TIMEOUT</c>.
    /// </summary>
    [JsonPropertyName("reason")]
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// 原因的補充說明。
    /// Further detail on the reason.
    /// </summary>
    [JsonPropertyName("detail")]
    public string Detail { get; init; } = string.Empty;
}
