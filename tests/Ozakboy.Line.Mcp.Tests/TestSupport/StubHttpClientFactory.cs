using System.Net;

namespace Ozakboy.Line.Mcp.Tests.TestSupport;

/// <summary>
/// 回傳固定內容的 <see cref="IHttpClientFactory"/>,用來代替抓圖的那一條網路請求。
/// An <see cref="IHttpClientFactory"/> answering with fixed content, standing in for the image fetch.
/// </summary>
/// <remarks>
/// 測試一律離線。抓圖是整個套件裡唯一一條目的地由外部決定的請求,更不該在測試裡真的出門。
/// The tests are entirely offline. The image fetch is the one request in the package whose destination comes
/// from outside, which makes it the last one that should leave the machine during a test.
/// </remarks>
internal sealed class StubHttpClientFactory : IHttpClientFactory, IDisposable
{
    private readonly StubHandler _handler;

    /// <summary>
    /// 以圖片內容與型別建立。
    /// Creates one for a given image body and type.
    /// </summary>
    /// <param name="bytes">圖片位元組。The image bytes.</param>
    /// <param name="contentType">圖片型別。The image type.</param>
    /// <param name="status">狀態碼。The status code.</param>
    internal StubHttpClientFactory(byte[] bytes, string contentType = "image/png", HttpStatusCode status = HttpStatusCode.OK) =>
        _handler = new StubHandler(bytes, contentType, status);

    /// <summary>
    /// 抓過的位址。
    /// The addresses that were fetched.
    /// </summary>
    internal List<string> Requests => _handler.Requests;

    /// <inheritdoc />
    public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);

    /// <inheritdoc />
    public void Dispose() => _handler.Dispose();

    /// <summary>
    /// 一律回同一份內容的處理器。
    /// A handler answering with the same content every time.
    /// </summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly byte[] _bytes;
        private readonly string _contentType;
        private readonly HttpStatusCode _status;

        /// <summary>
        /// 建立處理器。
        /// Creates the handler.
        /// </summary>
        /// <param name="bytes">圖片位元組。The image bytes.</param>
        /// <param name="contentType">圖片型別。The image type.</param>
        /// <param name="status">狀態碼。The status code.</param>
        internal StubHandler(byte[] bytes, string contentType, HttpStatusCode status)
        {
            _bytes = bytes;
            _contentType = contentType;
            _status = status;
        }

        /// <summary>
        /// 抓過的位址。
        /// The addresses that were fetched.
        /// </summary>
        internal List<string> Requests { get; } = [];

        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.ToString());

            var content = new ByteArrayContent(_bytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(_contentType);

            return Task.FromResult(new HttpResponseMessage(_status) { Content = content });
        }
    }
}
