namespace Ozakboy.Line.Login;

/// <summary>
/// 產生授權網址所需的一次性參數。
/// The per-sign-in parameters used to build an authorization URL.
/// </summary>
public sealed class LineAuthorizationRequest
{
    /// <summary>
    /// 授權完成後要導回的位址,必填,且必須與 LINE Developers 後台登記的完全一致。
    /// The address to return to after authorization. Required, and must match the one registered in the LINE
    /// Developers console exactly.
    /// </summary>
    /// <remarks>
    /// 「完全一致」包含結尾斜線與大小寫。不一致時 LINE 在授權頁就擋下來,錯誤訊息只說 redirect_uri 有問題,
    /// 不會說是哪裡不一樣。
    /// "Exactly" includes the trailing slash and the casing. A mismatch is refused on the authorization page,
    /// with an error saying the redirect_uri is wrong but not how.
    /// </remarks>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>
    /// 防 CSRF 的狀態值,必填,回呼時必須驗證它與這次請求送出的相同。
    /// The anti-CSRF state value. Required, and the callback must check it against the one sent with this request.
    /// </summary>
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// 一次性隨機值,會出現在 id_token 裡供比對;不需要時為 <see langword="null"/>。
    /// A one-time random value echoed in the id_token for comparison, or <see langword="null"/> when not needed.
    /// </summary>
    public string? Nonce { get; set; }

    /// <summary>
    /// 這次要求的權限範圍;為 <see langword="null"/> 時採用設定裡的預設值。
    /// The scopes for this request, or <see langword="null"/> to use the ones from the options.
    /// </summary>
    // CA2227:分析器建議集合屬性改成唯讀。這裡需要可設定,而且需要 null 與空集合是兩件不同的事:
    // null 表示「這次不特別指定,用設定裡的預設」,空集合表示「這次一個權限範圍都不要」。
    // 唯讀屬性表達不出前者,呼叫端就得另外記一個布林旗標來說明自己到底有沒有指定過。
#pragma warning disable CA2227
    public IList<string>? Scopes { get; set; }
#pragma warning restore CA2227

    /// <summary>
    /// 這次的加好友引導方式;為 <see langword="null"/> 時採用設定裡的預設值。
    /// The add-friend prompt for this request, or <see langword="null"/> to use the one from the options.
    /// </summary>
    public LineBotPrompt? BotPrompt { get; set; }

    /// <summary>
    /// 是否強制顯示同意畫面(<c>prompt=consent</c>),即使使用者先前已經同意過。
    /// Whether to force the consent screen (<c>prompt=consent</c>) even if the user has consented before.
    /// </summary>
    public bool ForceConsent { get; set; }

    /// <summary>
    /// 授權頁的語言,例如 <c>zh-TW</c>;不指定時由 LINE 決定。
    /// The authorization page's language, such as <c>zh-TW</c>; LINE decides when this is not set.
    /// </summary>
    public string? UiLocales { get; set; }

    /// <summary>
    /// PKCE 的 code challenge;有值時會一併送出 <c>code_challenge_method=S256</c>。
    /// The PKCE code challenge; when set, <c>code_challenge_method=S256</c> is sent with it.
    /// </summary>
    /// <remarks>
    /// 以 <see cref="LinePkce.CreateCodeVerifier"/> 產生 verifier、<see cref="LinePkce.ComputeCodeChallenge"/>
    /// 換出 challenge,verifier 自行保存到回呼時再交給
    /// <see cref="ILineLoginClient.ExchangeCodeAsync"/>。
    /// Produce a verifier with <see cref="LinePkce.CreateCodeVerifier"/>, derive the challenge with
    /// <see cref="LinePkce.ComputeCodeChallenge"/>, and keep the verifier until the callback hands it to
    /// <see cref="ILineLoginClient.ExchangeCodeAsync"/>.
    /// </remarks>
    public string? CodeChallenge { get; set; }

    /// <summary>
    /// 是否停用自動登入(<c>disable_auto_login=true</c>)。
    /// Whether to disable automatic sign-in (<c>disable_auto_login=true</c>).
    /// </summary>
    public bool DisableAutoLogin { get; set; }

    /// <summary>
    /// 是否只在 iOS 上停用自動登入(<c>disable_ios_auto_login=true</c>)。
    /// Whether to disable automatic sign-in on iOS only (<c>disable_ios_auto_login=true</c>).
    /// </summary>
    public bool DisableIosAutoLogin { get; set; }
}
