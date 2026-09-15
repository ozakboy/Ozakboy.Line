using Ozakboy.Core.Abstractions;

namespace Ozakboy.Line.Login;

/// <summary>
/// LINE Login 的用戶端。
/// The LINE Login client.
/// </summary>
/// <remarks>
/// 所有預期之內的失敗都以 <see cref="Result"/> 回傳而非擲出例外 —— 使用者取消授權、授權碼過期、
/// 頻道沒設定好,這些在登入流程裡是每天都會發生的事,用 try/catch 表達會讓「忘了處理」變成執行期才知道。
/// Every expected failure comes back as a <see cref="Result"/> rather than an exception: a user declining, an
/// expired authorization code, a misconfigured channel are routine events in a sign-in flow, and expressing them
/// as exceptions turns "forgot to handle it" into a runtime surprise.
/// </remarks>
public interface ILineLoginClient
{
    /// <summary>
    /// 產生要把使用者導過去的授權網址。
    /// Builds the authorization URL to send the user to.
    /// </summary>
    /// <param name="request">這次授權的參數。The parameters for this authorization.</param>
    /// <returns>完整的授權網址。The complete authorization URL.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="request"/> 為 <see langword="null"/> 時擲出。Thrown when <paramref name="request"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <see cref="LineAuthorizationRequest.RedirectUri"/> 或 <see cref="LineAuthorizationRequest.State"/>
    /// 為空白時擲出。Thrown when the redirect URI or the state is blank.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// 頻道未設定時擲出。這個方法不回 <see cref="Result"/>,因此以例外表達 —— 而「連 channel id 都沒有」
    /// 是設定缺陷而非預期失敗,本來就該在開發階段就炸開。
    /// Thrown when the channel is not configured. This method does not return a <see cref="Result"/>, so it
    /// throws — and having no channel id at all is a configuration defect rather than an expected failure,
    /// which should surface during development.
    /// </exception>
    string BuildAuthorizationUrl(LineAuthorizationRequest request);

    /// <summary>
    /// 以授權碼換取權杖。
    /// Exchanges an authorization code for tokens.
    /// </summary>
    /// <param name="code">回呼帶回來的授權碼。The authorization code from the callback.</param>
    /// <param name="redirectUri">與授權請求完全相同的導回位址。The same redirect URI the authorization request used.</param>
    /// <param name="codeVerifier">PKCE 的 code verifier;沒有用 PKCE 時為 <see langword="null"/>。The PKCE code verifier, or <see langword="null"/> when PKCE was not used.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>權杖內容。The tokens.</returns>
    Task<Result<LineTokenResponse>> ExchangeCodeAsync(
        string code,
        string redirectUri,
        string? codeVerifier = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 以 refresh token 續期。
    /// Refreshes with a refresh token.
    /// </summary>
    /// <param name="refreshToken">先前拿到的 refresh token。The refresh token obtained earlier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>新的權杖內容,含新的 refresh token。The new tokens, including a new refresh token.</returns>
    Task<Result<LineTokenResponse>> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// 撤銷存取權杖。
    /// Revokes an access token.
    /// </summary>
    /// <param name="accessToken">要撤銷的權杖。The token to revoke.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>成功或失敗。Success or failure.</returns>
    Task<Result> RevokeAsync(string accessToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// 驗證存取權杖是否仍然有效。
    /// Verifies that an access token is still valid.
    /// </summary>
    /// <param name="accessToken">要驗證的權杖。The token to verify.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>權杖的權限範圍、所屬 channel 與剩餘秒數。The token's scopes, owning channel, and remaining seconds.</returns>
    Task<Result<LineAccessTokenInfo>> VerifyAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// 交給 LINE 遠端驗證 id_token。
    /// Has LINE verify an id_token remotely.
    /// </summary>
    /// <param name="idToken">要驗證的 id_token。The id_token to verify.</param>
    /// <param name="nonce">授權時送出的 nonce;不檢查時為 <see langword="null"/>。The nonce sent at authorization, or <see langword="null"/> to skip the check.</param>
    /// <param name="userId">預期的使用者識別碼;不檢查時為 <see langword="null"/>。The expected user identifier, or <see langword="null"/> to skip the check.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>id_token 的內容。The id_token's contents.</returns>
    /// <remarks>
    /// 一般情況請用 <see cref="LineIdTokenValidator.Validate"/> 在本地驗:同樣安全,但不花一次網路往返,
    /// 也不會在 LINE 那頭暫時故障時把登入流程一起拖下水。這個方法留給簽章演算法不是 HS256 的情況。
    /// Prefer <see cref="LineIdTokenValidator.Validate"/> locally: equally sound, without a network round trip,
    /// and without taking the sign-in down with LINE during a blip at their end. This method is for the case
    /// where the signing algorithm is not HS256.
    /// </remarks>
    Task<Result<LineIdTokenPayload>> VerifyIdTokenAsync(
        string idToken,
        string? nonce = null,
        string? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 以存取權杖取得使用者的個人檔案。
    /// Reads the user's profile with an access token.
    /// </summary>
    /// <param name="accessToken">使用者的存取權杖。The user's access token.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>個人檔案。The profile.</returns>
    Task<Result<LineUserProfile>> GetProfileAsync(string accessToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// 以存取權杖取得 OpenID Connect 的 userinfo。
    /// Reads the OpenID Connect userinfo with an access token.
    /// </summary>
    /// <param name="accessToken">使用者的存取權杖,權限範圍需含 <c>openid</c>。The user's access token, whose scopes must include <c>openid</c>.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>userinfo 的內容。The userinfo contents.</returns>
    Task<Result<LineUserInfo>> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢使用者是否已加官方帳號好友。
    /// Reads whether the user has added the official account as a friend.
    /// </summary>
    /// <param name="accessToken">使用者的存取權杖。The user's access token.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>
    /// 已加好友時為 <see langword="true"/>。Login channel 未設定 Linked OA 時 LINE 回 4xx,
    /// 這個方法照實回失敗 —— 需要「查不到就當作不知道」的語意請用
    /// <see cref="CompleteLoginAsync"/>。
    /// <see langword="true"/> when they have. With no linked official account on the Login channel LINE answers
    /// 4xx and this method reports the failure as it is; for "could not tell" semantics use
    /// <see cref="CompleteLoginAsync"/>.
    /// </returns>
    Task<Result<bool>> GetFriendshipStatusAsync(string accessToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// 走完整個登入流程:換權杖、查個人檔案、查好友狀態、驗 id_token。
    /// Runs the whole sign-in: exchange the code, read the profile, read the friendship status, verify the
    /// id_token.
    /// </summary>
    /// <param name="code">回呼帶回來的授權碼。The authorization code from the callback.</param>
    /// <param name="redirectUri">與授權請求完全相同的導回位址。The same redirect URI the authorization request used.</param>
    /// <param name="codeVerifier">PKCE 的 code verifier;沒有用 PKCE 時為 <see langword="null"/>。The PKCE code verifier, or <see langword="null"/> when PKCE was not used.</param>
    /// <param name="nonce">授權時送出的 nonce;不檢查時為 <see langword="null"/>。The nonce sent at authorization, or <see langword="null"/> to skip the check.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>登入結果。The sign-in result.</returns>
    /// <remarks>
    /// 四個步驟裡只有好友狀態失敗不算數(記為 <see cref="LineLoginResult.IsFriend"/> 為 <see langword="null"/>);
    /// 換權杖、查檔案、驗 id_token 任一失敗,整個登入就失敗。
    /// Of the four steps only the friendship lookup is allowed to fail, recorded as a
    /// <see langword="null"/> <see cref="LineLoginResult.IsFriend"/>; a failure in the exchange, the profile, or
    /// the id_token fails the sign-in.
    /// </remarks>
    Task<Result<LineLoginResult>> CompleteLoginAsync(
        string code,
        string redirectUri,
        string? codeVerifier = null,
        string? nonce = null,
        CancellationToken cancellationToken = default);
}
