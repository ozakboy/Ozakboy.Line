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
    /// 檢查設定是否可用。
    /// Validates the settings.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <see cref="OAuthOptions.ClientId"/> 或 <see cref="OAuthOptions.ClientSecret"/> 未設定時擲出
    /// (由基底類別負責)。
    /// Thrown by the base class when <see cref="OAuthOptions.ClientId"/> or
    /// <see cref="OAuthOptions.ClientSecret"/> is not set.
    /// </exception>
    /// <remarks>
    /// <see cref="OAuthOptions.ClientId"/> 填 Login channel 的 channel id,
    /// <see cref="OAuthOptions.ClientSecret"/> 填它的 channel secret —— 後者同時是 id_token 的簽章金鑰,
    /// 所以本地驗證不需要另外設定。
    /// <see cref="OAuthOptions.ClientId"/> takes the Login channel's channel id and
    /// <see cref="OAuthOptions.ClientSecret"/> its channel secret. The secret is also the id_token signing key,
    /// which is why local validation needs no further configuration.
    /// </remarks>
    public override void Validate() => base.Validate();
}
