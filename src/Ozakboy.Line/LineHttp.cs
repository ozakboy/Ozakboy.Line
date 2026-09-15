using System.Net.Http.Headers;
using System.Text.Json;
using Ozakboy.Core.Abstractions;
using Ozakboy.Http;

namespace Ozakboy.Line;

/// <summary>
/// 送出請求、加工錯誤、反序列化內容,三件事的共用路徑。
/// The shared path for sending a request, rewriting its failure, and deserialising its body.
/// </summary>
/// <remarks>
/// 兩個用戶端(Login 與 Messaging)都走這裡,錯誤形狀才會一致:同一種失敗在兩邊拿到的
/// 代碼、分類與資料鍵完全相同,呼叫端不必分兩套處理。
/// Both clients — Login and Messaging — go through here so failures come out the same shape: the same kind of
/// failure yields the same code, category, and data keys on either side, and a caller needs only one way to
/// handle them.
/// </remarks>
internal static class LineHttp
{
    /// <summary>
    /// 送出請求並讀回內容字串,非 2xx 時回傳加工過的 LINE 錯誤。
    /// Sends the request and reads the body as a string, returning a rewritten LINE failure on a non-2xx status.
    /// </summary>
    /// <param name="http">管線用戶端。The pipeline client.</param>
    /// <param name="request">請求,送完由這個方法釋放。The request, disposed here once sent.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>成功時為回應內容。The response body on success.</returns>
    internal static async Task<Result<string>> SendForStringAsync(
        HttpPipelineClient http,
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using (request)
        {
            var result = await http.SendForStringAsync(request, cancellationToken).ConfigureAwait(false);
            return result.IsFailure ? Result.Failure<string>(LineApiErrorMapper.Map(result.Error)) : result;
        }
    }

    /// <summary>
    /// 送出請求、讀回內容並反序列化。
    /// Sends the request, reads the body, and deserialises it.
    /// </summary>
    /// <typeparam name="T">目標型別。The target type.</typeparam>
    /// <param name="http">管線用戶端。The pipeline client.</param>
    /// <param name="request">請求,送完由這個方法釋放。The request, disposed here once sent.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>成功時為反序列化後的物件。The deserialised object on success.</returns>
    internal static async Task<Result<T>> SendForJsonAsync<T>(
        HttpPipelineClient http,
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = await SendForStringAsync(http, request, cancellationToken).ConfigureAwait(false);
        return body.IsFailure ? body.ToFailure<T>() : Deserialize<T>(body.GetValueOrDefault() ?? string.Empty);
    }

    /// <summary>
    /// 送出請求但不在意回應內容(LINE 多數寫入端點成功時回空物件)。
    /// Sends the request and ignores the body, which most of LINE's write endpoints return as an empty object.
    /// </summary>
    /// <param name="http">管線用戶端。The pipeline client.</param>
    /// <param name="request">請求,送完由這個方法釋放。The request, disposed here once sent.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>成功或失敗。Success or failure.</returns>
    internal static async Task<Result> SendAsync(
        HttpPipelineClient http,
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = await SendForStringAsync(http, request, cancellationToken).ConfigureAwait(false);
        return body.ToResult();
    }

    /// <summary>
    /// 反序列化 LINE 的回應內容。
    /// Deserialises a LINE response body.
    /// </summary>
    /// <typeparam name="T">目標型別。The target type.</typeparam>
    /// <param name="json">回應內容。The response body.</param>
    /// <returns>
    /// 內容不是預期形狀時為 <see cref="LineErrorCodes.ApiInvalidResponse"/> 失敗。
    /// A <see cref="LineErrorCodes.ApiInvalidResponse"/> failure when the body is not the expected shape.
    /// </returns>
    internal static Result<T> Deserialize<T>(string json)
    {
        try
        {
            var value = JsonSerializer.Deserialize<T>(json, LineJson.Options);
            return value is null
                ? Error.Internal(LineErrorCodes.ApiInvalidResponse, "LINE 的回應內容為 null。The LINE response body was null.")
                : Result.Success(value);
        }
        catch (JsonException exception)
        {
            // 訊息刻意只講「讀不懂」而不回述內容:回應裡可能有使用者的顯示名稱或大頭貼位址,
            // 而錯誤訊息最後多半會進日誌。
            // The message says only that the body could not be read and never echoes it: a response can carry a
            // user's display name or avatar URL, and an error message usually ends up in a log.
            var error = Error.Internal(
                LineErrorCodes.ApiInvalidResponse,
                "LINE 的回應內容無法解析成預期的形狀。The LINE response body could not be read into the expected shape.");
            return error with { Exception = exception };
        }
    }

    /// <summary>
    /// 建立帶 Bearer 權杖的請求。
    /// Builds a request carrying a bearer token.
    /// </summary>
    /// <param name="method">HTTP 方法。The HTTP method.</param>
    /// <param name="uri">目標位址。The target address.</param>
    /// <param name="accessToken">存取權杖。The access token.</param>
    /// <returns>建好的請求。The request.</returns>
    internal static HttpRequestMessage Bearer(HttpMethod method, string uri, string accessToken)
    {
        var request = new HttpRequestMessage(method, new Uri(uri, UriKind.Absolute));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }
}
