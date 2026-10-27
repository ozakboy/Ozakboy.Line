namespace Ozakboy.Line;

/// <summary>
/// LINE 各項服務的端點位址(LIFF server API)。
/// The endpoint addresses of LINE's services — the LIFF server API.
/// </summary>
public static partial class LineEndpoints
{
    /// <summary>
    /// LIFF 應用程式的集合端點:POST 新增、GET 列出;單一應用程式在其後加 <c>/{liffId}</c>(PUT 更新、DELETE 刪除)。
    /// The LIFF apps collection endpoint: POST adds, GET lists; a single app lives at <c>/{liffId}</c> after it
    /// (PUT updates, DELETE removes).
    /// </summary>
    public const string LiffApps = "https://api.line.me/liff/v1/apps";
}
