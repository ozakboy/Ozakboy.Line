namespace Ozakboy.Line;

/// <summary>
/// 本套件回傳的錯誤代碼。
/// The error codes this package returns.
/// </summary>
/// <remarks>
/// <para>
/// 這些字串是<b>公開契約</b>:呼叫端會拿它們做分支與統計,發佈之後不再更動。要表達新的失敗情況請加新代碼,
/// 不要改既有代碼的字面值或語意 —— 改掉會讓下游的分支安靜地失效,而編譯器一個字都不會說。
/// These strings are a <b>public contract</b>: callers branch and aggregate on them, and they do not change once
/// published. Express a new failure with a new code rather than altering an existing one's text or meaning —
/// doing so silently breaks downstream branching, with nothing from the compiler to say so.
/// </para>
/// <para>
/// 錯誤分類(<see cref="Ozakboy.Core.Abstractions.ErrorCategory"/>)另有其事:HTTP 層來的失敗會保留
/// <c>Ozakboy.Http</c> 判定的分類,404 仍是 NotFound、429 仍是 RateLimited,所以「是否值得重試」的判斷
/// 不必去讀代碼字串。
/// The category (<see cref="Ozakboy.Core.Abstractions.ErrorCategory"/>) is a separate matter: failures arriving
/// from the HTTP layer keep the category <c>Ozakboy.Http</c> assigned, so a 404 stays NotFound and a 429 stays
/// RateLimited and "is this worth retrying" never has to parse a code string.
/// </para>
/// </remarks>
public static class LineErrorCodes
{
    /// <summary>
    /// 憑證未設定:沒有 channel id / channel secret / channel access token,請求根本沒有送出。
    /// The credentials are not configured — no channel id, channel secret, or channel access token — and no
    /// request was sent.
    /// </summary>
    public const string NotConfigured = "line.not_configured";

    /// <summary>
    /// 收件者人數超過單次上限。
    /// More recipients than a single call allows.
    /// </summary>
    public const string TooManyRecipients = "line.validation.too_many_recipients";

    /// <summary>
    /// 訊息則數不在允許範圍內。
    /// The number of messages is outside the allowed range.
    /// </summary>
    public const string TooManyMessages = "line.validation.too_many_messages";

    /// <summary>
    /// 呼叫端給的 JSON 無法解析。
    /// The JSON supplied by the caller could not be parsed.
    /// </summary>
    public const string InvalidJson = "line.validation.invalid_json";

    /// <summary>
    /// LINE 回了非 2xx 的狀態碼。
    /// LINE answered with a non-2xx status code.
    /// </summary>
    /// <remarks>
    /// 錯誤訊息會是 <c>LINE API {狀態碼}: {LINE 給的訊息}</c>,LINE 原本的 <c>message</c> 與 <c>details</c>
    /// 放在錯誤資料的 <see cref="LineErrorDataKeys.LineMessage"/> 與 <see cref="LineErrorDataKeys.LineDetails"/>,
    /// <c>Ozakboy.Http</c> 原有的 <c>statusCode</c>、<c>body</c>、<c>retryAfterSeconds</c> 全部保留。
    /// The message reads <c>LINE API {status}: {LINE's message}</c>. LINE's own <c>message</c> and
    /// <c>details</c> are placed in the error data under <see cref="LineErrorDataKeys.LineMessage"/> and
    /// <see cref="LineErrorDataKeys.LineDetails"/>, and <c>Ozakboy.Http</c>'s <c>statusCode</c>, <c>body</c>,
    /// and <c>retryAfterSeconds</c> are all preserved.
    /// </remarks>
    public const string ApiError = "line.api.error";

    /// <summary>
    /// LINE 回了 2xx,但內容不是本套件認得的形狀。
    /// LINE answered 2xx with a body this package could not read.
    /// </summary>
    public const string ApiInvalidResponse = "line.api.invalid_response";

    /// <summary>
    /// id_token 不是三段式 JWT,或任一段不是合法的 base64url。
    /// The id_token is not a three-part JWT, or one of its parts is not valid base64url.
    /// </summary>
    public const string IdTokenInvalidFormat = "line.id_token.invalid_format";

    /// <summary>
    /// id_token 的簽章演算法不是 HS256,本地驗證無法進行。
    /// The id_token is signed with something other than HS256, which local validation cannot verify.
    /// </summary>
    public const string IdTokenUnsupportedAlgorithm = "line.id_token.unsupported_algorithm";

    /// <summary>
    /// id_token 的簽章與 channel secret 算出來的不符。
    /// The id_token's signature does not match the one computed from the channel secret.
    /// </summary>
    public const string IdTokenInvalidSignature = "line.id_token.invalid_signature";

    /// <summary>
    /// id_token 的發行者不是 LINE。
    /// The id_token was not issued by LINE.
    /// </summary>
    public const string IdTokenInvalidIssuer = "line.id_token.invalid_issuer";

    /// <summary>
    /// id_token 不是發給這個 channel id 的。
    /// The id_token was not issued for this channel id.
    /// </summary>
    public const string IdTokenInvalidAudience = "line.id_token.invalid_audience";

    /// <summary>
    /// id_token 已過期。
    /// The id_token has expired.
    /// </summary>
    public const string IdTokenExpired = "line.id_token.expired";

    /// <summary>
    /// id_token 的 nonce 與這次登入送出的不符。
    /// The id_token's nonce does not match the one sent with this sign-in.
    /// </summary>
    public const string IdTokenNonceMismatch = "line.id_token.nonce_mismatch";

    /// <summary>
    /// webhook 請求的簽章驗不過,內容不可信。
    /// The webhook request's signature did not verify and its body cannot be trusted.
    /// </summary>
    public const string WebhookInvalidSignature = "line.webhook.invalid_signature";

    /// <summary>
    /// webhook 請求的內容無法解析成事件。
    /// The webhook request's body could not be parsed into events.
    /// </summary>
    public const string WebhookInvalidPayload = "line.webhook.invalid_payload";
}
