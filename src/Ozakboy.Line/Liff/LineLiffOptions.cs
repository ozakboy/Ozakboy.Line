using Ozakboy.Core.Abstractions;

namespace Ozakboy.Line.Liff;

/// <summary>
/// LIFF server API 的設定。
/// The settings for the LIFF server API.
/// </summary>
/// <remarks>
/// LIFF 應用程式屬於 <b>LINE Login channel</b>,不是 Messaging channel,所以這裡用的權杖與
/// <see cref="Messaging.LineMessagingOptions"/> 的<b>不是同一個</b>。
/// LIFF apps belong to a <b>LINE Login channel</b>, not a Messaging channel, so the token here is <b>not the
/// same one</b> as in <see cref="Messaging.LineMessagingOptions"/>.
/// </remarks>
public sealed class LineLiffOptions
{
    /// <summary>
    /// LINE Login channel 的 channel access token,呼叫 LIFF server API 時放在
    /// <c>Authorization: Bearer</c> 裡。可在 LINE Developers Console 簽發,或以 <c>client_credentials</c>
    /// 授權向權杖端點取得。
    /// The LINE Login channel's channel access token, sent as <c>Authorization: Bearer</c> on every LIFF server
    /// API call. It can be issued in the LINE Developers Console or obtained from the token endpoint with the
    /// <c>client_credentials</c> grant.
    /// </summary>
    /// <remarks>
    /// 這個值是祕密:註冊管線時會被登記進遮罩器,日誌與錯誤訊息裡都不會出現原文。
    /// This is a secret: it is registered with the masker when the pipeline is set up, so it appears in neither
    /// logs nor error messages.
    /// </remarks>
    public string ChannelAccessToken { get; set; } = string.Empty;

    /// <summary>
    /// 是否已設定可呼叫 LIFF server API 的憑證。
    /// Whether the credentials needed to call the LIFF server API are set.
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
    public Result Validate() => IsConfigured ? Result.Success() : Result.Failure(LineLiffClient.NotConfiguredError());
}
