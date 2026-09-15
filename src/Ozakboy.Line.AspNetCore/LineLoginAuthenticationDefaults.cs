namespace Ozakboy.Line.AspNetCore;

/// <summary>
/// LINE Login 認證方案的預設值。
/// The defaults of the LINE Login authentication scheme.
/// </summary>
public static class LineLoginAuthenticationDefaults
{
    /// <summary>
    /// 方案名稱。
    /// The scheme name.
    /// </summary>
    /// <remarks>
    /// 同時也是 <c>[Authorize(AuthenticationSchemes = ...)]</c> 與
    /// <c>ChallengeAsync</c> 要用的名稱。
    /// This is also the name used by <c>[Authorize(AuthenticationSchemes = ...)]</c> and by
    /// <c>ChallengeAsync</c>.
    /// </remarks>
    public const string AuthenticationScheme = "Line";

    /// <summary>
    /// 顯示名稱,出現在登入方式選單上。
    /// The display name, shown in a list of sign-in methods.
    /// </summary>
    public const string DisplayName = "LINE";

    /// <summary>
    /// 預設的回呼路徑。
    /// The default callback path.
    /// </summary>
    /// <remarks>
    /// 這個路徑必須<b>逐字</b>登記在 LINE Developers 後台的 Callback URL 欄位(含網域與協定)。
    /// 不一致時使用者會停在 LINE 的錯誤頁,自己的網站一行日誌都不會有。
    /// This path must be registered <b>verbatim</b> — domain and scheme included — in the Callback URL field of
    /// the LINE Developers console. A mismatch leaves the user on LINE's error page, with not one line in the
    /// site's own log.
    /// </remarks>
    public const string CallbackPath = "/signin-line";

    /// <summary>
    /// id_token 的發行者。
    /// The id_token issuer.
    /// </summary>
    public const string Issuer = "https://access.line.me";
}
