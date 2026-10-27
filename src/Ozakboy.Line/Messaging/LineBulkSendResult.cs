namespace Ozakboy.Line.Messaging;

/// <summary>
/// 分批發送全部成功後的結果。
/// The outcome of a bulk send in which every batch succeeded.
/// </summary>
/// <remarks>
/// 只有「全部成功」才會有這個型別;中途任何一批失敗時回的是失敗 <see cref="Ozakboy.Core.Abstractions.Result"/>,
/// 錯誤資料裡帶 <see cref="LineBulkSendErrorDataKeys.SentBatches"/> 與
/// <see cref="LineBulkSendErrorDataKeys.FailedBatchIndex"/>。
/// This type exists only when every batch succeeded; when a batch fails along the way the result is a failure
/// whose error data carries <see cref="LineBulkSendErrorDataKeys.SentBatches"/> and
/// <see cref="LineBulkSendErrorDataKeys.FailedBatchIndex"/>.
/// </remarks>
/// <param name="BatchCount">
/// 實際送出的請求數(群發為收件人批數乘以訊息批數)。
/// The number of requests sent (for a multicast, recipient batches times message batches).
/// </param>
/// <param name="TotalMessages">
/// 呼叫端給的訊息總則數(群發時不乘以收件人數)。
/// The total number of messages the caller supplied (not multiplied by recipients for a multicast).
/// </param>
/// <param name="SentMessages">
/// LINE 回報已送出的訊息,依批次順序串接;群發端點不回這份資料,所以群發時為空。
/// The messages LINE reported as sent, concatenated in batch order; the multicast endpoint returns none, so this
/// is empty for a multicast.
/// </param>
public sealed record LineBulkSendResult(
    int BatchCount,
    int TotalMessages,
    IReadOnlyList<LineSentMessage> SentMessages);
