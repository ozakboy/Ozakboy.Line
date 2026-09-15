using Microsoft.AspNetCore.Http;
using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Webhook;

namespace Ozakboy.Line.AspNetCore.Webhook;

/// <summary>
/// 從 <see cref="HttpRequest"/> 讀出並驗證 LINE webhook 內容。
/// Reads and verifies a LINE webhook body from an <see cref="HttpRequest"/>.
/// </summary>
public static class LineWebhookHttpRequestExtensions
{
    /// <summary>
    /// LINE 放簽章的標頭名稱。
    /// The header name LINE puts its signature in.
    /// </summary>
    public const string SignatureHeaderName = "X-Line-Signature";

    /// <summary>
    /// 讀取請求內容、驗證簽章、解析事件。
    /// Reads the body, verifies the signature, and parses the events.
    /// </summary>
    /// <param name="request">HTTP 請求。The HTTP request.</param>
    /// <param name="channelSecret">Messaging channel 的密鑰。The Messaging channel's secret.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>
    /// 驗簽通過且可解析時為事件內容;否則為 <see cref="LineErrorCodes.WebhookInvalidSignature"/> 或
    /// <see cref="LineErrorCodes.WebhookInvalidPayload"/> 的失敗。
    /// The events when the signature verifies and the body parses; otherwise a
    /// <see cref="LineErrorCodes.WebhookInvalidSignature"/> or
    /// <see cref="LineErrorCodes.WebhookInvalidPayload"/> failure.
    /// </returns>
    /// <remarks>
    /// 讀的是<b>原始位元組</b>,不經過任何模型繫結。驗簽算的是 LINE 送來的那一份內容,
    /// 只要中間經過一次「解析成物件再序列化回去」,欄位順序或跳脫方式一變,簽章就再也對不上。
    /// The <b>raw bytes</b> are read, with no model binding in between. The signature covers the body exactly as
    /// LINE sent it, and one round trip through parse-then-reserialise is enough to change field order or
    /// escaping and leave the signature permanently mismatched.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="request"/> 為 <see langword="null"/> 時擲出。Thrown when <paramref name="request"/> is <see langword="null"/>.
    /// </exception>
    public static async Task<Result<LineWebhookPayload>> ReadLineWebhookAsync(
        this HttpRequest request,
        string channelSecret,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        var body = buffer.ToArray();

        var signature = request.Headers[SignatureHeaderName].ToString();
        if (!LineWebhookSignature.Verify(channelSecret, body, signature))
        {
            // 訊息不說明是「標頭缺漏」還是「簽章不符」:對呼叫端而言結論相同(這個請求不可信),
            // 而對送出請求的一方,任何區分都只是在幫他縮小猜測範圍。
            // The message does not distinguish a missing header from a mismatched signature: the conclusion is
            // the same for the caller — this request cannot be trusted — and for whoever sent it, any
            // distinction only narrows their guesswork.
            return Error.Validation(
                LineErrorCodes.WebhookInvalidSignature,
                "webhook 請求的簽章驗證失敗。The webhook request's signature did not verify.");
        }

        return LineWebhookParser.Parse(body);
    }
}
