using System.Globalization;
using System.Text;
using System.Text.Json;
using Ozakboy.Core.Abstractions;
using Ozakboy.Http;

namespace Ozakboy.Line;

/// <summary>
/// 把 <c>Ozakboy.Http</c> 回報的狀態碼錯誤,加工成帶有 LINE 錯誤內容的錯誤。
/// Rewrites a status-code failure from <c>Ozakboy.Http</c> into one carrying LINE's own error body.
/// </summary>
/// <remarks>
/// <para>
/// 加工而不是重建:<c>Ozakboy.Http</c> 判定的<b>分類</b>與它放進去的<b>資料</b>全部保留。分類決定了下游
/// 「這個失敗值不值得重試」,404 必須還是 NotFound、429 必須還是 RateLimited;在這裡另訂一套分類,
/// 等於讓同一件事有兩個真相來源。
/// It rewrites rather than rebuilds: the <b>category</b> assigned by <c>Ozakboy.Http</c> and the <b>data</b> it
/// attached both survive. The category is what tells downstream code whether a failure is worth retrying, so a
/// 404 must stay NotFound and a 429 must stay RateLimited; assigning categories again here would give one fact
/// two sources of truth.
/// </para>
/// <para>
/// LINE 的錯誤內容形狀是 <c>{"message": "...", "details": [{"message": "...", "property": "..."}]}</c>。
/// <c>details</c> 才是真正說得出「哪個欄位錯了」的部分,而 <c>message</c> 常常只是
/// <c>The request body has 1 error(s)</c> —— 少了 details,除錯等於從頭猜起。
/// LINE's error body has the shape <c>{"message": "...", "details": [{"message": "...", "property": "..."}]}</c>.
/// The <c>details</c> array is the part that can actually name the offending field, while <c>message</c> is
/// often no more than <c>The request body has 1 error(s)</c>; without details, debugging starts from a guess.
/// </para>
/// </remarks>
internal static class LineApiErrorMapper
{
    /// <summary>
    /// 加工錯誤。非狀態碼錯誤(連線失敗、逾時、取消)原封不動回傳。
    /// Rewrites the error. A failure that is not a status code — a transport failure, a timeout, a cancellation —
    /// is returned untouched.
    /// </summary>
    /// <param name="httpError">
    /// <c>Ozakboy.Http</c> 回報的錯誤。The failure reported by <c>Ozakboy.Http</c>.
    /// </param>
    /// <returns>加工後的錯誤。The rewritten error.</returns>
    internal static Error Map(Error httpError)
    {
        ArgumentNullException.ThrowIfNull(httpError);

        if (!httpError.Code.StartsWith(HttpErrorCodes.StatusPrefix, StringComparison.Ordinal))
        {
            return httpError;
        }

        var status = httpError.TryGetInt64(HttpErrorDataKeys.StatusCode, out var parsed)
            ? parsed.ToString(CultureInfo.InvariantCulture)
            : httpError.Code[HttpErrorCodes.StatusPrefix.Length..];

        httpError.TryGetData(HttpErrorDataKeys.Body, out var body);
        var (lineMessage, lineDetails) = ParseErrorBody(body);

        var message = string.Create(
            CultureInfo.InvariantCulture,
            $"LINE API {status}: {lineMessage ?? httpError.Message}");

        // 原有的資料整份搬過來再補上 LINE 的兩個鍵:statusCode / body / retryAfterSeconds 對呼叫端仍然有用,
        // 尤其 retryAfterSeconds 在 429 時是唯一能決定「等多久再送」的依據。
        // The existing data is carried over whole before LINE's two keys are added: statusCode, body, and
        // retryAfterSeconds remain useful to the caller, and on a 429 retryAfterSeconds is the only thing that
        // says how long to wait.
        var data = new Dictionary<string, string>(StringComparer.Ordinal);
        if (httpError.Data is not null)
        {
            foreach (var entry in httpError.Data)
            {
                data[entry.Key] = entry.Value;
            }
        }

        if (lineMessage is not null)
        {
            data[LineErrorDataKeys.LineMessage] = lineMessage;
        }

        if (lineDetails is not null)
        {
            data[LineErrorDataKeys.LineDetails] = lineDetails;
        }

        return new Error(LineErrorCodes.ApiError, message, httpError.Category)
        {
            Exception = httpError.Exception,
            Data = data,
        };
    }

    /// <summary>
    /// 解析 LINE 的錯誤內容,取出 <c>message</c> 與串好的 <c>details</c>。
    /// Parses LINE's error body into its <c>message</c> and a flattened <c>details</c>.
    /// </summary>
    /// <param name="body">回應內容,可能為 <see langword="null"/> 或被截斷。The body, possibly <see langword="null"/> or truncated.</param>
    /// <returns>兩個值皆可能為 <see langword="null"/>。Either value may be <see langword="null"/>.</returns>
    private static (string? Message, string? Details) ParseErrorBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return (null, null);
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (null, null);
            }

            var message = document.RootElement.TryGetProperty("message", out var messageElement)
                && messageElement.ValueKind == JsonValueKind.String
                    ? messageElement.GetString()
                    : null;

            return (message, FlattenDetails(document.RootElement));
        }
        catch (JsonException)
        {
            // 內容不是 JSON,或是被 512 字元上限截斷成半截 JSON。這在錯誤路徑上很常見,
            // 不值得再製造第二個失敗 —— 原始內容仍然在 body 鍵裡,要看的人看得到。
            // The body is not JSON, or is half a JSON document after the 512-character cap. That is common
            // enough on the error path not to be worth a second failure: the raw body is still under the body
            // key for anyone who wants to look.
            return (null, null);
        }
    }

    /// <summary>
    /// 把 <c>details</c> 陣列串成單行字串。
    /// Flattens the <c>details</c> array into a single line.
    /// </summary>
    /// <param name="root">錯誤內容的根物件。The error body's root object.</param>
    /// <returns>沒有 details 時為 <see langword="null"/>。<see langword="null"/> when there are no details.</returns>
    private static string? FlattenDetails(JsonElement root)
    {
        if (!root.TryGetProperty("details", out var details) || details.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var builder = new StringBuilder();
        foreach (var detail in details.EnumerateArray())
        {
            if (detail.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var property = detail.TryGetProperty("property", out var propertyElement)
                && propertyElement.ValueKind == JsonValueKind.String
                    ? propertyElement.GetString()
                    : null;
            var detailMessage = detail.TryGetProperty("message", out var detailMessageElement)
                && detailMessageElement.ValueKind == JsonValueKind.String
                    ? detailMessageElement.GetString()
                    : null;

            if (property is null && detailMessage is null)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append("; ");
            }

            if (property is not null)
            {
                builder.Append(property).Append(": ");
            }

            builder.Append(detailMessage);
        }

        return builder.Length > 0 ? builder.ToString() : null;
    }
}
