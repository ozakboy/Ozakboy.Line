namespace Ozakboy.Line.Login;

/// <summary>
/// id_token 的內容。
/// The contents of an id_token.
/// </summary>
/// <remarks>
/// 這個型別由兩條路徑產生,內容相同:<see cref="LineIdTokenValidator.Validate"/> 在本地驗完簽章後解出來的,
/// 以及 <see cref="ILineLoginClient.VerifyIdTokenAsync"/> 交給 LINE 遠端驗證後回來的。
/// Two paths produce this type with the same contents: <see cref="LineIdTokenValidator.Validate"/> after
/// verifying the signature locally, and <see cref="ILineLoginClient.VerifyIdTokenAsync"/> after LINE has verified
/// it remotely.
/// </remarks>
public sealed class LineIdTokenPayload
{
    /// <summary>
    /// 發行者,LINE 一律是 <c>https://access.line.me</c>。
    /// The issuer, always <c>https://access.line.me</c> from LINE.
    /// </summary>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>
    /// 使用者識別碼,等同於 <see cref="LineUserProfile.UserId"/>。
    /// The user identifier, the same value as <see cref="LineUserProfile.UserId"/>.
    /// </summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>
    /// 對象,也就是這個 token 是發給哪一個 channel id 的。
    /// The audience: the channel id this token was issued for.
    /// </summary>
    public string Audience { get; init; } = string.Empty;

    /// <summary>
    /// 到期時間。
    /// When the token expires.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// 核發時間。
    /// When the token was issued.
    /// </summary>
    public DateTimeOffset IssuedAt { get; init; }

    /// <summary>
    /// 授權請求送出的一次性隨機值;授權時沒有送 nonce 時為 <see langword="null"/>。
    /// The one-time value sent with the authorization request, or <see langword="null"/> when none was sent.
    /// </summary>
    public string? Nonce { get; init; }

    /// <summary>
    /// 使用者實際使用的認證方式,例如 <c>pwd</c>(密碼)、<c>linesso</c>(單一登入)。
    /// The authentication methods the user actually used, such as <c>pwd</c> for a password or <c>linesso</c> for
    /// single sign-on.
    /// </summary>
    public IReadOnlyList<string> Amr { get; init; } = [];

    /// <summary>
    /// 顯示名稱,權限範圍不含 <c>profile</c> 時為 <see langword="null"/>。
    /// The display name, or <see langword="null"/> without the <c>profile</c> scope.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 大頭貼位址,權限範圍不含 <c>profile</c> 或使用者未設定時為 <see langword="null"/>。
    /// The avatar address, or <see langword="null"/> without the <c>profile</c> scope or when the user has not set
    /// one.
    /// </summary>
    public string? Picture { get; init; }

    /// <summary>
    /// 電子郵件,權限範圍不含 <c>email</c> 時為 <see langword="null"/>。
    /// The email address, or <see langword="null"/> without the <c>email</c> scope.
    /// </summary>
    /// <remarks>
    /// <c>email</c> 權限需要向 LINE 另外申請並通過審核;沒過審時登入照常成功,這個欄位就是 <see langword="null"/>。
    /// The <c>email</c> scope has to be applied for and approved by LINE. Without approval sign-in still succeeds
    /// and this field is simply <see langword="null"/>.
    /// </remarks>
    public string? Email { get; init; }
}
