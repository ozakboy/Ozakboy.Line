using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Messaging;
using Ozakboy.Line.Messaging.Messages;

namespace Ozakboy.Line.Tests.TestSupport;

/// <summary>
/// <see cref="FakeLineMessagingClient"/> 的分批發送部分:全部未安排。
/// The bulk-send part of <see cref="FakeLineMessagingClient"/>: nothing is arranged.
/// </summary>
internal sealed partial class FakeLineMessagingClient
{
    /// <inheritdoc />
    public Task<Result<LineBulkSendResult>> PushManyAsync(
        string to,
        IReadOnlyList<LineMessage> messages,
        LinePushOptions? options = null,
        CancellationToken cancellationToken = default) => throw NotArranged();

    /// <inheritdoc />
    public Task<Result<LineBulkSendResult>> MulticastManyAsync(
        IReadOnlyList<string> userIds,
        IReadOnlyList<LineMessage> messages,
        LinePushOptions? options = null,
        CancellationToken cancellationToken = default) => throw NotArranged();
}
