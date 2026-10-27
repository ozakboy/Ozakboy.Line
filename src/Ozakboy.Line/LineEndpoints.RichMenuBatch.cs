namespace Ozakboy.Line;

/// <summary>
/// 圖文選單批次作業的端點(<see cref="LineEndpoints"/> 的一部分)。
/// The endpoints of rich menu batch operations (part of <see cref="LineEndpoints"/>).
/// </summary>
public static partial class LineEndpoints
{
    /// <summary>
    /// 批次替換或解除圖文選單連結(POST,非同步,回應標頭帶 <c>X-Line-Request-Id</c>)。
    /// Replaces or unlinks rich menus in batch (POST, asynchronous, the response header carries
    /// <c>X-Line-Request-Id</c>).
    /// </summary>
    public const string RichMenuBatch = "https://api.line.me/v2/bot/richmenu/batch";

    /// <summary>
    /// 驗證批次作業的內容,不實際執行(POST)。
    /// Validates the contents of a batch without running it (POST).
    /// </summary>
    public const string RichMenuValidateBatch = "https://api.line.me/v2/bot/richmenu/validate/batch";

    /// <summary>
    /// 查詢批次作業的進度(GET,<c>requestId</c>)。
    /// Queries a batch's progress (GET, <c>requestId</c>).
    /// </summary>
    public const string RichMenuBatchProgress = "https://api.line.me/v2/bot/richmenu/progress/batch";
}
