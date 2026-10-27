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
    /// <inheritdoc />
    public Task<Result<LineMessageDeliveryCount>> GetMessageDeliveryCountAsync(
        LineMessageDeliveryType type,
        DateOnly date,
        CancellationToken cancellationToken = default) => throw NotArranged();

    /// <inheritdoc />
    public Task<Result<LineMessageEventInsight>> GetMessageEventInsightAsync(string requestId, CancellationToken cancellationToken = default) =>
        throw NotArranged();

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
