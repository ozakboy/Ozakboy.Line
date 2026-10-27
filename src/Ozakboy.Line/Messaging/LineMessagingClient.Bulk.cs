using System.Globalization;
using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Messaging.Messages;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// <see cref="LineMessagingClient"/> 的超量自動分批發送。
/// The automatic batching of oversized sends in <see cref="LineMessagingClient"/>.
/// </summary>
public sealed partial class LineMessagingClient
{
    /// <inheritdoc />
    public async Task<Result<LineBulkSendResult>> PushManyAsync(
        string to,
        IReadOnlyList<LineMessage> messages,
        LinePushOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        ArgumentNullException.ThrowIfNull(messages);

        if (!Options.IsConfigured)
        {
            return NotConfigured<LineBulkSendResult>();
        }

        var valid = ValidateBulkInput(messages, options);
        if (valid.IsFailure)
        {
            return valid.ToFailure<LineBulkSendResult>();
        }

        var batches = Chunk(messages, LineMessagingLimits.MaxMessagesPerRequest);
        var sentMessages = new List<LineSentMessage>(messages.Count);

        for (var index = 0; index < batches.Count; index++)
        {
            var sent = await PushAsync(to, batches[index], BatchOptions(options, index), cancellationToken).ConfigureAwait(false);
            if (sent.IsFailure)
            {
                return Result.Failure<LineBulkSendResult>(StoppedAt(sent.Error!, index));
            }

            sentMessages.AddRange(sent.GetValueOrThrow());
        }

        return Result.Success(new LineBulkSendResult(batches.Count, messages.Count, sentMessages));
    }

    /// <inheritdoc />
    public async Task<Result<LineBulkSendResult>> MulticastManyAsync(
        IReadOnlyList<string> userIds,
        IReadOnlyList<LineMessage> messages,
        LinePushOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        ArgumentNullException.ThrowIfNull(messages);

        if (!Options.IsConfigured)
        {
            return NotConfigured<LineBulkSendResult>();
        }

        if (userIds.Count == 0)
        {
            return Error.Validation(
                LineErrorCodes.NoRecipients,
                "分批群發至少需要 1 位收件人。A bulk multicast needs at least 1 recipient.");
        }

        var valid = ValidateBulkInput(messages, options);
        if (valid.IsFailure)
        {
            return valid.ToFailure<LineBulkSendResult>();
        }

        var recipientBatches = Chunk(userIds, LineMessagingLimits.MulticastRecipients);
        var messageBatches = Chunk(messages, LineMessagingLimits.MaxMessagesPerRequest);

        // 外層收件人、內層訊息:同一群人先依序收完全部訊息,再換下一群。
        // Recipients outside, messages inside: one group of people receives every message, in order, before the
        // next group starts.
        for (var recipientIndex = 0; recipientIndex < recipientBatches.Count; recipientIndex++)
        {
            for (var messageIndex = 0; messageIndex < messageBatches.Count; messageIndex++)
            {
                var batchIndex = (recipientIndex * messageBatches.Count) + messageIndex;
                var sent = await MulticastAsync(
                    recipientBatches[recipientIndex],
                    messageBatches[messageIndex],
                    BatchOptions(options, batchIndex),
                    cancellationToken).ConfigureAwait(false);

                if (sent.IsFailure)
                {
                    return Result.Failure<LineBulkSendResult>(StoppedAt(sent.Error!, batchIndex));
                }
            }
        }

        return Result.Success(new LineBulkSendResult(recipientBatches.Count * messageBatches.Count, messages.Count, []));
    }

    /// <summary>
    /// 在送第一批之前檢查整份輸入:訊息至少 1 則、每一則自己的上限,以及統計單位。
    /// Checks the whole input before the first batch: at least one message, each message's own limits, and the
    /// aggregation unit.
    /// </summary>
    /// <param name="messages">全部訊息。All the messages.</param>
    /// <param name="options">可選參數。The optional parameters.</param>
    /// <returns>合法時為成功。Success when valid.</returns>
    /// <remarks>
    /// 訊息則數的檢查只剩「至少 1 則」(上限由分批消化),錯誤語意沿用 <see cref="LineErrorCodes.TooManyMessages"/>。
    /// 全部先驗完,是為了不讓第 3 批的格式錯誤發生在前兩批已經送出之後。
    /// The count check reduces to "at least one" (the upper limit is absorbed by batching), keeping the
    /// <see cref="LineErrorCodes.TooManyMessages"/> semantics. Everything is checked up front so that a format
    /// error in batch 3 cannot surface after batches 1 and 2 have already gone out.
    /// </remarks>
    private static Result ValidateBulkInput(IReadOnlyList<LineMessage> messages, LinePushOptions? options)
    {
        if (messages.Count < LineMessagingLimits.MinMessagesPerRequest)
        {
            return ValidateMessageCount(messages.Count);
        }

        for (var index = 0; index < messages.Count; index++)
        {
            var message = messages[index].Validate();
            if (message.IsFailure)
            {
                return message;
            }
        }

        return ValidateAggregationUnits(options?.CustomAggregationUnits);
    }

    /// <summary>
    /// 切成每批最多 <paramref name="size"/> 個、保持原順序的批次。
    /// Splits into order-preserving batches of at most <paramref name="size"/>.
    /// </summary>
    /// <typeparam name="T">元素型別。The element type.</typeparam>
    /// <param name="items">全部元素。All the items.</param>
    /// <param name="size">每批上限。The batch size.</param>
    /// <returns>批次。The batches.</returns>
    private static List<T[]> Chunk<T>(IReadOnlyList<T> items, int size)
    {
        var batches = new List<T[]>((items.Count + size - 1) / size);
        for (var start = 0; start < items.Count; start += size)
        {
            var batch = new T[Math.Min(size, items.Count - start)];
            for (var offset = 0; offset < batch.Length; offset++)
            {
                batch[offset] = items[start + offset];
            }

            batches.Add(batch);
        }

        return batches;
    }

    /// <summary>
    /// 產生某一批專用的選項:沿用通知與統計單位設定,重試鍵改成該批推導出的鍵(沒給就不帶)。
    /// Builds the options for one batch: notification and aggregation unit carry over, and the retry key becomes
    /// the one derived for that batch (none when the caller gave none).
    /// </summary>
    /// <param name="options">呼叫端的選項。The caller's options.</param>
    /// <param name="batchIndex">批次索引(從 0 起算)。The zero-based batch index.</param>
    /// <returns>該批的選項。The batch's options.</returns>
    private static LinePushOptions BatchOptions(LinePushOptions? options, int batchIndex) =>
        new()
        {
            NotificationDisabled = options?.NotificationDisabled ?? false,
            CustomAggregationUnits = options?.CustomAggregationUnits,
            RetryKey = options?.RetryKey is { } key ? LineBulkSendRetryKey.Derive(key, batchIndex) : null,
        };

    /// <summary>
    /// 在失敗那一批的錯誤上補上進度資料,代碼與分類保持原樣。
    /// Adds the progress data to the failing batch's error, leaving its code and category as they were.
    /// </summary>
    /// <param name="error">該批的錯誤。The batch's error.</param>
    /// <param name="failedBatchIndex">失敗批的索引。The failing batch's index.</param>
    /// <returns>補上資料的錯誤。The error with the data added.</returns>
    private static Error StoppedAt(Error error, int failedBatchIndex) =>
        error
            .WithData(LineBulkSendErrorDataKeys.SentBatches, (long)failedBatchIndex)
            .WithData(LineBulkSendErrorDataKeys.FailedBatchIndex, (long)failedBatchIndex);
}
