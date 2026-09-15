namespace Ozakboy.Line.Messaging;

/// <summary>
/// 從 LINE 下載回來的二進位內容。
/// Binary content downloaded from LINE.
/// </summary>
/// <remarks>
/// 內容一次讀進記憶體。使用者傳的檔案最大 300 MB,而 LINE 的內容端點在訊息送達後只保留一段時間 ——
/// 要處理大檔的呼叫端請自行評估,別在高並發的請求執行緒上直接呼叫。
/// The content is read into memory in one go. A user's file can be up to 300 MB, and LINE's content endpoint keeps
/// it only for a while after delivery; a caller handling large files should weigh that, and not call this
/// directly on a heavily concurrent request thread.
/// </remarks>
/// <param name="Bytes">內容位元組。The content bytes.</param>
/// <param name="ContentType">內容型別,例如 <c>image/jpeg</c>。The content type, such as <c>image/jpeg</c>.</param>
public sealed record LineContent(byte[] Bytes, string ContentType);
