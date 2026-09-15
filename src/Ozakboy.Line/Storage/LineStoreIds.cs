namespace Ozakboy.Line.Storage;

/// <summary>
/// 本套件的 store 產生識別碼的方式。
/// How this package's stores produce identifiers.
/// </summary>
/// <remarks>
/// 用 <c>N</c> 格式(32 個十六進位字元、沒有連字號):這個識別碼會出現在網址、JSON 鍵與 MCP 工具的參數裡,
/// 少了連字號就少一種「有沒有被某一層編碼過」的差異。
/// The <c>N</c> format — 32 hex characters, no hyphens — is used because these identifiers show up in URLs, in
/// JSON keys, and in MCP tool arguments, and without hyphens there is one less way for a layer to have encoded
/// them differently.
/// </remarks>
internal static class LineStoreIds
{
    /// <summary>
    /// 產生一個新的識別碼。
    /// Produces a new identifier.
    /// </summary>
    /// <returns>32 個十六進位字元。Thirty-two hex characters.</returns>
    internal static string New() => Guid.NewGuid().ToString("N");
}
