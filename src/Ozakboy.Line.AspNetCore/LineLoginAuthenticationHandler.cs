using System.Buffers.Text;
using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ozakboy.Line.Login;

namespace Ozakboy.Line.AspNetCore;

/// <summary>
/// LINE Login 的認證處理器。
/// The LINE Login authentication handler.
/// </summary>
/// <remarks>
/// 建立在官方的 <see cref="OAuthHandler{TOptions}"/> 之上:狀態值的保護、PKCE、關聯 cookie、
/// 授權碼換權杖這些都由基底類別處理,這裡只補 LINE 特有的部分 —— 加好友引導、nonce、個人檔案、
/// 好友狀態與 id_token 的本地驗證。
/// Built on the first-party <see cref="OAuthHandler{TOptions}"/>: state protection, PKCE, the correlation cookie,
/// and the code-for-token exchange are the base class's work, and only LINE's own parts are added here — the
/// add-friend prompt, the nonce, the profile, the friendship status, and local id_token validation.
/// </remarks>
public sealed class LineLoginAuthenticationHandler : OAuthHandler<LineLoginAuthenticationOptions>
{
    /// <summary>
    /// 存放 nonce 的認證屬性鍵。
    /// The authentication-properties key holding the nonce.
    /// </summary>
    private const string NonceKey = "line.nonce";

    /// <summary>
    /// 建立處理器。
    /// Creates the handler.
    /// </summary>
    /// <param name="options">設定監看器。The options monitor.</param>
    /// <param name="logger">記錄器工廠。The logger factory.</param>
    /// <param name="encoder">網址編碼器。The URL encoder.</param>
    public LineLoginAuthenticationHandler(
        IOptionsMonitor<LineLoginAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    /// <summary>
    /// 產生導向 LINE 的授權網址,並附上 LINE 特有的參數。
    /// Builds the authorization URL to LINE, with its own parameters appended.
    /// </summary>
    /// <param name="properties">認證屬性。The authentication properties.</param>
    /// <param name="redirectUri">
    /// 由基底類別從當前請求推導出來的回呼位址;設了
    /// <see cref="LineLoginAuthenticationOptions.PublicOrigin"/> 時會被換掉。
    /// The callback address the base class derived from the incoming request, replaced when
    /// <see cref="LineLoginAuthenticationOptions.PublicOrigin"/> is set.
    /// </param>
    /// <returns>完整的授權網址。The complete authorization URL.</returns>
    protected override string BuildChallengeUrl(AuthenticationProperties properties, string redirectUri)
    {
        ArgumentNullException.ThrowIfNull(properties);

        // 回呼網址在交給基底類別之前就換掉。基底類別把這個值原樣寫進授權網址的 redirect_uri,
        // 之後再改就來不及了 —— 使用者的瀏覽器已經帶著舊的值出發了。
        // The callback address is swapped before the base class sees it: the base writes this value into the
        // authorization URL's redirect_uri as it stands, and changing it afterwards is too late — the user's
        // browser has already left with the old one.
        redirectUri = Options.ResolveRedirectUri(Request.PathBase.Value ?? string.Empty, redirectUri);

        var nonce = CreateNonce();

        // nonce 必須在呼叫 base 之前放進 properties。基底類別在 BuildChallengeUrl 裡就把 properties
        // 序列化並加密成 state 參數,之後再放進去的東西不會跟著出門,回呼時自然也拿不回來。
        // The nonce has to be in properties before base is called: the base class serialises and encrypts
        // properties into the state parameter inside BuildChallengeUrl, so anything added afterwards never
        // leaves, and is simply not there on the callback.
        properties.Items[NonceKey] = nonce;

        var url = base.BuildChallengeUrl(properties, redirectUri);
        url = QueryHelpers.AddQueryString(url, "nonce", nonce);

        if (Options.BotPrompt != LineBotPrompt.None)
        {
            url = QueryHelpers.AddQueryString(url, "bot_prompt", ToParameterValue(Options.BotPrompt));
        }

        if (Options.ForceConsent)
        {
            url = QueryHelpers.AddQueryString(url, "prompt", "consent");
        }

        if (!string.IsNullOrWhiteSpace(Options.UiLocales))
        {
            url = QueryHelpers.AddQueryString(url, "ui_locales", Options.UiLocales);
        }

        return url;
    }

    /// <summary>
    /// 以授權碼換權杖,並確保 <c>redirect_uri</c> 與授權階段送出的那一個完全相同。
    /// Exchanges the authorization code for tokens, with the same <c>redirect_uri</c> that went out at the
    /// authorisation step.
    /// </summary>
    /// <param name="context">換權杖的內容。The code exchange context.</param>
    /// <returns>權杖端點的回應。The token endpoint's response.</returns>
    /// <remarks>
    /// OAuth 要求兩個階段的 <c>redirect_uri</c> 逐字相同。只改授權階段而不改這裡,使用者會順利授權完畢,
    /// 卻在換權杖那一步收到 <c>invalid_grant</c> —— 那比「一開始就被擋下」難查得多,
    /// 因為畫面上看得到的每一步都成功了。
    /// OAuth requires the two steps' <c>redirect_uri</c> values to be identical. Changing only the authorisation
    /// step leaves the user authorising successfully and then hitting <c>invalid_grant</c> at the exchange —
    /// harder to track down than being refused up front, because every visible step succeeded.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    protected override Task<OAuthTokenResponse> ExchangeCodeAsync(OAuthCodeExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(Options.PublicOrigin))
        {
            return base.ExchangeCodeAsync(context);
        }

        var redirectUri = Options.ResolveRedirectUri(Request.PathBase.Value ?? string.Empty, context.RedirectUri);
        return base.ExchangeCodeAsync(new OAuthCodeExchangeContext(context.Properties, context.Code, redirectUri));
    }

    /// <summary>
    /// 以權杖取得使用者資料並組出登入票證。
    /// Reads the user's data with the tokens and assembles the authentication ticket.
    /// </summary>
    /// <param name="identity">身分。The identity.</param>
    /// <param name="properties">認證屬性。The authentication properties.</param>
    /// <param name="tokens">權杖端點的回應。The token endpoint's response.</param>
    /// <returns>登入票證。The authentication ticket.</returns>
    /// <exception cref="AuthenticationFailureException">
    /// 個人檔案取不到、或 id_token 驗證不過時擲出;基底類別會把它轉成一次失敗的遠端認證。
    /// Thrown when the profile cannot be read or the id_token does not verify; the base class turns it into a
    /// failed remote authentication.
    /// </exception>
    protected override async Task<AuthenticationTicket> CreateTicketAsync(
        ClaimsIdentity identity,
        AuthenticationProperties properties,
        OAuthTokenResponse tokens)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(tokens);

        using var payload = await ReadProfileAsync(tokens.AccessToken!).ConfigureAwait(false);

        var context = new OAuthCreatingTicketContext(
            new ClaimsPrincipal(identity),
            properties,
            Context,
            Scheme,
            Options,
            Backchannel,
            tokens,
            payload.RootElement);

        context.RunClaimActions();

        if (Options.QueryFriendship)
        {
            await AddFriendshipClaimAsync(identity, tokens.AccessToken!).ConfigureAwait(false);
        }

        if (Options.ValidateIdToken)
        {
            ValidateIdToken(identity, properties, tokens);
        }

        await Events.CreatingTicket(context).ConfigureAwait(false);
        return new AuthenticationTicket(context.Principal!, context.Properties, Scheme.Name);
    }

    /// <summary>
    /// 呼叫 LINE 的個人檔案端點。
    /// Calls LINE's profile endpoint.
    /// </summary>
    /// <param name="accessToken">存取權杖。The access token.</param>
    /// <returns>個人檔案的 JSON。The profile as JSON.</returns>
    /// <exception cref="AuthenticationFailureException">
    /// LINE 回非 2xx 時擲出。Thrown when LINE answers with a non-2xx status.
    /// </exception>
    private async Task<JsonDocument> ReadProfileAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Options.UserInformationEndpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await Backchannel
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, Context.RequestAborted)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            // 訊息只講狀態碼,不回述回應內容:那份內容可能含有使用者資料,而這個例外會被寫進日誌。
            // The message carries only the status code and never echoes the body, which can contain user data and
            // would end up in a log through this exception.
            throw new AuthenticationFailureException(string.Create(
                CultureInfo.InvariantCulture,
                $"向 LINE 取得使用者個人檔案失敗,狀態碼 {(int)response.StatusCode}。Reading the user profile from LINE failed with status {(int)response.StatusCode}."));
        }

        var body = await response.Content.ReadAsStringAsync(Context.RequestAborted).ConfigureAwait(false);
        return JsonDocument.Parse(body);
    }

    /// <summary>
    /// 查詢好友狀態並加上宣告;查不到時不加宣告,也不讓登入失敗。
    /// Looks the friendship status up and adds the claim; when it cannot be read, no claim is added and the
    /// sign-in still succeeds.
    /// </summary>
    /// <param name="identity">身分。The identity.</param>
    /// <param name="accessToken">存取權杖。The access token.</param>
    private async Task AddFriendshipClaimAsync(ClaimsIdentity identity, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LineEndpoints.FriendshipStatus);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await Backchannel
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, Context.RequestAborted)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(Context.RequestAborted).ConfigureAwait(false);

        bool friendFlag;
        try
        {
            using var document = JsonDocument.Parse(body);
            friendFlag = document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("friendFlag", out var flag)
                && flag.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            return;
        }

        identity.AddClaim(new Claim(
            LineClaimTypes.IsFriend,
            friendFlag ? "true" : "false",
            ClaimValueTypes.Boolean,
            LineLoginAuthenticationDefaults.Issuer));
    }

    /// <summary>
    /// 在本地驗證 id_token,並在通過時補上電子郵件宣告。
    /// Verifies the id_token locally and adds the email claim when it verifies.
    /// </summary>
    /// <param name="identity">身分。The identity.</param>
    /// <param name="properties">認證屬性(nonce 從這裡取)。The authentication properties, which carry the nonce.</param>
    /// <param name="tokens">權杖端點的回應。The token endpoint's response.</param>
    /// <exception cref="AuthenticationFailureException">
    /// 驗證不過時擲出。Thrown when the token does not verify.
    /// </exception>
    private void ValidateIdToken(ClaimsIdentity identity, AuthenticationProperties properties, OAuthTokenResponse tokens)
    {
        if (tokens.Response is null
            || tokens.Response.RootElement.ValueKind != JsonValueKind.Object
            || !tokens.Response.RootElement.TryGetProperty("id_token", out var idTokenElement)
            || idTokenElement.ValueKind != JsonValueKind.String)
        {
            return;
        }

        var idToken = idTokenElement.GetString();
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return;
        }

        properties.Items.TryGetValue(NonceKey, out var nonce);

        var validated = LineIdTokenValidator.Validate(
            idToken,
            Options.ClientId,
            Options.ClientSecret,
            nonce);

        if (validated.IsFailure)
        {
            // 這裡擲出例外而不是默默略過。id_token 驗不過只有兩種可能 —— token 被動過手腳,
            // 或是設定與核發時不一致 —— 而繼續完成登入,等於在這兩種情況下都發出一張有效的身分。
            // An exception rather than a quiet skip. An id_token that does not verify means either tampering or
            // settings that differ from the ones it was issued under, and finishing the sign-in would hand out a
            // valid identity in both cases.
            throw new AuthenticationFailureException(
                $"LINE 的 id_token 驗證失敗({validated.Error.Code})。Validation of LINE's id_token failed ({validated.Error.Code}).");
        }

        var payload = validated.GetValueOrThrow();
        if (!string.IsNullOrWhiteSpace(payload.Email))
        {
            identity.AddClaim(new Claim(
                ClaimTypes.Email,
                payload.Email,
                ClaimValueTypes.String,
                LineLoginAuthenticationDefaults.Issuer));
        }
    }

    /// <summary>
    /// 產生一個一次性隨機值。
    /// Creates a one-time random value.
    /// </summary>
    /// <returns>base64url 編碼的 nonce。The nonce in base64url.</returns>
    private static string CreateNonce() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));

    /// <summary>
    /// 把 <see cref="LineBotPrompt"/> 轉成 LINE 認得的參數值。
    /// Converts a <see cref="LineBotPrompt"/> into the parameter value LINE expects.
    /// </summary>
    /// <param name="prompt">列舉值。The enum value.</param>
    /// <returns>參數值。The parameter value.</returns>
    private static string ToParameterValue(LineBotPrompt prompt) => prompt switch
    {
        LineBotPrompt.Normal => "normal",
        LineBotPrompt.Aggressive => "aggressive",
        _ => "none",
    };
}
