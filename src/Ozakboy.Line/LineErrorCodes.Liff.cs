namespace Ozakboy.Line;

/// <summary>
/// 本套件回傳的錯誤代碼(LIFF 相關)。
/// The error codes this package returns — LIFF related.
/// </summary>
public static partial class LineErrorCodes
{
    /// <summary>
    /// LIFF 應用程式的設定不合規則:網址不是 <c>https://</c>、說明超過 100 字,或新增時缺少必要欄位。
    /// The LIFF app's settings are not acceptable: the URL is not <c>https://</c>, the description exceeds 100
    /// characters, or a field required for creation is missing.
    /// </summary>
    public const string InvalidLiffApp = "line.validation.invalid_liff_app";
}
