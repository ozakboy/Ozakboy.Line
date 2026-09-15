using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ozakboy.Core.Abstractions;
using Ozakboy.Http;
using Ozakboy.Line.Messaging.Messages;
using Ozakboy.Line.Messaging.RichMenu;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// <see cref="ILineMessagingClient"/> 的實作。
/// The implementation of <see cref="ILineMessagingClient"/>.
/// </summary>
/// <remarks>
/// 送出的 JSON 全部由 <see cref="Utf8JsonWriter"/> 明確寫出,不靠反射轉欄位名:LINE 的欄位名
/// (<c>notificationDisabled</c>、<c>replyToken</c>、<c>chatBarText</c>)與屬性名的對應不是機械的,
/// 而寫錯的代價是一個只說「請求內容有錯」的 400。
/// Every outgoing JSON body is written explicitly with <see cref="Utf8JsonWriter"/> rather than reflected from
/// property names: LINE's field names (<c>notificationDisabled</c>, <c>replyToken</c>, <c>chatBarText</c>) do not
/// map mechanically onto property names, and getting one wrong costs a 400 that says only that the body is wrong.
/// </remarks>
public sealed partial class LineMessagingClient : ILineMessagingClient
{
    private readonly HttpPipelineClient _http;
    private readonly IOptions<LineMessagingOptions> _options;
    private readonly ILogger<LineMessagingClient>? _logger;

    /// <summary>
    /// 建立用戶端。
    /// Creates the client.
    /// </summary>
    /// <param name="http">管線用戶端。The pipeline client.</param>
    /// <param name="options">Messaging channel 設定。The Messaging channel settings.</param>
    /// <param name="logger">記錄器,可為 <see langword="null"/>。The logger, which may be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="http"/> 或 <paramref name="options"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="http"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    public LineMessagingClient(
        HttpPipelineClient http,
        IOptions<LineMessagingOptions> options,
        ILogger<LineMessagingClient>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(options);

        _http = http;
        _options = options;
        _logger = logger;
    }

    private LineMessagingOptions Options => _options.Value;

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<LineSentMessage>>> PushAsync(
        string to,
        IReadOnlyList<LineMessage> messages,
        LinePushOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        ArgumentNullException.ThrowIfNull(messages);

        if (!Options.IsConfigured)
        {
            return NotConfigured<IReadOnlyList<LineSentMessage>>();
        }

        var count = ValidateMessageCount(messages.Count);
        if (count.IsFailure)
        {
            return count.ToFailure<IReadOnlyList<LineSentMessage>>();
        }

        var request = Post(LineEndpoints.PushMessage, options, writer =>
        {
            writer.WriteString("to", to);
            writer.WritePropertyName("messages");
            LineMessageSerializer.WriteMessages(writer, messages);
            WriteNotificationDisabled(writer, options);
        });

        return await SendForSentMessagesAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<LineSentMessage>>> PushTextAsync(
        string to,
        string text,
        LinePushOptions? options = null,
        CancellationToken cancellationToken = default) =>
        PushAsync(to, [new TextMessage(text)], options, cancellationToken);

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<LineSentMessage>>> PushRawJsonAsync(
        string to,
        string messagesJson,
        LinePushOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        ArgumentException.ThrowIfNullOrWhiteSpace(messagesJson);

        if (!Options.IsConfigured)
        {
            return NotConfigured<IReadOnlyList<LineSentMessage>>();
        }

        var parsed = ParseRawMessages(messagesJson);
        if (parsed.IsFailure)
        {
            return parsed.ToFailure<IReadOnlyList<LineSentMessage>>();
        }

        using var document = parsed.GetValueOrThrow();
        var request = Post(LineEndpoints.PushMessage, options, writer =>
        {
            writer.WriteString("to", to);
            WriteRawMessages(writer, document.RootElement);
            WriteNotificationDisabled(writer, options);
        });

        return await SendForSentMessagesAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result> MulticastAsync(
        IReadOnlyList<string> to,
        IReadOnlyList<LineMessage> messages,
        LinePushOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(messages);

        if (!Options.IsConfigured)
        {
            return Result.Failure(NotConfiguredError());
        }

        if (to.Count is 0 or > LineMessagingLimits.MulticastRecipients)
        {
            return Error.Validation(
                LineErrorCodes.TooManyRecipients,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"multicast 的收件者需為 1 到 {LineMessagingLimits.MulticastRecipients} 人,這次是 {to.Count} 人。A multicast takes between 1 and {LineMessagingLimits.MulticastRecipients} recipients; {to.Count} were supplied."));
        }

        var count = ValidateMessageCount(messages.Count);
        if (count.IsFailure)
        {
            return count;
        }

        var request = Post(LineEndpoints.MulticastMessage, options, writer =>
        {
            writer.WriteStartArray("to");
            for (var index = 0; index < to.Count; index++)
            {
                writer.WriteStringValue(to[index]);
            }

            writer.WriteEndArray();
            writer.WritePropertyName("messages");
            LineMessageSerializer.WriteMessages(writer, messages);
            WriteNotificationDisabled(writer, options);
        });

        return await LineHttp.SendAsync(_http, request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result> BroadcastAsync(
        IReadOnlyList<LineMessage> messages,
        LinePushOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        if (!Options.IsConfigured)
        {
            return Result.Failure(NotConfiguredError());
        }

        var count = ValidateMessageCount(messages.Count);
        if (count.IsFailure)
        {
            return count;
        }

        var request = Post(LineEndpoints.BroadcastMessage, options, writer =>
        {
            writer.WritePropertyName("messages");
            LineMessageSerializer.WriteMessages(writer, messages);
            WriteNotificationDisabled(writer, options);
        });

        return await LineHttp.SendAsync(_http, request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<Result> BroadcastTextAsync(string text, LinePushOptions? options = null, CancellationToken cancellationToken = default) =>
        BroadcastAsync([new TextMessage(text)], options, cancellationToken);

    /// <inheritdoc />
    public async Task<Result> BroadcastRawJsonAsync(
        string messagesJson,
        LinePushOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messagesJson);

        if (!Options.IsConfigured)
        {
            return Result.Failure(NotConfiguredError());
        }

        var parsed = ParseRawMessages(messagesJson);
        if (parsed.IsFailure)
        {
            return parsed.ToResult();
        }

        using var document = parsed.GetValueOrThrow();
        var request = Post(LineEndpoints.BroadcastMessage, options, writer =>
        {
            WriteRawMessages(writer, document.RootElement);
            WriteNotificationDisabled(writer, options);
        });

        return await LineHttp.SendAsync(_http, request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<LineSentMessage>>> ReplyAsync(
        string replyToken,
        IReadOnlyList<LineMessage> messages,
        bool notificationDisabled = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replyToken);
        ArgumentNullException.ThrowIfNull(messages);

        if (!Options.IsConfigured)
        {
            return NotConfigured<IReadOnlyList<LineSentMessage>>();
        }

        var count = ValidateMessageCount(messages.Count);
        if (count.IsFailure)
        {
            return count.ToFailure<IReadOnlyList<LineSentMessage>>();
        }

        var request = Post(LineEndpoints.ReplyMessage, options: null, writer =>
        {
            writer.WriteString("replyToken", replyToken);
            writer.WritePropertyName("messages");
            LineMessageSerializer.WriteMessages(writer, messages);

            if (notificationDisabled)
            {
                writer.WriteBoolean("notificationDisabled", true);
            }
        });

        return await SendForSentMessagesAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<LineSentMessage>>> ReplyTextAsync(string replyToken, string text, CancellationToken cancellationToken = default) =>
        ReplyAsync(replyToken, [new TextMessage(text)], notificationDisabled: false, cancellationToken);

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<LineSentMessage>>> ReplyRawJsonAsync(
        string replyToken,
        string messagesJson,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replyToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(messagesJson);

        if (!Options.IsConfigured)
        {
            return NotConfigured<IReadOnlyList<LineSentMessage>>();
        }

        var parsed = ParseRawMessages(messagesJson);
        if (parsed.IsFailure)
        {
            return parsed.ToFailure<IReadOnlyList<LineSentMessage>>();
        }

        using var document = parsed.GetValueOrThrow();
        var request = Post(LineEndpoints.ReplyMessage, options: null, writer =>
        {
            writer.WriteString("replyToken", replyToken);
            WriteRawMessages(writer, document.RootElement);
        });

        return await SendForSentMessagesAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<Result<LineMessageQuota>> GetQuotaAsync(CancellationToken cancellationToken = default) =>
        !Options.IsConfigured
            ? Task.FromResult(NotConfigured<LineMessageQuota>())
            : LineHttp.SendForJsonAsync<LineMessageQuota>(_http, Get(LineEndpoints.MessageQuota), cancellationToken);

    /// <inheritdoc />
    public async Task<Result<long>> GetQuotaConsumptionAsync(CancellationToken cancellationToken = default)
    {
        if (!Options.IsConfigured)
        {
            return NotConfigured<long>();
        }

        var consumption = await LineHttp
            .SendForJsonAsync<LineQuotaConsumptionResponse>(_http, Get(LineEndpoints.MessageQuotaConsumption), cancellationToken)
            .ConfigureAwait(false);

        return consumption.IsFailure ? consumption.ToFailure<long>() : Result.Success(consumption.GetValueOrThrow().TotalUsage);
    }

    /// <inheritdoc />
    public Task<Result<LineBotInfo>> GetBotInfoAsync(CancellationToken cancellationToken = default) =>
        !Options.IsConfigured
            ? Task.FromResult(NotConfigured<LineBotInfo>())
            : LineHttp.SendForJsonAsync<LineBotInfo>(_http, Get(LineEndpoints.BotInfo), cancellationToken);

    /// <inheritdoc />
    public Task<Result<LineUserProfile>> GetProfileAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        return !Options.IsConfigured
            ? Task.FromResult(NotConfigured<LineUserProfile>())
            : LineHttp.SendForJsonAsync<LineUserProfile>(
                _http,
                Get(LineEndpoints.BotProfileBase + Uri.EscapeDataString(userId)),
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<LineContent>> GetMessageContentAsync(string messageId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        if (!Options.IsConfigured)
        {
            return NotConfigured<LineContent>();
        }

        var uri = $"{LineEndpoints.MessageContentBase}{Uri.EscapeDataString(messageId)}/content";

        // 這一條走 HttpPipelineClient.SendAsync 而不是 LineHttp 的包裝,因此請求的擁有權留在這裡,
        // 要自己釋放。
        // This path calls HttpPipelineClient.SendAsync directly rather than LineHttp's wrapper, so ownership of
        // the request stays here and it is disposed here.
        using var request = Get(uri);
        var sent = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (sent.IsFailure)
        {
            return Result.Failure<LineContent>(LineApiErrorMapper.Map(sent.Error));
        }

        // 這條路徑不能走 SendForStringAsync:內容是二進位,轉成字串會壞掉。非 2xx 的錯誤加工因此在這裡自己做一次,
        // 用的是同一個 HttpErrorMapper,錯誤形狀與其他端點完全相同。
        // This path cannot use SendForStringAsync: the content is binary and turning it into a string would
        // corrupt it. The non-2xx rewrite is therefore done here, through the same HttpErrorMapper, so the error
        // comes out exactly as it does from every other endpoint.
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

    /// <inheritdoc />
    public async Task<Result<string>> CreateRichMenuAsync(LineRichMenu richMenu, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(richMenu);

        if (!Options.IsConfigured)
        {
            return NotConfigured<string>();
        }

        var request = Authorize(new HttpRequestMessage(HttpMethod.Post, new Uri(LineEndpoints.RichMenu, UriKind.Absolute))
        {
            Content = JsonContent(JsonSerializer.SerializeToUtf8Bytes(richMenu, LineJson.Options)),
        });

        var created = await LineHttp
            .SendForJsonAsync<LineRichMenuIdResponse>(_http, request, cancellationToken)
            .ConfigureAwait(false);

        if (created.IsFailure)
        {
            return created.ToFailure<string>();
        }

        var id = created.GetValueOrThrow().RichMenuId;
        return string.IsNullOrWhiteSpace(id)
            ? Error.Internal(LineErrorCodes.ApiInvalidResponse, "建立圖文選單的回應沒有 richMenuId。The rich menu creation response carried no richMenuId.")
            : Result.Success(id);
    }

    /// <inheritdoc />
    public Task<Result> UploadRichMenuImageAsync(
        string richMenuId,
        byte[] image,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(richMenuId);
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        if (!Options.IsConfigured)
        {
            return Task.FromResult(Result.Failure(NotConfiguredError()));
        }

        var uri = $"{LineEndpoints.RichMenuContentBase}{Uri.EscapeDataString(richMenuId)}/content";
        var content = new ByteArrayContent(image);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        var request = Authorize(new HttpRequestMessage(HttpMethod.Post, new Uri(uri, UriKind.Absolute))
        {
            Content = content,
        });

        return LineHttp.SendAsync(_http, request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<LineRichMenuInfo>>> GetRichMenuListAsync(CancellationToken cancellationToken = default)
    {
        if (!Options.IsConfigured)
        {
            return NotConfigured<IReadOnlyList<LineRichMenuInfo>>();
        }

        var list = await LineHttp
            .SendForJsonAsync<LineRichMenuListResponse>(_http, Get(LineEndpoints.RichMenuList), cancellationToken)
            .ConfigureAwait(false);

        return list.IsFailure
            ? list.ToFailure<IReadOnlyList<LineRichMenuInfo>>()
            : Result.Success<IReadOnlyList<LineRichMenuInfo>>(list.GetValueOrThrow().RichMenus ?? []);
    }

    /// <inheritdoc />
    public Task<Result<LineRichMenuInfo>> GetRichMenuAsync(string richMenuId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(richMenuId);

        return !Options.IsConfigured
            ? Task.FromResult(NotConfigured<LineRichMenuInfo>())
            : LineHttp.SendForJsonAsync<LineRichMenuInfo>(
                _http,
                Get($"{LineEndpoints.RichMenu}/{Uri.EscapeDataString(richMenuId)}"),
                cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result> DeleteRichMenuAsync(string richMenuId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(richMenuId);

        return !Options.IsConfigured
            ? Task.FromResult(Result.Failure(NotConfiguredError()))
            : LineHttp.SendAsync(
                _http,
                Authorize(new HttpRequestMessage(HttpMethod.Delete, new Uri($"{LineEndpoints.RichMenu}/{Uri.EscapeDataString(richMenuId)}", UriKind.Absolute))),
                cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result> SetDefaultRichMenuAsync(string richMenuId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(richMenuId);

        return !Options.IsConfigured
            ? Task.FromResult(Result.Failure(NotConfiguredError()))
            : LineHttp.SendAsync(
                _http,
                Authorize(new HttpRequestMessage(HttpMethod.Post, new Uri($"{LineEndpoints.DefaultRichMenu}/{Uri.EscapeDataString(richMenuId)}", UriKind.Absolute))),
                cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result> ClearDefaultRichMenuAsync(CancellationToken cancellationToken = default) =>
        !Options.IsConfigured
            ? Task.FromResult(Result.Failure(NotConfiguredError()))
            : LineHttp.SendAsync(
                _http,
                Authorize(new HttpRequestMessage(HttpMethod.Delete, new Uri(LineEndpoints.DefaultRichMenu, UriKind.Absolute))),
                cancellationToken);

    /// <inheritdoc />
    public Task<Result<string?>> GetDefaultRichMenuIdAsync(CancellationToken cancellationToken = default) =>
        !Options.IsConfigured
            ? Task.FromResult(NotConfigured<string?>())
            : ReadRichMenuIdAsync(LineEndpoints.DefaultRichMenu, cancellationToken);

    /// <inheritdoc />
    public Task<Result> LinkRichMenuToUserAsync(string userId, string richMenuId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(richMenuId);

        var uri = $"{LineEndpoints.UserRichMenuBase}{Uri.EscapeDataString(userId)}/richmenu/{Uri.EscapeDataString(richMenuId)}";
        return !Options.IsConfigured
            ? Task.FromResult(Result.Failure(NotConfiguredError()))
            : LineHttp.SendAsync(
                _http,
                Authorize(new HttpRequestMessage(HttpMethod.Post, new Uri(uri, UriKind.Absolute))),
                cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result> UnlinkRichMenuFromUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var uri = $"{LineEndpoints.UserRichMenuBase}{Uri.EscapeDataString(userId)}/richmenu";
        return !Options.IsConfigured
            ? Task.FromResult(Result.Failure(NotConfiguredError()))
            : LineHttp.SendAsync(
                _http,
                Authorize(new HttpRequestMessage(HttpMethod.Delete, new Uri(uri, UriKind.Absolute))),
                cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<string?>> GetRichMenuIdOfUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        return !Options.IsConfigured
            ? Task.FromResult(NotConfigured<string?>())
            : ReadRichMenuIdAsync($"{LineEndpoints.UserRichMenuBase}{Uri.EscapeDataString(userId)}/richmenu", cancellationToken);
    }

    /// <summary>
    /// 讀一個「可能不存在」的圖文選單連結。
    /// Reads a rich menu link that may not exist.
    /// </summary>
    /// <param name="uri">查詢位址。The address to read.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>沒有連結時為成功且值為 <see langword="null"/>。Success with <see langword="null"/> when nothing is linked.</returns>
    /// <remarks>
    /// LINE 以 404 表達「這個使用者沒有連結選單」,而那是一個正常狀態、不是錯誤。照實回失敗會逼每個呼叫端
    /// 自己去讀錯誤代碼判斷「這個 404 是不是其實沒事」—— 那是把協定細節推給呼叫端。
    /// LINE expresses "this user has no menu linked" as a 404, and that is a normal state rather than an error.
    /// Reporting it as a failure would make every caller read the error code to decide whether this particular
    /// 404 is actually fine, which is protocol detail pushed onto the caller.
    /// </remarks>
    private async Task<Result<string?>> ReadRichMenuIdAsync(string uri, CancellationToken cancellationToken)
    {
        var read = await LineHttp
            .SendForJsonAsync<LineRichMenuIdResponse>(_http, Get(uri), cancellationToken)
            .ConfigureAwait(false);

        if (read.IsSuccess)
        {
            return Result.Success<string?>(read.GetValueOrThrow().RichMenuId);
        }

        if (read.Error.Category == ErrorCategory.NotFound)
        {
            if (_logger is not null)
            {
                Log.NoRichMenuLinked(_logger);
            }

            return Result.Success<string?>(null);
        }

        return read.ToFailure<string?>();
    }

    /// <summary>
    /// 送出並解析 <c>sentMessages</c>。
    /// Sends and reads <c>sentMessages</c>.
    /// </summary>
    /// <param name="request">請求。The request.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>已送出的訊息。The messages that were sent.</returns>
    private async Task<Result<IReadOnlyList<LineSentMessage>>> SendForSentMessagesAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var sent = await LineHttp
            .SendForJsonAsync<LineSentMessagesResponse>(_http, request, cancellationToken)
            .ConfigureAwait(false);

        return sent.IsFailure
            ? sent.ToFailure<IReadOnlyList<LineSentMessage>>()
            : Result.Success<IReadOnlyList<LineSentMessage>>(sent.GetValueOrThrow().SentMessages ?? []);
    }

    /// <summary>
    /// 檢查訊息則數。
    /// Checks the message count.
    /// </summary>
    /// <param name="count">則數。The count.</param>
    /// <returns>在允許範圍內時為成功。Success when within the allowed range.</returns>
    private static Result ValidateMessageCount(int count) =>
        count is >= LineMessagingLimits.MinMessagesPerRequest and <= LineMessagingLimits.MaxMessagesPerRequest
            ? Result.Success()
            : Error.Validation(
                LineErrorCodes.TooManyMessages,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"一次請求需帶 {LineMessagingLimits.MinMessagesPerRequest} 到 {LineMessagingLimits.MaxMessagesPerRequest} 則訊息,這次是 {count} 則。One request takes between {LineMessagingLimits.MinMessagesPerRequest} and {LineMessagingLimits.MaxMessagesPerRequest} messages; {count} were supplied."));

    /// <summary>
    /// 解析呼叫端給的原始訊息 JSON。
    /// Parses the raw message JSON supplied by the caller.
    /// </summary>
    /// <param name="messagesJson">單一訊息物件或訊息陣列。A single message object or an array of them.</param>
    /// <returns>可解析時為文件。The document when it parses.</returns>
    private static Result<JsonDocument> ParseRawMessages(string messagesJson)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(messagesJson);
        }
        catch (JsonException exception)
        {
            var error = Error.Validation(
                LineErrorCodes.InvalidJson,
                "訊息 JSON 無法解析。The message JSON could not be parsed.");
            return error with { Exception = exception };
        }

        if (document.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
        {
            return Result.Success(document);
        }

        document.Dispose();
        return Error.Validation(
            LineErrorCodes.InvalidJson,
            "訊息 JSON 必須是單一訊息物件或訊息陣列。The message JSON must be a single message object or an array of them.");
    }

    /// <summary>
    /// 把原始訊息 JSON 寫成 <c>messages</c> 陣列。
    /// Writes the raw message JSON as a <c>messages</c> array.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    /// <param name="root">解析後的根元素。The parsed root element.</param>
    /// <remarks>
    /// 單一物件也包成陣列:呼叫端手上常常是「一則訊息的 JSON」,要求他們自己加一層中括號,
    /// 只是把一個機械步驟推給每一個呼叫端,而少加的那一次會變成 LINE 的 400。
    /// A single object is wrapped into an array as well: what a caller usually has is the JSON of one message, and
    /// asking them to add the brackets is a mechanical step pushed onto every caller — with the one time it is
    /// forgotten coming back as a 400 from LINE.
    /// </remarks>
    private static void WriteRawMessages(Utf8JsonWriter writer, JsonElement root)
    {
        writer.WritePropertyName("messages");

        if (root.ValueKind == JsonValueKind.Array)
        {
            root.WriteTo(writer);
            return;
        }

        writer.WriteStartArray();
        root.WriteTo(writer);
        writer.WriteEndArray();
    }

    /// <summary>
    /// 寫出 <c>notificationDisabled</c>,只在為 <see langword="true"/> 時寫。
    /// Writes <c>notificationDisabled</c>, and only when it is <see langword="true"/>.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    /// <param name="options">可選參數。The optional parameters.</param>
    private static void WriteNotificationDisabled(Utf8JsonWriter writer, LinePushOptions? options)
    {
        if (options?.NotificationDisabled == true)
        {
            writer.WriteBoolean("notificationDisabled", true);
        }
    }

    /// <summary>
    /// 建立帶權杖的 GET 請求。
    /// Builds an authorized GET request.
    /// </summary>
    /// <param name="uri">目標位址。The target address.</param>
    /// <returns>建好的請求。The request.</returns>
    private HttpRequestMessage Get(string uri) =>
        Authorize(new HttpRequestMessage(HttpMethod.Get, new Uri(uri, UriKind.Absolute)));

    /// <summary>
    /// 建立帶權杖與 JSON 內容的 POST 請求。
    /// Builds an authorized POST request carrying a JSON body.
    /// </summary>
    /// <param name="uri">目標位址。The target address.</param>
    /// <param name="options">可選參數,決定重試鍵。The optional parameters, which decide the retry key.</param>
    /// <param name="writeBody">寫出內容欄位的委派。The delegate writing the body's fields.</param>
    /// <returns>建好的請求。The request.</returns>
    private HttpRequestMessage Post(string uri, LinePushOptions? options, Action<Utf8JsonWriter> writeBody)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writeBody(writer);
            writer.WriteEndObject();
        }

        var request = Authorize(new HttpRequestMessage(HttpMethod.Post, new Uri(uri, UriKind.Absolute))
        {
            Content = JsonContent(buffer.ToArray()),
        });

        if (options?.RetryKey is { } retryKey)
        {
            // 兩件事必須同時做:標頭讓 LINE 去重,AsIdempotent 讓管線願意重試。
            // 只標其中一個的結果分別是「重試但會重複發送」與「帶了鍵卻永遠不重試」,兩種都不是本意。
            // Both are needed: the header is what lets LINE deduplicate, and AsIdempotent is what lets the
            // pipeline retry at all. Doing only one gives either retries that duplicate messages or a key that is
            // never used, and neither is the intent.
            request.Headers.TryAddWithoutValidation("X-Line-Retry-Key", retryKey.ToString("D", CultureInfo.InvariantCulture));
            request.AsIdempotent();
        }

        return request;
    }

    /// <summary>
    /// 建立 JSON 內容。
    /// Builds a JSON body.
    /// </summary>
    /// <param name="utf8">已編碼的 UTF-8 位元組。The encoded UTF-8 bytes.</param>
    /// <returns>內容。The content.</returns>
    private static ByteArrayContent JsonContent(byte[] utf8)
    {
        var content = new ByteArrayContent(utf8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        return content;
    }

    /// <summary>
    /// 掛上頻道存取權杖。
    /// Attaches the channel access token.
    /// </summary>
    /// <param name="request">請求。The request.</param>
    /// <returns>同一個請求。The same request.</returns>
    private HttpRequestMessage Authorize(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Options.ChannelAccessToken);
        return request;
    }

    /// <summary>
    /// 產生「頻道未設定」的失敗。
    /// Builds the not-configured failure.
    /// </summary>
    /// <typeparam name="T">結果型別。The result type.</typeparam>
    /// <returns>失敗。The failure.</returns>
    private static Result<T> NotConfigured<T>() => Result.Failure<T>(NotConfiguredError());

    /// <summary>
    /// 「頻道未設定」的錯誤內容。
    /// The not-configured error.
    /// </summary>
    /// <returns>錯誤。The error.</returns>
    private static Error NotConfiguredError() => Error.Validation(
        LineErrorCodes.NotConfigured,
        "LINE Messaging API 的 ChannelAccessToken 必須設定。ChannelAccessToken must be set for the LINE Messaging API.");

    /// <summary>
    /// 記錄訊息的定義。
    /// The log message definitions.
    /// </summary>
    private static partial class Log
    {
        /// <summary>
        /// 查不到圖文選單連結時的記錄。
        /// Logged when no rich menu is linked.
        /// </summary>
        /// <param name="logger">記錄器。The logger.</param>
        [LoggerMessage(
            EventId = 2100,
            Level = LogLevel.Debug,
            Message = "查詢圖文選單連結得到 404,視為「沒有連結」。A rich menu lookup answered 404 and is read as \"nothing linked\".")]
        internal static partial void NoRichMenuLinked(ILogger logger);
    }
}
