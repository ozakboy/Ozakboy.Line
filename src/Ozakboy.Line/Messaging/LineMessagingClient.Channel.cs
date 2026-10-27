using Ozakboy.Core.Abstractions;
using Ozakboy.Http;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// <see cref="LineMessagingClient"/> 的頻道設定、帳號連結與訊息內容補充端點。
/// The channel-settings, account-linking and message-content additions to <see cref="LineMessagingClient"/>.
/// </summary>
public sealed partial class LineMessagingClient
{
    /// <summary>
    /// webhook 位址的長度上限(LINE 的限制是 500 字元)。
    /// The maximum length of a webhook address; LINE's limit is 500 characters.
    /// </summary>
    private const int MaxWebhookEndpointLength = 500;

    /// <inheritdoc />
    public Task<Result<LineWebhookEndpointInfo>> GetWebhookEndpointAsync(CancellationToken cancellationToken = default) =>
        !Options.IsConfigured
            ? Task.FromResult(NotConfigured<LineWebhookEndpointInfo>())
            : LineHttp.SendForJsonAsync<LineWebhookEndpointInfo>(_http, Get(LineEndpoints.WebhookEndpoint), cancellationToken);

    /// <inheritdoc />
    public Task<Result> SetWebhookEndpointAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        if (!Options.IsConfigured)
        {
            return Task.FromResult(Result.Failure(NotConfiguredError()));
        }

        if (ValidateWebhookEndpoint(endpoint) is { } invalid)
        {
            return Task.FromResult(Result.Failure(invalid));
        }

        return LineHttp.SendAsync(
            _http,
            JsonRequest(HttpMethod.Put, LineEndpoints.WebhookEndpoint, retryKey: null, writer => writer.WriteString("endpoint", endpoint)),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<LineWebhookTestResult>> TestWebhookEndpointAsync(string? endpoint = null, CancellationToken cancellationToken = default)
    {
        if (!Options.IsConfigured)
        {
            return Task.FromResult(NotConfigured<LineWebhookTestResult>());
        }

        if (endpoint is not null && ValidateWebhookEndpoint(endpoint) is { } invalid)
        {
            return Task.FromResult(Result.Failure<LineWebhookTestResult>(invalid));
        }

        // 沒給位址就送空物件 {}:LINE 會測試頻道目前設定的位址。
        // Without an address an empty object {} is sent, and LINE tests the one currently set on the channel.
        var request = JsonRequest(
            HttpMethod.Post,
            LineEndpoints.WebhookTest,
            retryKey: null,
            writer =>
            {
                if (endpoint is not null)
                {
                    writer.WriteString("endpoint", endpoint);
                }
            });

        return LineHttp.SendForJsonAsync<LineWebhookTestResult>(_http, request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<string>> IssueLinkTokenAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        if (!Options.IsConfigured)
        {
            return NotConfigured<string>();
        }

        var uri = $"{LineEndpoints.UserLinkTokenBase}{Uri.EscapeDataString(userId)}/linkToken";
        var issued = await LineHttp
            .SendForJsonAsync<LineLinkTokenResponse>(
                _http,
                Authorize(new HttpRequestMessage(HttpMethod.Post, new Uri(uri, UriKind.Absolute))),
                cancellationToken)
            .ConfigureAwait(false);

        if (issued.IsFailure)
        {
            return issued.ToFailure<string>();
        }

        var token = issued.GetValueOrThrow().LinkToken;
        return string.IsNullOrEmpty(token)
            ? Error.Internal(
                LineErrorCodes.ApiInvalidResponse,
                "LINE 的回應裡沒有 linkToken。The LINE response carried no linkToken.")
            : Result.Success(token);
    }

    /// <inheritdoc />
    public Task<Result<LineContent>> GetMessageContentPreviewAsync(string messageId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        return !Options.IsConfigured
            ? Task.FromResult(NotConfigured<LineContent>())
            : DownloadMessageContentAsync(
                $"{LineEndpoints.MessageContentBase}{Uri.EscapeDataString(messageId)}/content/preview",
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<LineContentTranscodingStatus>> GetMessageContentTranscodingStatusAsync(
        string messageId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        if (!Options.IsConfigured)
        {
            return NotConfigured<LineContentTranscodingStatus>();
        }

        var uri = $"{LineEndpoints.MessageContentBase}{Uri.EscapeDataString(messageId)}/content/transcoding";
        var read = await LineHttp
            .SendForJsonAsync<LineContentTranscodingResponse>(_http, Get(uri), cancellationToken)
            .ConfigureAwait(false);

        if (read.IsFailure)
        {
            return read.ToFailure<LineContentTranscodingStatus>();
        }

        return read.GetValueOrThrow().Status switch
        {
            "processing" => Result.Success(LineContentTranscodingStatus.Processing),
            "succeeded" => Result.Success(LineContentTranscodingStatus.Succeeded),
            "failed" => Result.Success(LineContentTranscodingStatus.Failed),
            _ => Error.Internal(
                LineErrorCodes.ApiInvalidResponse,
                "LINE 回了無法辨認的轉檔狀態。LINE answered a transcoding status that is not recognised."),
        };
    }

    /// <summary>
    /// 檢查 webhook 位址是否合規則。
    /// Checks that a webhook address is acceptable.
    /// </summary>
    /// <param name="endpoint">要檢查的位址。The address to check.</param>
    /// <returns>合規則時為 <see langword="null"/>,否則為驗證錯誤。<see langword="null"/> when acceptable, otherwise a validation error.</returns>
    private static Error? ValidateWebhookEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint)
            || !endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || endpoint.Length > MaxWebhookEndpointLength)
        {
            return Error.Validation(
                LineErrorCodes.InvalidWebhookEndpoint,
                $"webhook 位址必須以 https:// 開頭且不超過 {MaxWebhookEndpointLength} 字元。The webhook address must start with https:// and be at most {MaxWebhookEndpointLength} characters.");
        }

        return null;
    }

    /// <summary>
    /// 下載二進位的訊息內容(原檔或縮圖)。
    /// Downloads binary message content — the original or its preview.
    /// </summary>
    /// <param name="uri">目標位址。The target address.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>內容位元組與內容型別。The content bytes and content type.</returns>
    private async Task<Result<LineContent>> DownloadMessageContentAsync(string uri, CancellationToken cancellationToken)
    {
        // 與 GetMessageContentAsync 同一條路徑:二進位不能轉成字串,所以直接走 SendAsync,
        // 請求由這裡釋放,非 2xx 的錯誤加工也在這裡用同一個 HttpErrorMapper 做一次。
        // The same path as GetMessageContentAsync: binary cannot go through a string, so it calls SendAsync
        // directly, disposes the request here, and applies the non-2xx rewrite here through the same HttpErrorMapper.
        using var request = Get(uri);
        var sent = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (sent.IsFailure)
        {
            return Result.Failure<LineContent>(LineApiErrorMapper.Map(sent.Error));
        }

        using var response = sent.GetValueOrThrow();
        if (!response.IsSuccessStatusCode)
        {
            var error = await HttpErrorMapper
                .FromResponseAsync(response, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return Result.Failure<LineContent>(LineApiErrorMapper.Map(error));
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        return Result.Success(new LineContent(bytes, contentType));
    }
}
