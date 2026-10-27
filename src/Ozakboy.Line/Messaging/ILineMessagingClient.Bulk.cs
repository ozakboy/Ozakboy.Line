using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Messaging.Messages;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// <see cref="ILineMessagingClient"/> 的超量自動分批發送部分。
/// The automatic batching of oversized sends, part of <see cref="ILineMessagingClient"/>.
/// </summary>
/// <remarks>
/// <b>沒有 <c>ReplyManyAsync</c>,這是刻意的。</b>reply token 只能用一次,而且一次最多 5 則;
/// 超過 5 則的回覆不可能拆成多次 reply,硬拆的結果是第二批以後全部因 token 已用過而失敗。
/// 需要回超過 5 則時,第一批用 reply,其餘改用 <see cref="PushManyAsync"/>(會消耗推播額度)。
/// <b>There is deliberately no <c>ReplyManyAsync</c>.</b> A reply token works once and carries at most 5
/// messages; a reply of more than 5 cannot be split across several reply calls, and forcing it would fail every
/// batch after the first because the token is already spent. To answer with more than 5, reply with the first
/// batch and send the rest with <see cref="PushManyAsync"/> (which consumes push quota).
/// </remarks>
public partial interface ILineMessagingClient
{
    /// <summary>
    /// 推播任意則數的訊息給單一對象:每 5 則一批,依序送出。
    /// Pushes any number of messages to one recipient, in batches of 5 sent one after another.
    /// </summary>
    /// <param name="to">使用者、群組或聊天室識別碼。A user, group, or room identifier.</param>
    /// <param name="messages">訊息,至少 1 則,沒有上限。The messages, at least one and no upper limit.</param>
    /// <param name="options">可選參數。The optional parameters.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>
    /// 全部批次成功時為 <see cref="LineBulkSendResult"/>;任何一批失敗就<b>停止</b>,回該批的錯誤,
    /// 並在錯誤資料放 <see cref="LineBulkSendErrorDataKeys.SentBatches"/> 與 <see cref="LineBulkSendErrorDataKeys.FailedBatchIndex"/>。
    /// A <see cref="LineBulkSendResult"/> when every batch succeeds; the first failing batch <b>stops</b> the send
    /// and its error is returned, with <see cref="LineBulkSendErrorDataKeys.SentBatches"/> and
    /// <see cref="LineBulkSendErrorDataKeys.FailedBatchIndex"/> in the error data.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>依序,不平行。</b>LINE 依送達順序排列訊息,平行送出會讓第 6 則跑到第 1 則前面。
    /// <b>Sequential, never parallel.</b> LINE orders messages by arrival, and sending in parallel would let the 6th
    /// message land before the 1st.
    /// </para>
    /// <para>
    /// <b>送出前先整批驗證。</b>憑證、零則訊息、每一則自己的上限與統計單位都在送第一批之前檢查完,
    /// 因此「第 3 批的某則訊息格式不對」不會讓前兩批已經送出。
    /// <b>The whole input is validated first.</b> Credentials, an empty list, each message's own limits and the
    /// aggregation unit are all checked before the first batch goes out, so a malformed message in batch 3 does not
    /// leave batches 1 and 2 already sent.
    /// </para>
    /// <para>
    /// <b>重試鍵:</b>給了 <see cref="LinePushOptions.RetryKey"/> 時,每一批使用
    /// <see cref="LineBulkSendRetryKey.Derive"/> 從它與批次索引推導出的、各自且可重現的鍵,並標為可重試;
    /// 因此失敗後<b>用同一個鍵重跑整個呼叫</b>,已送達的批次會被 LINE 去重,只有沒送出的批次真的送出。
    /// 沒給時每批都不帶鍵、絕不重試(與 <see cref="PushAsync"/> 相同)。
    /// <b>Retry key:</b> when <see cref="LinePushOptions.RetryKey"/> is set, each batch uses its own reproducible key
    /// derived from it and the batch index by <see cref="LineBulkSendRetryKey.Derive"/>, and is marked retryable;
    /// <b>re-running the whole call with the same key</b> after a failure therefore has LINE deduplicate the batches
    /// that already arrived, and only the unsent ones go out. Without one no batch carries a key and none is ever
    /// retried (as with <see cref="PushAsync"/>).
    /// </para>
    /// </remarks>
    Task<Result<LineBulkSendResult>> PushManyAsync(
        string to,
        IReadOnlyList<LineMessage> messages,
        LinePushOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 群發任意則數的訊息給任意多位收件人:收件人每 500 人一批、訊息每 5 則一批,依序送出。
    /// Multicasts any number of messages to any number of recipients: recipients in batches of 500 and messages in
    /// batches of 5, sent one after another.
    /// </summary>
    /// <param name="userIds">收件人的使用者識別碼,至少 1 位,沒有上限。The recipients' user ids, at least one and no upper limit.</param>
    /// <param name="messages">訊息,至少 1 則,沒有上限。The messages, at least one and no upper limit.</param>
    /// <param name="options">可選參數。The optional parameters.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>
    /// 語意同 <see cref="PushManyAsync"/>;零位收件人以 <see cref="LineErrorCodes.NoRecipients"/> 失敗。
    /// 群發端點不回已送出的訊息,所以 <see cref="LineBulkSendResult.SentMessages"/> 為空。
    /// The same semantics as <see cref="PushManyAsync"/>; zero recipients fail with
    /// <see cref="LineErrorCodes.NoRecipients"/>. The multicast endpoint returns no sent messages, so
    /// <see cref="LineBulkSendResult.SentMessages"/> is empty.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>順序:外層收件人批、內層訊息批。</b>同一群收件人會先收完全部訊息(依序),才輪到下一群收件人。
    /// 失敗批的索引是「收件人批索引 × 訊息批數 + 訊息批索引」。
    /// <b>Order: recipient batches outside, message batches inside.</b> One group of recipients receives all its
    /// messages, in order, before the next group starts. The failing batch's index is
    /// "recipient batch index × message batch count + message batch index".
    /// </para>
    /// <para>
    /// 重試鍵的推導以這個攤平後的批次索引為準,細節同 <see cref="PushManyAsync"/>。
    /// Retry keys are derived from that flattened batch index, with the same details as <see cref="PushManyAsync"/>.
    /// </para>
    /// </remarks>
    Task<Result<LineBulkSendResult>> MulticastManyAsync(
        IReadOnlyList<string> userIds,
        IReadOnlyList<LineMessage> messages,
        LinePushOptions? options = null,
        CancellationToken cancellationToken = default);
}
