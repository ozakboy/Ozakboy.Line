namespace Ozakboy.Line.Messaging;

/// <summary>
/// 分批發送失敗時,本套件放進 <see cref="Ozakboy.Core.Abstractions.Error.Data"/> 的鍵名。
/// The keys this package writes into <see cref="Ozakboy.Core.Abstractions.Error.Data"/> when a bulk send fails.
/// </summary>
/// <remarks>
/// 錯誤本身的代碼、分類與其他資料鍵(<c>statusCode</c> 等)維持失敗那一批的原樣,這裡只是補上進度。
/// The error's own code, category and other keys (<c>statusCode</c> and so on) stay exactly as the failing batch
/// produced them; these two only add where the send stopped.
/// </remarks>
public static class LineBulkSendErrorDataKeys
{
    /// <summary>
    /// 已成功送出的批數。因為批次依序送、遇到失敗就停,它也等於失敗那一批之前的批數。
    /// The number of batches already sent successfully. Batches go out in order and stop at the first failure,
    /// so this also equals the number of batches before the failing one.
    /// </summary>
    public const string SentBatches = "sentBatches";

    /// <summary>
    /// 失敗那一批的索引(從 0 起算)。群發的索引是「收件人批索引 × 訊息批數 + 訊息批索引」。
    /// The zero-based index of the failing batch. For a multicast the index is
    /// "recipient batch index × message batch count + message batch index".
    /// </summary>
    public const string FailedBatchIndex = "failedBatchIndex";
}
