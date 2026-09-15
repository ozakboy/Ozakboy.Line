using Ozakboy.Core.Abstractions;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// Messaging API 的頻道設定。
/// The channel settings for the Messaging API.
/// </summary>
/// <remarks>
/// 這裡的憑證來自 Messaging channel,與 LINE Login 的<b>不是同一個頻道</b>。
/// These credentials belong to a Messaging channel, which is <b>not the same channel</b> as LINE Login's.
/// </remarks>
public sealed class LineMessagingOptions
{
    /// <summary>
    /// 頻道存取權杖,呼叫 Messaging API 時放在 <c>Authorization: Bearer</c> 裡。
    /// The channel access token, sent as <c>Authorization: Bearer</c> on every Messaging API call.
    /// </summary>
    /// <remarks>
    /// 這個值是祕密:註冊管線時會被登記進遮罩器,日誌與錯誤訊息裡都不會出現原文。
    /// This is a secret: it is registered with the masker when the pipeline is set up, so it appears in neither
    /// logs nor error messages.
    /// </remarks>
    public string ChannelAccessToken { get; set; } = string.Empty;

    /// <summary>
    /// 頻道密鑰,用來驗證 webhook 請求的簽章。
    /// The channel secret, used to verify the signature on webhook requests.
    /// </summary>
    /// <remarks>
    /// 只做 webhook 驗簽時可以只填這個而不填權杖;只推播不收 webhook 時反過來。兩者互不相欠,
    /// <see cref="IsConfigured"/> 因此只看權杖。
    /// Verifying webhooks needs only this and no token; pushing without receiving webhooks needs only the token.
    /// Neither implies the other, which is why <see cref="IsConfigured"/> looks only at the token.
    /// </remarks>
    public string ChannelSecret { get; set; } = string.Empty;

    /// <summary>
    /// 是否已設定可呼叫 Messaging API 的憑證。
    /// Whether the credentials needed to call the Messaging API are set.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ChannelAccessToken);

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
                "LINE Messaging API 的 ChannelAccessToken 必須設定。ChannelAccessToken must be set for the LINE Messaging API.");
}
