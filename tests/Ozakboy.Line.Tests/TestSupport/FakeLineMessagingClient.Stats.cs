using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Messaging;
using Ozakboy.Line.Messaging.Insight;

namespace Ozakboy.Line.Tests.TestSupport;

/// <summary>
/// <see cref="FakeLineMessagingClient"/> 的投遞統計與統計單位部分:全部未安排。
/// The delivery count and aggregation unit part of <see cref="FakeLineMessagingClient"/>: nothing is arranged.
/// </summary>
internal sealed partial class FakeLineMessagingClient
{
    /// <summary>
    /// 各管道 <see cref="GetMessageDeliveryCountAsync"/> 要回的結果;沒有安排的管道沒有安排行為。
    /// What <see cref="GetMessageDeliveryCountAsync"/> answers with per channel; a channel not listed has no
    /// arranged behaviour.
    /// </summary>
    internal Dictionary<LineMessageDeliveryType, Result<LineMessageDeliveryCount>> DeliveryCountResults { get; } = [];

    /// <summary>
    /// 每一次 <see cref="GetMessageDeliveryCountAsync"/> 收到的管道與日期。
    /// The channel and date every <see cref="GetMessageDeliveryCountAsync"/> call received.
    /// </summary>
    internal List<(LineMessageDeliveryType Type, DateOnly Date)> DeliveryCountCalls { get; } = [];

    /// <summary>
    /// <see cref="GetMessageEventInsightAsync"/> 要回的結果;為 <see langword="null"/> 時沒有安排行為。
    /// What <see cref="GetMessageEventInsightAsync"/> answers with, or <see langword="null"/> when not arranged.
    /// </summary>
    internal Result<LineMessageEventInsight>? EventInsightResult { get; set; }

    /// <summary>
    /// 每一次 <see cref="GetMessageEventInsightAsync"/> 收到的請求識別碼。
    /// The request id every <see cref="GetMessageEventInsightAsync"/> call received.
    /// </summary>
    internal List<string> EventInsightRequests { get; } = [];

    /// <inheritdoc />
    public Task<Result<LineMessageDeliveryCount>> GetMessageDeliveryCountAsync(
        LineMessageDeliveryType type,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        DeliveryCountCalls.Add((type, date));
        return DeliveryCountResults.TryGetValue(type, out var arranged) ? Task.FromResult(arranged) : throw NotArranged();
    }

    /// <inheritdoc />
    public Task<Result<LineMessageEventInsight>> GetMessageEventInsightAsync(string requestId, CancellationToken cancellationToken = default)
    {
        EventInsightRequests.Add(requestId);
        return EventInsightResult is { } arranged ? Task.FromResult(arranged) : throw NotArranged();
    }

    /// <inheritdoc />
    public Task<Result<LineMessageEventInsight>> GetMessageEventInsightByUnitAsync(
        string customAggregationUnit,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default) => throw NotArranged();

    /// <inheritdoc />
    public Task<Result<int>> GetAggregationUnitUsageAsync(CancellationToken cancellationToken = default) => throw NotArranged();

    /// <inheritdoc />
    public Task<Result<LineAggregationUnitNamesPage>> GetAggregationUnitNamesAsync(
        int? limit = null,
        string? start = null,
        CancellationToken cancellationToken = default) => throw NotArranged();
}
