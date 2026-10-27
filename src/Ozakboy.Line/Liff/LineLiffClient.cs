using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ozakboy.Core.Abstractions;
using Ozakboy.Http;

namespace Ozakboy.Line.Liff;

/// <summary>
/// <see cref="ILineLiffClient"/> 的實作。
/// The implementation of <see cref="ILineLiffClient"/>.
/// </summary>
/// <remarks>
/// 送出的 JSON 一律用 <see cref="Utf8JsonWriter"/> 明確寫出,而不是靠屬性名稱反射:更新要「只送有值的欄位」,
/// 而 <see cref="LineLiffApp.LiffId"/> 這種讀回才有的欄位絕不能出現在請求裡。
/// Every outgoing JSON body is written explicitly with <see cref="Utf8JsonWriter"/> rather than reflected from
/// property names: an update sends only the fields that have a value, and a read-back-only field such as
/// <see cref="LineLiffApp.LiffId"/> must never appear in a request.
/// </remarks>
public sealed partial class LineLiffClient : ILineLiffClient
{
    /// <summary>
    /// 說明欄位的長度上限(LINE 的限制是 100 字元)。
    /// The maximum length of the description; LINE's limit is 100 characters.
    /// </summary>
    private const int MaxDescriptionLength = 100;

    private readonly HttpPipelineClient _http;
    private readonly IOptions<LineLiffOptions> _options;
    private readonly ILogger<LineLiffClient>? _logger;

    /// <summary>
    /// 建立用戶端。
    /// Creates the client.
    /// </summary>
    /// <param name="http">管線用戶端。The pipeline client.</param>
    /// <param name="options">LIFF 設定。The LIFF settings.</param>
    /// <param name="logger">記錄器,可為 <see langword="null"/>。The logger, which may be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="http"/> 或 <paramref name="options"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="http"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    public LineLiffClient(
        HttpPipelineClient http,
        IOptions<LineLiffOptions> options,
        ILogger<LineLiffClient>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(options);

        _http = http;
        _options = options;
        _logger = logger;
    }

    private LineLiffOptions Options => _options.Value;

    /// <inheritdoc />
    public async Task<Result<string>> AddAsync(LineLiffApp app, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!Options.IsConfigured)
        {
            return Result.Failure<string>(NotConfiguredError());
        }

        if (Validate(app, forAdd: true) is { } invalid)
        {
            return Result.Failure<string>(invalid);
        }

        var added = await LineHttp
            .SendForJsonAsync<LineLiffAddResponse>(
                _http,
                JsonRequest(HttpMethod.Post, LineEndpoints.LiffApps, app),
                cancellationToken)
            .ConfigureAwait(false);

        if (added.IsFailure)
        {
            return added.ToFailure<string>();
        }

        var liffId = added.GetValueOrThrow().LiffId;
        return string.IsNullOrEmpty(liffId)
            ? Error.Internal(
                LineErrorCodes.ApiInvalidResponse,
                "LINE 的回應裡沒有 liffId。The LINE response carried no liffId.")
            : Result.Success(liffId);
    }

    /// <inheritdoc />
    public Task<Result> UpdateAsync(string liffId, LineLiffApp app, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(liffId);
        ArgumentNullException.ThrowIfNull(app);

        if (!Options.IsConfigured)
        {
            return Task.FromResult(Result.Failure(NotConfiguredError()));
        }

        if (Validate(app, forAdd: false) is { } invalid)
        {
            return Task.FromResult(Result.Failure(invalid));
        }

        return LineHttp.SendAsync(
            _http,
            JsonRequest(HttpMethod.Put, AppUri(liffId), app),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<LineLiffApp>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (!Options.IsConfigured)
        {
            return Result.Failure<IReadOnlyList<LineLiffApp>>(NotConfiguredError());
        }

        var read = await LineHttp
            .SendForJsonAsync<LineLiffListResponse>(_http, Authorize(HttpMethod.Get, LineEndpoints.LiffApps), cancellationToken)
            .ConfigureAwait(false);

        if (read.IsSuccess)
        {
            return Result.Success<IReadOnlyList<LineLiffApp>>(read.GetValueOrThrow().Apps ?? []);
        }

        // 這個 channel 還沒有任何 LIFF 時 LINE 回 404:那是「空」,不是「出錯」。
        // When the channel has no LIFF app yet, LINE answers 404 — that is "empty", not "wrong".
        if (read.Error.Category == ErrorCategory.NotFound)
        {
            if (_logger is not null)
            {
                Log.NoLiffApps(_logger);
            }

            return Result.Success<IReadOnlyList<LineLiffApp>>([]);
        }

        return read.ToFailure<IReadOnlyList<LineLiffApp>>();
    }

    /// <inheritdoc />
    public Task<Result> DeleteAsync(string liffId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(liffId);

        return !Options.IsConfigured
            ? Task.FromResult(Result.Failure(NotConfiguredError()))
            : LineHttp.SendAsync(_http, Authorize(HttpMethod.Delete, AppUri(liffId)), cancellationToken);
    }

    /// <summary>
    /// 「憑證未設定」的錯誤內容。
    /// The not-configured error.
    /// </summary>
    /// <returns>錯誤。The error.</returns>
    internal static Error NotConfiguredError() => Error.Validation(
        LineErrorCodes.NotConfigured,
        "LINE LIFF 的 ChannelAccessToken(LINE Login channel 的)必須設定。The LINE Login channel's ChannelAccessToken must be set for LIFF.");

    /// <summary>
    /// 單一 LIFF 應用程式的位址。
    /// The address of a single LIFF app.
    /// </summary>
    /// <param name="liffId">LIFF ID。The LIFF ID.</param>
    /// <returns>位址。The address.</returns>
    private static string AppUri(string liffId) => $"{LineEndpoints.LiffApps}/{Uri.EscapeDataString(liffId)}";

    /// <summary>
    /// 檢查 LIFF 設定是否合規則。
    /// Checks that a LIFF app's settings are acceptable.
    /// </summary>
    /// <param name="app">設定。The settings.</param>
    /// <param name="forAdd">是否為新增(新增要求檢視設定齊全)。Whether this is an add, which requires a complete view.</param>
    /// <returns>合規則時為 <see langword="null"/>,否則為驗證錯誤。<see langword="null"/> when acceptable, otherwise a validation error.</returns>
    private static Error? Validate(LineLiffApp app, bool forAdd)
    {
        if (forAdd && (app.View?.Type is null || string.IsNullOrWhiteSpace(app.View.Url)))
        {
            return Invalid("新增 LIFF 時 View.Type 與 View.Url 必填。View.Type and View.Url are required when adding a LIFF app.");
        }

        if (app.View?.Url is { } url && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return Invalid("View.Url 必須以 https:// 開頭。View.Url must start with https://.");
        }

        if (app.Description is { Length: > MaxDescriptionLength })
        {
            return Invalid($"Description 不可超過 {MaxDescriptionLength} 字元。Description must be at most {MaxDescriptionLength} characters.");
        }

        return null;

        static Error Invalid(string message) => Error.Validation(LineErrorCodes.InvalidLiffApp, message);
    }

    /// <summary>
    /// 建立帶權杖與 LIFF 設定內容的請求(POST 或 PUT)。
    /// Builds an authorized request, POST or PUT, carrying the LIFF app's settings.
    /// </summary>
    /// <param name="method">HTTP 方法。The HTTP method.</param>
    /// <param name="uri">目標位址。The target address.</param>
    /// <param name="app">設定。The settings.</param>
    /// <returns>建好的請求。The request.</returns>
    private HttpRequestMessage JsonRequest(HttpMethod method, string uri, LineLiffApp app)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteApp(writer, app);
        }

        var content = new ByteArrayContent(buffer.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };

        var request = Authorize(method, uri);
        request.Content = content;
        return request;
    }

    /// <summary>
    /// 把 LIFF 設定寫成請求內容,只寫有值的欄位,也絕不寫 <see cref="LineLiffApp.LiffId"/>。
    /// Writes the settings as a request body, only the fields with a value and never
    /// <see cref="LineLiffApp.LiffId"/>.
    /// </summary>
    /// <param name="writer">寫出器。The writer.</param>
    /// <param name="app">設定。The settings.</param>
    private static void WriteApp(Utf8JsonWriter writer, LineLiffApp app)
    {
        writer.WriteStartObject();

        if (app.View is { } view)
        {
            writer.WriteStartObject("view");
            if (view.Type is { } type)
            {
                writer.WriteString("type", LineLiffViewTypeConverter.ToWire(type));
            }

            if (view.Url is not null)
            {
                writer.WriteString("url", view.Url);
            }

            if (view.ModuleMode is { } moduleMode)
            {
                writer.WriteBoolean("moduleMode", moduleMode);
            }

            writer.WriteEndObject();
        }

        if (app.Description is not null)
        {
            writer.WriteString("description", app.Description);
        }

        if (app.Features is { } features)
        {
            writer.WriteStartObject("features");
            if (features.Ble is { } ble)
            {
                writer.WriteBoolean("ble", ble);
            }

            if (features.QrCode is { } qrCode)
            {
                writer.WriteBoolean("qrCode", qrCode);
            }

            writer.WriteEndObject();
        }

        if (app.PermanentLinkPattern is not null)
        {
            writer.WriteString("permanentLinkPattern", app.PermanentLinkPattern);
        }

        if (app.Scope is { } scope)
        {
            writer.WriteStartArray("scope");
            foreach (var item in scope)
            {
                writer.WriteStringValue(item);
            }

            writer.WriteEndArray();
        }

        if (app.BotPrompt is not null)
        {
            writer.WriteString("botPrompt", app.BotPrompt);
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// 建立帶 Login channel 權杖的請求。
    /// Builds a request carrying the Login channel's token.
    /// </summary>
    /// <param name="method">HTTP 方法。The HTTP method.</param>
    /// <param name="uri">目標位址。The target address.</param>
    /// <returns>建好的請求。The request.</returns>
    private HttpRequestMessage Authorize(HttpMethod method, string uri) =>
        LineHttp.Bearer(method, uri, Options.ChannelAccessToken);

    /// <summary>
    /// 記錄訊息的定義。
    /// The log message definitions.
    /// </summary>
    private static partial class Log
    {
        /// <summary>
        /// 列出時 LINE 回 404、被當成沒有任何應用程式時的記錄。
        /// Logged when LINE answers 404 on listing and it is read as "no apps".
        /// </summary>
        /// <param name="logger">記錄器。The logger.</param>
        [LoggerMessage(
            EventId = 2200,
            Level = LogLevel.Debug,
            Message = "LINE 回 404,視為這個 channel 沒有任何 LIFF 應用程式。LINE answered 404; treating it as the channel having no LIFF apps.")]
        internal static partial void NoLiffApps(ILogger logger);
    }
}
