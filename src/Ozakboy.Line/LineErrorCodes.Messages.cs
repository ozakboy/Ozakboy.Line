namespace Ozakboy.Line;

/// <summary>
/// 本套件回傳的錯誤代碼(訊息與動作的本地驗證)。
/// The error codes this package returns — local validation of messages and actions.
/// </summary>
public static partial class LineErrorCodes
{
    /// <summary>
    /// 訊息本身不合規格:文字過長、位址不是 HTTPS、emoji 或 mention 的位置對不上等。請求沒有送出。
    /// A message breaks the specification — text too long, an address that is not HTTPS, an emoji or mention that
    /// points nowhere, and so on. No request was sent.
    /// </summary>
    /// <remarks>
    /// 範本與圖片地圖的結構問題維持用 <see cref="InvalidTemplate"/> 與 <see cref="InvalidImagemap"/>;
    /// 動作的問題一律是 <see cref="InvalidAction"/>。
    /// Structural problems in a template or an imagemap keep using <see cref="InvalidTemplate"/> and
    /// <see cref="InvalidImagemap"/>; a problem inside an action is always <see cref="InvalidAction"/>.
    /// </remarks>
    public const string InvalidMessage = "line.validation.invalid_message";

    /// <summary>
    /// 動作不合規格:標籤過長、位址的協定不被接受、回傳資料過長、日期時間格式不符等。請求沒有送出。
    /// An action breaks the specification — a label that is too long, an address with an unaccepted scheme,
    /// oversized postback data, a malformed date or time, and so on. No request was sent.
    /// </summary>
    public const string InvalidAction = "line.validation.invalid_action";
}
