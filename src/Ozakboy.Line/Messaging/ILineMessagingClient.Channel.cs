using Ozakboy.Core.Abstractions;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// <see cref="ILineMessagingClient"/> 的頻道設定、帳號連結與訊息內容補充端點。
/// The channel-settings, account-linking and message-content additions to <see cref="ILineMessagingClient"/>.
/// </summary>
public partial interface ILineMessagingClient
{
    /// <summary>
    /// 查詢頻道目前設定的 webhook 位址。
    /// Reads the webhook address currently set on the channel.
    /// </summary>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>webhook 位址與是否啟用。The webhook address and whether it is active.</returns>
    Task<Result<LineWebhookEndpointInfo>> GetWebhookEndpointAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 設定頻道的 webhook 位址。
    /// Sets the channel's webhook address.
    /// </summary>
    /// <param name="endpoint">
    /// 新的位址,必須以 <c>https://</c> 開頭且不超過 500 字元;不合規則時回
    /// <see cref="LineErrorCodes.InvalidWebhookEndpoint"/> 失敗且不送出請求。
    /// The new address, which must start with <c>https://</c> and be at most 500 characters; otherwise it fails
    /// with <see cref="LineErrorCodes.InvalidWebhookEndpoint"/> and nothing is sent.
    /// </param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>成功或失敗。Success or failure.</returns>
    Task<Result> SetWebhookEndpointAsync(string endpoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// 要求 LINE 對 webhook 位址送一次測試請求,回報你的伺服器怎麼回應。
    /// Asks LINE to send one test request to the webhook address and reports how your server answered.
    /// </summary>
    /// <param name="endpoint">
    /// 要測試的位址;為 <see langword="null"/> 時測試頻道目前設定的位址。位址的規則同
    /// <see cref="SetWebhookEndpointAsync"/>。
    /// The address to test, or <see langword="null"/> to test the one currently set on the channel. The same
    /// rules as <see cref="SetWebhookEndpointAsync"/> apply.
    /// </param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>
    /// 測試結果;伺服器沒回 2xx 時呼叫本身仍算成功,看 <see cref="LineWebhookTestResult.Success"/>。
    /// The test outcome; when the server does not answer 2xx the call itself still succeeds, so look at
    /// <see cref="LineWebhookTestResult.Success"/>.
    /// </returns>
    Task<Result<LineWebhookTestResult>> TestWebhookEndpointAsync(string? endpoint = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 為使用者簽發帳號連結權杖(有效 10 分鐘,只能用一次)。
    /// Issues an account link token for a user (valid for 10 minutes, usable once).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 帳號連結流程:在收到使用者的連結請求後簽發 link token,把它帶到你自家的登入頁(例如放在網址參數),
    /// 使用者在那裡登入、你的伺服器產生一個 nonce 與該使用者綁定,最後把使用者導回
    /// <c>https://access.line.me/dialog/bot/accountLink?linkToken={linkToken}&amp;nonce={nonce}</c>。
    /// LINE 隨後會送出 account link 事件,事件裡帶著同一個 nonce,你就能把 LINE 使用者與自家帳號對起來。
    /// The account linking flow: after the user asks to link, issue a link token and carry it to your own login
    /// page (in a URL parameter, say). The user signs in there, your server generates a nonce bound to that user,
    /// and finally redirects the user to
    /// <c>https://access.line.me/dialog/bot/accountLink?linkToken={linkToken}&amp;nonce={nonce}</c>.
    /// LINE then sends an account link event carrying the same nonce, which is how you match the LINE user to your
    /// own account.
    /// </para>
    /// </remarks>
    /// <param name="userId">LINE 使用者識別碼。The LINE user identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>連結權杖。The link token.</returns>
    Task<Result<string>> IssueLinkTokenAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 下載圖片或影片訊息的縮圖。
    /// Downloads the preview image of an image or video message.
    /// </summary>
    /// <param name="messageId">訊息識別碼。The message identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>縮圖的位元組與內容型別。The preview's bytes and content type.</returns>
    Task<Result<LineContent>> GetMessageContentPreviewAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢影片或音訊訊息內容的轉檔狀態。
    /// Reads the transcoding status of a video or audio message's content.
    /// </summary>
    /// <remarks>
    /// 影片與音訊內容要先等狀態變成 <see cref="LineContentTranscodingStatus.Succeeded"/> 才能用
    /// <see cref="GetMessageContentAsync"/> 下載;<see cref="LineContentTranscodingStatus.Processing"/>
    /// 時請稍後再查。
    /// Video and audio content can be downloaded with <see cref="GetMessageContentAsync"/> only once the status is
    /// <see cref="LineContentTranscodingStatus.Succeeded"/>; while it is
    /// <see cref="LineContentTranscodingStatus.Processing"/>, check again later.
    /// </remarks>
    /// <param name="messageId">訊息識別碼。The message identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>轉檔狀態;不認得的狀態字串回 <c>line.api.invalid_response</c> 失敗。The status; an unrecognised status string fails with <c>line.api.invalid_response</c>.</returns>
    Task<Result<LineContentTranscodingStatus>> GetMessageContentTranscodingStatusAsync(string messageId, CancellationToken cancellationToken = default);
}
