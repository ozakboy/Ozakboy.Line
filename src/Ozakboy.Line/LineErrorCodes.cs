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
    /// 快速回覆的按鈕數不在允許範圍內(見 <see cref="LineMessagingLimits.MaxQuickReplyItems"/>)。
    /// The number of quick reply buttons is outside the allowed range; see
    /// <see cref="LineMessagingLimits.MaxQuickReplyItems"/>.
    /// </summary>
    public const string TooManyQuickReplyItems = "line.validation.too_many_quick_reply_items";

    /// <summary>
    /// 範本訊息不成立:動作數或欄數超出 <see cref="LineMessagingLimits"/> 的範圍,或輪播各欄的動作數不一致。
    /// The template message does not hold together: an action or column count outside the range in
    /// <see cref="LineMessagingLimits"/>, or carousel columns with differing action counts.
    /// </summary>
    public const string InvalidTemplate = "line.validation.invalid_template";

    /// <summary>
    /// 圖片地圖不成立:區域數不在 1 到 <see cref="LineMessagingLimits.MaxImagemapActions"/> 之間。
    /// The imagemap does not hold together: the area count is outside 1 to
    /// <see cref="LineMessagingLimits.MaxImagemapActions"/>.
    /// </summary>
    public const string InvalidImagemap = "line.validation.invalid_imagemap";

    /// <summary>
    /// 載入動畫的秒數不是 5 到 60 之間的 5 的倍數。
    /// The loading animation's seconds are not a multiple of 5 between 5 and 60.
    /// </summary>
    public const string InvalidLoadingSeconds = "line.validation.invalid_loading_seconds";

    /// <summary>
    /// 分頁參數超出端點允許的範圍(好友清單的 <c>limit</c>、受眾清單的 <c>page</c> / <c>size</c>)。
    /// A paging argument is outside the endpoint's range: the follower list's <c>limit</c>, or the audience list's
    /// <c>page</c> / <c>size</c>.
    /// </summary>
    public const string InvalidPageSize = "line.validation.invalid_page_size";

    /// <summary>
    /// 一次加入受眾的人數不在 1 到 <see cref="LineMessagingLimits.MaxAudienceMembersPerRequest"/> 之間。
    /// The number of users added to an audience in one request is outside 1 to
    /// <see cref="LineMessagingLimits.MaxAudienceMembersPerRequest"/>.
    /// </summary>
    public const string TooManyAudienceMembers = "line.validation.too_many_audience_members";

    /// <summary>
    /// 範本裡有佔位符沒有對應的值。
    /// The template has placeholders with no value supplied.
    /// </summary>
    /// <remarks>
    /// 錯誤訊息會列出缺了哪幾個變數 —— 少給一個變數的結果是把 <c>{{name}}</c> 原樣送到使用者眼前,
    /// 那種錯誤看得到卻查不出是哪一層漏掉的。
    /// The message names the variables that were missing. Leaving one out means sending <c>{{name}}</c> verbatim
    /// to a user: visible on screen, with nothing to say which layer dropped it.
    /// </remarks>
    public const string MissingTemplateVariables = "line.validation.missing_template_variables";

    /// <summary>
    /// 自動回覆規則本身不成立:樣式空白、正規表示式編不過,或回覆內容不是 1 到 5 則訊息。
    /// The auto reply rule does not hold together: a blank pattern, a regular expression that will not compile,
    /// or a reply body that is not between one and five messages.
    /// </summary>
    /// <remarks>
    /// 只用一個代碼而不是三個:呼叫端對這三種情形的處置相同(把規則退回給編輯的人),
    /// 差別只在顯示哪一句話,而那句話在錯誤訊息裡。
    /// One code rather than three: a caller does the same thing in all three cases — hand the rule back to
    /// whoever is editing it — and the only difference is which sentence to show, which the message carries.
    /// </remarks>
    public const string InvalidAutoReplyRule = "line.validation.invalid_auto_reply_rule";

    /// <summary>
    /// 待發項目不存在。
    /// The outbox item does not exist.
    /// </summary>
    public const string McpOutboxNotFound = "line.mcp.outbox_not_found";

    /// <summary>
    /// 待發項目目前的狀態不允許這個操作(例如已送出的項目不能再取消)。
    /// The outbox item's current status does not allow this operation — an item already sent cannot be cancelled,
    /// for one.
    /// </summary>
    public const string McpOutboxInvalidStatus = "line.mcp.outbox_invalid_status";

    /// <summary>
    /// 圖文選單圖片抓取失敗:位址連不上、型別不是 JPEG / PNG,或超過大小上限。
    /// Fetching the rich menu image failed: the address could not be reached, the type was neither JPEG nor PNG,
    /// or it exceeded the size cap.
    /// </summary>
    public const string McpImageFetchFailed = "line.mcp.image_fetch_failed";

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
