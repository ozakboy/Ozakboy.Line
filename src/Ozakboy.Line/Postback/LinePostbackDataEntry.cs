namespace Ozakboy.Line.Postback;

/// <summary>
/// 暫存項目:代碼、資料與到期時間。給兩個內建儲存體共用,檔案型儲存體也拿它序列化。
/// A staged entry: token, data and expiry. Shared by the two built-in stores, and serialised by the file-backed one.
/// </summary>
internal sealed class LinePostbackDataEntry
{
    /// <summary>代碼。The token.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>資料。The data.</summary>
    public string Data { get; set; } = string.Empty;

    /// <summary>到期時間(到了這一刻即視為過期)。The expiry; the entry counts as expired from this instant on.</summary>
    public DateTimeOffset ExpiresAt { get; set; }
}
