namespace Ozakboy.Line;

/// <summary>
/// 本套件回傳的錯誤代碼(頻道設定相關)。
/// The error codes this package returns — channel-settings related.
/// </summary>
public static partial class LineErrorCodes
{
    /// <summary>
    /// webhook 位址不合規則:必須是 <c>https://</c> 開頭且不超過 500 字元。
    /// The webhook address is not acceptable: it must start with <c>https://</c> and be at most 500 characters.
    /// </summary>
    public const string InvalidWebhookEndpoint = "line.validation.invalid_webhook_endpoint";
}
