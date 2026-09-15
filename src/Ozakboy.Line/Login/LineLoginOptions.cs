using Ozakboy.Core.Abstractions;

namespace Ozakboy.Line.Login;

/// <summary>
/// LINE Login 的頻道設定。
/// The channel settings for LINE Login.
/// </summary>
/// <remarks>
/// <b>Login channel 與 Messaging channel 是兩個不同的頻道</b>,各有各的 id 與 secret,在 LINE Developers
/// 後台也是兩個不同的項目。把 Messaging 的憑證填進這裡,得到的是 LINE 回的 400,而不是任何提示這件事的訊息。
/// <b>A Login channel and a Messaging channel are two different channels</b>, each with its own id and secret,
/// listed separately in the LINE Developers console. Putting the Messaging credentials here earns a 400 from
/// LINE and nothing that points at the mix-up.
/// </remarks>
public sealed class LineLoginOptions
{
    /// <summary>
    /// Login channel 的 channel id,也就是 OAuth 的 client_id。
    /// The Login channel's channel id, which is the OAuth client_id.
    /// </summary>
    public string ChannelId { get; set; } = string.Empty;

    /// <summary>
    /// Login channel 的 channel secret,也就是 OAuth 的 client_secret,同時是 id_token 的簽章金鑰。
    /// The Login channel's channel secret, which is the OAuth client_secret and also the id_token signing key.
    /// </summary>
    /// <remarks>
    /// 這個值是祕密:註冊管線時會被登記進遮罩器,日誌與錯誤訊息裡都不會出現原文。
    /// This is a secret: it is registered with the masker when the pipeline is set up, so it appears in neither
    /// logs nor error messages.
    /// </remarks>
    public string ChannelSecret { get; set; } = string.Empty;

    /// <summary>
    /// 授權時要求的權限範圍,預設為 <c>profile</c> 與 <c>openid</c>。
    /// The scopes requested at authorization; <c>profile</c> and <c>openid</c> by default.
    /// </summary>
    /// <remarks>
    /// 拿得到 id_token 的前提是 <c>openid</c>;要 email 還得另外申請 <c>email</c> 權限並通過 LINE 審核。
    /// An id_token arrives only with <c>openid</c>. Email additionally requires the <c>email</c> scope, which has
    /// to be applied for and approved by LINE.
    /// </remarks>
    public IList<string> Scopes { get; } = ["profile", "openid"];

    /// <summary>
    /// 預設的加好友引導方式,個別請求可以覆寫。
    /// The default add-friend prompt, which an individual request may override.
    /// </summary>
    public LineBotPrompt BotPrompt { get; set; } = LineBotPrompt.None;

    /// <summary>
    /// 憑證是否齊全。
    /// Whether the credentials are complete.
    /// </summary>
    /// <remarks>
    /// 憑證不齊時,用戶端的每個方法都直接回 <see cref="LineErrorCodes.NotConfigured"/> 失敗而不送出請求。
    /// 這讓「忘了設環境變數」在第一次呼叫就講清楚,而不是變成一個 401 讓人去查是不是憑證過期。
    /// When they are not, every client method fails with <see cref="LineErrorCodes.NotConfigured"/> without
    /// sending anything. That way a missing environment variable says so on the first call instead of arriving as
    /// a 401 that looks like an expired credential.
    /// </remarks>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ChannelId) && !string.IsNullOrWhiteSpace(ChannelSecret);

    /// <summary>
    /// 檢查設定是否可用。
    /// Validates the settings.
    /// </summary>
    /// <returns>
    /// 可用時為成功,否則為 <see cref="LineErrorCodes.NotConfigured"/> 失敗。
    /// Success when usable, otherwise a <see cref="LineErrorCodes.NotConfigured"/> failure.
    /// </returns>
    public Result Validate() =>
        IsConfigured
            ? Result.Success()
            : Error.Validation(
                LineErrorCodes.NotConfigured,
                "LINE Login 的 ChannelId 與 ChannelSecret 都必須設定。Both ChannelId and ChannelSecret must be set for LINE Login.");
}
