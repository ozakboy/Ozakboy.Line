using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ozakboy.Core.Abstractions;
using Ozakboy.Http;

namespace Ozakboy.Line.Login;

/// <summary>
/// <see cref="ILineLoginClient"/> 的實作。
/// The implementation of <see cref="ILineLoginClient"/>.
/// </summary>
/// <remarks>
/// HTTP 一律經由 <see cref="HttpPipelineClient"/>:重試、逾時與日誌遮罩都由那條管線負責,
/// 這個型別只管 LINE 的協定細節。測試時直接 <c>new HttpPipelineClient(new HttpClient(handler))</c>
/// 就能接上假的處理器。
/// Every HTTP call goes through <see cref="HttpPipelineClient"/>: retries, timeouts, and log masking belong to
/// that pipeline, and this type deals only with LINE's protocol. A test can attach a fake handler with
/// <c>new HttpPipelineClient(new HttpClient(handler))</c>.
/// </remarks>
public sealed partial class LineLoginClient : ILineLoginClient
{
    private readonly HttpPipelineClient _http;
    private readonly IOptions<LineLoginOptions> _options;
    private readonly ILogger<LineLoginClient>? _logger;

    /// <summary>
    /// 建立用戶端。
    /// Creates the client.
    /// </summary>
    /// <param name="http">管線用戶端。The pipeline client.</param>
    /// <param name="options">Login channel 設定。The Login channel settings.</param>
    /// <param name="logger">記錄器,可為 <see langword="null"/>。The logger, which may be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="http"/> 或 <paramref name="options"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="http"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    public LineLoginClient(HttpPipelineClient http, IOptions<LineLoginOptions> options, ILogger<LineLoginClient>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(options);

        _http = http;
        _options = options;
        _logger = logger;
    }

    private LineLoginOptions Options => _options.Value;

    /// <inheritdoc />
    public string BuildAuthorizationUrl(LineAuthorizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RedirectUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.State);

        var options = Options;
        if (!options.IsConfigured)
        {
            throw new InvalidOperationException(
                "LINE Login 的 ChannelId 與 ChannelSecret 都必須設定才能產生授權網址。Both ChannelId and ChannelSecret must be set before an authorization URL can be built.");
        }

        var scopes = request.Scopes ?? options.Scopes;
        var builder = new StringBuilder(LineEndpoints.Authorization);
        builder.Append("?response_type=code");
        Append(builder, "client_id", options.ChannelId);
        Append(builder, "redirect_uri", request.RedirectUri);
        Append(builder, "state", request.State);
        Append(builder, "scope", string.Join(' ', scopes));

        if (!string.IsNullOrWhiteSpace(request.Nonce))
        {
            Append(builder, "nonce", request.Nonce);
        }

        if (request.ForceConsent)
        {
            Append(builder, "prompt", "consent");
        }

        var botPrompt = request.BotPrompt ?? options.BotPrompt;
        if (botPrompt != LineBotPrompt.None)
        {
            Append(builder, "bot_prompt", ToParameterValue(botPrompt));
        }

        if (!string.IsNullOrWhiteSpace(request.UiLocales))
        {
            Append(builder, "ui_locales", request.UiLocales);
        }

        if (!string.IsNullOrWhiteSpace(request.CodeChallenge))
        {
            Append(builder, "code_challenge", request.CodeChallenge);
            Append(builder, "code_challenge_method", "S256");
        }

        if (request.DisableAutoLogin)
        {
            Append(builder, "disable_auto_login", "true");
        }

        if (request.DisableIosAutoLogin)
        {
            Append(builder, "disable_ios_auto_login", "true");
        }

        return builder.ToString();
    }

    /// <inheritdoc />
    public Task<Result<LineTokenResponse>> ExchangeCodeAsync(
        string code,
        string redirectUri,
        string? codeVerifier = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(redirectUri);

        var options = Options;
        if (!options.IsConfigured)
        {
            return Task.FromResult(NotConfigured<LineTokenResponse>());
        }

        var form = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "authorization_code"),
            new("code", code),
            new("redirect_uri", redirectUri),
            new("client_id", options.ChannelId),
            new("client_secret", options.ChannelSecret),
        };

        if (!string.IsNullOrWhiteSpace(codeVerifier))
        {
            form.Add(new KeyValuePair<string, string>("code_verifier", codeVerifier));
        }

        return LineHttp.SendForJsonAsync<LineTokenResponse>(_http, Form(LineEndpoints.Token, form), cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<LineTokenResponse>> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);

        var options = Options;
        if (!options.IsConfigured)
        {
            return Task.FromResult(NotConfigured<LineTokenResponse>());
        }

        var form = new KeyValuePair<string, string>[]
        {
            new("grant_type", "refresh_token"),
            new("refresh_token", refreshToken),
            new("client_id", options.ChannelId),
            new("client_secret", options.ChannelSecret),
        };

        return LineHttp.SendForJsonAsync<LineTokenResponse>(_http, Form(LineEndpoints.Token, form), cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result> RevokeAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        var options = Options;
        if (!options.IsConfigured)
        {
            return Task.FromResult(Result.Failure(NotConfiguredError()));
        }

        var form = new KeyValuePair<string, string>[]
        {
            new("access_token", accessToken),
            new("client_id", options.ChannelId),
            new("client_secret", options.ChannelSecret),
        };

        return LineHttp.SendAsync(_http, Form(LineEndpoints.Revoke, form), cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<LineAccessTokenInfo>> VerifyAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        if (!Options.IsConfigured)
        {
            return Task.FromResult(NotConfigured<LineAccessTokenInfo>());
        }

        var uri = $"{LineEndpoints.VerifyAccessToken}?access_token={Uri.EscapeDataString(accessToken)}";
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(uri, UriKind.Absolute));
        return LineHttp.SendForJsonAsync<LineAccessTokenInfo>(_http, request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<LineIdTokenPayload>> VerifyIdTokenAsync(
        string idToken,
        string? nonce = null,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idToken);

        var options = Options;
        if (!options.IsConfigured)
        {
            return NotConfigured<LineIdTokenPayload>();
        }

        var form = new List<KeyValuePair<string, string>>
        {
            new("id_token", idToken),
            new("client_id", options.ChannelId),
        };

        if (!string.IsNullOrWhiteSpace(nonce))
        {
            form.Add(new KeyValuePair<string, string>("nonce", nonce));
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            form.Add(new KeyValuePair<string, string>("user_id", userId));
        }

        var claims = await LineHttp
            .SendForJsonAsync<LineIdTokenClaims>(_http, Form(LineEndpoints.VerifyIdToken, form), cancellationToken)
            .ConfigureAwait(false);

        return claims.IsFailure ? claims.ToFailure<LineIdTokenPayload>() : Result.Success(claims.GetValueOrThrow().ToPayload());
    }

    /// <inheritdoc />
    public Task<Result<LineUserProfile>> GetProfileAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        return !Options.IsConfigured
            ? Task.FromResult(NotConfigured<LineUserProfile>())
            : LineHttp.SendForJsonAsync<LineUserProfile>(
                _http,
                LineHttp.Bearer(HttpMethod.Get, LineEndpoints.Profile, accessToken),
                cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<LineUserInfo>> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        return !Options.IsConfigured
            ? Task.FromResult(NotConfigured<LineUserInfo>())
            : LineHttp.SendForJsonAsync<LineUserInfo>(
                _http,
                LineHttp.Bearer(HttpMethod.Get, LineEndpoints.UserInfo, accessToken),
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> GetFriendshipStatusAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        if (!Options.IsConfigured)
        {
            return NotConfigured<bool>();
        }

        var status = await LineHttp
            .SendForJsonAsync<LineFriendshipStatus>(
                _http,
                LineHttp.Bearer(HttpMethod.Get, LineEndpoints.FriendshipStatus, accessToken),
                cancellationToken)
            .ConfigureAwait(false);

        return status.IsFailure ? status.ToFailure<bool>() : Result.Success(status.GetValueOrThrow().FriendFlag);
    }

    /// <inheritdoc />
    public async Task<Result<LineLoginResult>> CompleteLoginAsync(
        string code,
        string redirectUri,
        string? codeVerifier = null,
        string? nonce = null,
        CancellationToken cancellationToken = default)
    {
        var tokens = await ExchangeCodeAsync(code, redirectUri, codeVerifier, cancellationToken).ConfigureAwait(false);
        if (tokens.IsFailure)
        {
            return tokens.ToFailure<LineLoginResult>();
        }

        var tokenResponse = tokens.GetValueOrThrow();

        var profile = await GetProfileAsync(tokenResponse.AccessToken, cancellationToken).ConfigureAwait(false);
        if (profile.IsFailure)
        {
            return profile.ToFailure<LineLoginResult>();
        }

        // 好友狀態查不到不算登入失敗:Login channel 沒設定 Linked OA 時 LINE 就是回 4xx,
        // 而那是頻道設定的事實,不是這次登入出了問題。
        // A friendship lookup that fails is not a failed sign-in: without a linked official account LINE simply
        // answers 4xx, which is a fact about the channel rather than a problem with this sign-in.
        var friendship = await GetFriendshipStatusAsync(tokenResponse.AccessToken, cancellationToken).ConfigureAwait(false);
        bool? isFriend = null;
        if (friendship.IsSuccess)
        {
            isFriend = friendship.GetValueOrThrow();
        }
        else if (_logger is not null)
        {
            Log.FriendshipUnavailable(_logger, friendship.Error.Code);
        }

        LineIdTokenPayload? idToken = null;
        if (!string.IsNullOrWhiteSpace(tokenResponse.IdToken))
        {
            var options = Options;
            var validated = LineIdTokenValidator.Validate(
                tokenResponse.IdToken,
                options.ChannelId,
                options.ChannelSecret,
                nonce);

            // 這裡刻意讓整個登入失敗。id_token 驗不過只有兩種可能:傳輸途中被動過手腳,
            // 或是 channel 設定與核發時不一致 —— 兩種都不該放行,「先讓他登入再說」正是不該有的選擇。
            // The whole sign-in fails here on purpose. An id_token that does not verify means either tampering in
            // transit or channel settings that differ from the ones it was issued under, and neither should be
            // let through; "sign them in anyway" is exactly the wrong call.
            if (validated.IsFailure)
            {
                return validated.ToFailure<LineLoginResult>();
            }

            idToken = validated.GetValueOrThrow();
        }

        return Result.Success(new LineLoginResult
        {
            Tokens = tokenResponse,
            Profile = profile.GetValueOrThrow(),
            IsFriend = isFriend,
            IdToken = idToken,
        });
    }

    /// <summary>
    /// 把 <see cref="LineBotPrompt"/> 轉成 LINE 認得的參數值。
    /// Converts a <see cref="LineBotPrompt"/> into the parameter value LINE expects.
    /// </summary>
    /// <param name="prompt">列舉值。The enum value.</param>
    /// <returns>參數值。The parameter value.</returns>
    /// <remarks>
    /// 明確對映而不是把列舉名轉小寫:轉小寫在多數地區設定下結果相同,但依賴的是格式化行為而不是契約,
    /// 而且列舉改名時不會有任何警告。
    /// An explicit mapping rather than lower-casing the enum name: lower-casing gives the same answer in most
    /// cultures, but it leans on formatting behaviour instead of a contract, and renaming the enum would raise no
    /// warning at all.
    /// </remarks>
    private static string ToParameterValue(LineBotPrompt prompt) => prompt switch
    {
        LineBotPrompt.Normal => "normal",
        LineBotPrompt.Aggressive => "aggressive",
        _ => "none",
    };

    /// <summary>
    /// 附加一個已編碼的 query 參數。
    /// Appends one encoded query parameter.
    /// </summary>
    /// <param name="builder">網址建構器。The URL builder.</param>
    /// <param name="name">參數名。The parameter name.</param>
    /// <param name="value">參數值。The parameter value.</param>
    private static void Append(StringBuilder builder, string name, string value) =>
        builder.Append('&').Append(name).Append('=').Append(Uri.EscapeDataString(value));

    /// <summary>
    /// 建立表單編碼的 POST 請求。
    /// Builds a form-encoded POST request.
    /// </summary>
    /// <param name="uri">目標位址。The target address.</param>
    /// <param name="form">表單欄位。The form fields.</param>
    /// <returns>建好的請求。The request.</returns>
    private static HttpRequestMessage Form(string uri, IEnumerable<KeyValuePair<string, string>> form) =>
        new(HttpMethod.Post, new Uri(uri, UriKind.Absolute))
        {
            Content = new FormUrlEncodedContent(form),
        };

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
        "LINE Login 的 ChannelId 與 ChannelSecret 都必須設定。Both ChannelId and ChannelSecret must be set for LINE Login.");

    /// <summary>
    /// 記錄訊息的定義。
    /// The log message definitions.
    /// </summary>
    private static partial class Log
    {
        /// <summary>
        /// 好友狀態查不到時的記錄。
        /// Logged when the friendship status could not be read.
        /// </summary>
        /// <param name="logger">記錄器。The logger.</param>
        /// <param name="errorCode">失敗代碼。The failure code.</param>
        [LoggerMessage(
            EventId = 2000,
            Level = LogLevel.Debug,
            Message = "LINE 好友狀態查詢失敗({ErrorCode}),登入結果的 IsFriend 記為未知。Friendship lookup failed ({ErrorCode}); IsFriend is recorded as unknown.")]
        internal static partial void FriendshipUnavailable(ILogger logger, string errorCode);
    }
}
