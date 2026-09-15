using System.Net;
using Ozakboy.Http;

namespace Ozakboy.Line.Tests.TestSupport;

/// <summary>
/// 記錄所有送出的請求並回傳事先安排好的回應。
/// Records every request sent and answers with a pre-arranged response.
/// </summary>
/// <remarks>
/// 測試一律離線。這個處理器擋在最底層,LINE 的真實端點一次都不會被打到。
/// The tests are entirely offline. This handler sits at the bottom of the pipeline and LINE's real endpoints are
/// never called.
/// </remarks>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, string?, HttpResponseMessage> _responder;

    /// <summary>
    /// 以自訂的回應邏輯建立。
    /// Creates one with custom response logic.
    /// </summary>
    /// <param name="responder">依請求與其內容決定回應。Decides the response from the request and its body.</param>
    internal FakeHttpMessageHandler(Func<HttpRequestMessage, string?, HttpResponseMessage> responder) =>
        _responder = responder;

    /// <summary>
    /// 送出過的請求,依序記錄。
    /// The requests that were sent, in order.
    /// </summary>
    internal List<RecordedRequest> Requests { get; } = [];

    /// <summary>
    /// 最後一個請求。
    /// The last request.
    /// </summary>
    internal RecordedRequest Last => Requests[^1];

    /// <summary>
    /// 一律以同一份 JSON 回應。
    /// Always answers with the same JSON.
    /// </summary>
    /// <param name="json">回應內容。The response body.</param>
    /// <param name="status">狀態碼。The status code.</param>
    /// <returns>處理器。The handler.</returns>
    internal static FakeHttpMessageHandler Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new((_, _) => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });

    /// <summary>
    /// 依請求位址是否含有指定片段決定回應。
    /// Answers according to whether the request address contains a given fragment.
    /// </summary>
    /// <param name="routes">片段與回應內容的對應,依序比對。Fragments paired with response bodies, matched in order.</param>
    /// <returns>處理器。The handler.</returns>
    /// <remarks>
    /// 沒有任何片段命中時回 404 與空的 LINE 錯誤內容 —— 測試因此不會因為「忘了安排某個端點」
    /// 而得到一個看起來像成功的結果。
    /// When nothing matches it answers 404 with an empty LINE error body, so a test never mistakes "an endpoint
    /// was not arranged" for a success.
    /// </remarks>
    internal static FakeHttpMessageHandler Routes(params (string Fragment, string Json)[] routes) =>
        new((request, _) =>
        {
            var uri = request.RequestUri!.ToString();
            foreach (var (fragment, json) in routes)
            {
                if (uri.Contains(fragment, StringComparison.Ordinal))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
                    };
                }
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"message\":\"no route arranged\"}", System.Text.Encoding.UTF8, "application/json"),
            };
        });

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        Requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri!,
            body,
            request.Headers.ToDictionary(header => header.Key, header => string.Join(",", header.Value), StringComparer.OrdinalIgnoreCase),
            request.Content?.Headers.ContentType?.MediaType,
            request.GetIdempotency()));

        return _responder(request, body);
    }
}
