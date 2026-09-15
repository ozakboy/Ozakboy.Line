using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication;
using Ozakboy.Line.Login;

namespace Ozakboy.Line.AspNetCore;

/// <summary>
/// LINE Login 認證方案的設定。
/// The settings of the LINE Login authentication scheme.
/// </summary>
/// <remarks>
/// 繼承官方的 <see cref="OAuthOptions"/>,因此 <see cref="OAuthOptions.Events"/>、
/// <see cref="OAuthOptions.ClaimActions"/>、<see cref="Microsoft.AspNetCore.Authentication.RemoteAuthenticationOptions.BackchannelHttpHandler"/>
/// 這些既有的擴充點全部照常可用 —— 本套件加的是 LINE 特有的那幾項,不是另起一套。
/// It derives from the first-party <see cref="OAuthOptions"/>, so the existing extension points —
/// <see cref="OAuthOptions.Events"/>, <see cref="OAuthOptions.ClaimActions"/>, and
/// <see cref="Microsoft.AspNetCore.Authentication.RemoteAuthenticationOptions.BackchannelHttpHandler"/> — all
/// work as usual. What this package adds is the handful of LINE-specific settings, not a parallel system.
/// </remarks>
public sealed class LineLoginAuthenticationOptions : OAuthOptions
{
    /// <summary>
    /// 建立設定並填入 LINE 的端點與預設宣告對映。
    /// Creates the settings with LINE's endpoints and the default claim mappings.
    /// </summary>
    public LineLoginAuthenticationOptions()
    {
        CallbackPath = LineLoginAuthenticationDefaults.CallbackPath;
        AuthorizationEndpoint = LineEndpoints.Authorization;
        TokenEndpoint = LineEndpoints.Token;
        UserInformationEndpoint = LineEndpoints.Profile;

        // PKCE 預設開啟。LINE Login 支援 S256,而授權碼在回呼網址上走一趟瀏覽器 ——
        // 有 PKCE 時,攔到授權碼的人拿不出 verifier,也就換不到權杖。
        // PKCE is on by default. LINE Login supports S256, and the authorization code travels through the browser
        // on the callback URL: with PKCE, whoever intercepts it has no verifier and cannot redeem it.
        UsePkce = true;

        Scope.Add("profile");
        Scope.Add("openid");

        ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "userId");
        ClaimActions.MapJsonKey(LineClaimTypes.UserId, "userId");
        ClaimActions.MapJsonKey(ClaimTypes.Name, "displayName");
        ClaimActions.MapJsonKey(LineClaimTypes.Picture, "pictureUrl");
        ClaimActions.MapJsonKey(LineClaimTypes.StatusMessage, "statusMessage");
    }

    /// <summary>
    /// 登入時要不要引導使用者加官方帳號好友。
    /// Whether sign-in should invite the user to add the official account as a friend.
    /// </summary>
    /// <remarks>
    /// 需要 Login channel 設定了 Linked OA 才會生效;沒設定時 LINE 直接忽略,不報錯。
    /// This takes effect only when the Login channel has a linked official account; without one LINE ignores it
    /// silently.
    /// </remarks>
    public LineBotPrompt BotPrompt { get; set; } = LineBotPrompt.None;

    /// <summary>
    /// 登入後是否查詢好友狀態,預設為 <see langword="true"/>。
    /// Whether to look up the friendship status after sign-in; <see langword="true"/> by default.
    /// </summary>
    /// <remarks>
    /// 查得到就加上 <see cref="LineClaimTypes.IsFriend"/> 宣告,查不到就不加,而不是讓登入失敗 ——
    /// Login channel 沒設 Linked OA 時 LINE 一律回 4xx,那是設定的事實,不是這次登入有問題。
    /// A successful lookup adds the <see cref="LineClaimTypes.IsFriend"/> claim; an unsuccessful one adds
    /// nothing rather than failing the sign-in, because a Login channel with no linked official account always
    /// answers 4xx — a fact about the configuration, not a problem with this sign-in.
    /// </remarks>
    public bool QueryFriendship { get; set; } = true;

    /// <summary>
    /// 是否在本地驗證 id_token,預設為 <see langword="true"/>。
    /// Whether to verify the id_token locally; <see langword="true"/> by default.
    /// </summary>
    /// <remarks>
    /// 驗證通過且 token 帶有 email 時,會加上 <see cref="ClaimTypes.Email"/> 宣告。
    /// 驗證<b>不通過時整個登入失敗</b>:id_token 驗不過代表它被動過手腳,或是發給別的 channel 的,
    /// 兩種情況都不該放行。
    /// A token that verifies and carries an email adds a <see cref="ClaimTypes.Email"/> claim. A token that does
    /// <b>not</b> verify fails the sign-in outright: it has either been tampered with or was issued for another
    /// channel, and neither should be let through.
    /// </remarks>
    public bool ValidateIdToken { get; set; } = true;

    /// <summary>
    /// 是否每次都強制顯示同意畫面(<c>prompt=consent</c>)。
    /// Whether to force the consent screen every time (<c>prompt=consent</c>).
    /// </summary>
    public bool ForceConsent { get; set; }

    /// <summary>
    /// 授權頁的語言,例如 <c>zh-TW</c>;不指定時由 LINE 決定。
    /// The authorization page's language, such as <c>zh-TW</c>; LINE decides when this is not set.
    /// </summary>
    public string? UiLocales { get; set; }

    /// <summary>
    /// 這個站台對外的網址(例如 <c>https://example.com</c>);設定之後 <c>redirect_uri</c> 一律以它組出來,
    /// 不再從當前請求推導。
    /// The site's public address, such as <c>https://example.com</c>. Once set, <c>redirect_uri</c> is always
    /// built from it rather than derived from the incoming request.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 這是給<b>反向代理後面</b>的站台用的。預設行為是從當前請求的 scheme 與 host 推導出回呼網址;
    /// 代理沒把 <c>X-Forwarded-Proto</c> 設好(或應用程式沒啟用 <c>UseForwardedHeaders</c>)時,
    /// 應用程式看到的 scheme 是 <c>http</c>,送給 LINE 的 <c>redirect_uri</c> 就成了
    /// <c>http://example.com/...</c>,而 LINE Developers 後台登記的是 <c>https://example.com/...</c>。
    /// This is for a site <b>behind a reverse proxy</b>. By default the callback address is derived from the
    /// incoming request's scheme and host; when the proxy does not set <c>X-Forwarded-Proto</c> — or the
    /// application does not enable <c>UseForwardedHeaders</c> — the scheme the application sees is <c>http</c>,
    /// and the <c>redirect_uri</c> sent to LINE reads <c>http://example.com/...</c> while the LINE Developers
    /// console has <c>https://example.com/...</c> registered.
    /// </para>
    /// <para>
    /// LINE 對 <c>redirect_uri</c> 是<b>逐字比對</b>的,差一個字元就拒絕。症狀是「使用者授權完回來就 400」,
    /// 而錯誤訊息一個字都不會說是哪裡不一樣。明確給一個公開網址就完全繞開這一整類問題,
    /// 不必去確認代理的每一個標頭有沒有設對。
    /// LINE compares <c>redirect_uri</c> <b>verbatim</b> and refuses on a single differing character. The symptom
    /// is a 400 right after the user authorises, with nothing in the error to say what differs. Naming the public
    /// address outright sidesteps the whole class of problem, without auditing every header the proxy sets.
    /// </para>
    /// <para>
    /// 授權階段與換權杖階段用的是<b>同一個</b>值。OAuth 要求兩次的 <c>redirect_uri</c> 完全相同,
    /// 只改其中一邊的結果是換權杖那一步失敗 —— 那比第一種更難查,因為使用者明明已經授權成功了。
    /// The same value is used for the authorisation step and for the token exchange. OAuth requires the two
    /// <c>redirect_uri</c> values to be identical, and changing only one of them fails at the exchange — harder
    /// to track down than the first case, because the user did authorise successfully.
    /// </para>
    /// </remarks>
    public string? PublicOrigin { get; set; }

    /// <summary>
    /// 檢查設定是否可用。
    /// Validates the settings.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <see cref="OAuthOptions.ClientId"/> 或 <see cref="OAuthOptions.ClientSecret"/> 未設定
    /// (由基底類別負責),或 <see cref="PublicOrigin"/> 不是絕對的 http / https 網址時擲出。
    /// Thrown by the base class when <see cref="OAuthOptions.ClientId"/> or
    /// <see cref="OAuthOptions.ClientSecret"/> is not set, and here when <see cref="PublicOrigin"/> is not an
    /// absolute http or https address.
    /// </exception>
    /// <remarks>
    /// <see cref="OAuthOptions.ClientId"/> 填 Login channel 的 channel id,
    /// <see cref="OAuthOptions.ClientSecret"/> 填它的 channel secret —— 後者同時是 id_token 的簽章金鑰,
    /// 所以本地驗證不需要另外設定。
    /// <see cref="OAuthOptions.ClientId"/> takes the Login channel's channel id and
    /// <see cref="OAuthOptions.ClientSecret"/> its channel secret. The secret is also the id_token signing key,
    /// which is why local validation needs no further configuration.
    /// </remarks>
    public override void Validate()
    {
        base.Validate();

        if (string.IsNullOrWhiteSpace(PublicOrigin))
        {
            return;
        }

        // 在啟動時擋下來。一個寫錯的公開網址(少了 scheme、寫成相對路徑)組出來的 redirect_uri 一樣會被
        // LINE 拒絕,而那時的症狀與「沒設這個值」時一模一樣 —— 等於白設定了一次。
        // Refused at startup. A mistyped public address — missing its scheme, or written as a relative path —
        // produces a redirect_uri LINE refuses just the same, with symptoms identical to not having set it at
        // all: the setting would have bought nothing.
        if (!Uri.TryCreate(PublicOrigin, UriKind.Absolute, out var origin)
            || (origin.Scheme != Uri.UriSchemeHttp && origin.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                $"{nameof(PublicOrigin)} 必須是絕對的 http 或 https 網址,例如 https://example.com,目前是「{PublicOrigin}」。{nameof(PublicOrigin)} must be an absolute http or https address such as https://example.com; it is currently \"{PublicOrigin}\".",
                nameof(PublicOrigin));
        }
    }

    /// <summary>
    /// 依 <see cref="PublicOrigin"/> 組出回呼網址;沒有設定時回傳原本推導出來的位址。
    /// Builds the callback address from <see cref="PublicOrigin"/>, or returns the derived address when it is not
    /// set.
    /// </summary>
    /// <param name="pathBase">應用程式的路徑基底。The application's path base.</param>
    /// <param name="derived">原本推導出來的位址。The address that was derived.</param>
    /// <returns>要送給 LINE 的回呼網址。The callback address to send to LINE.</returns>
    internal string ResolveRedirectUri(string pathBase, string derived) =>
        string.IsNullOrWhiteSpace(PublicOrigin)
            ? derived
            : PublicOrigin.TrimEnd('/') + pathBase + CallbackPath;
}
