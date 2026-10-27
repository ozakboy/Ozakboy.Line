using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Messaging;
using Ozakboy.Line.Messaging.RichMenu;

namespace Ozakboy.Line.Tests.TestSupport;

/// <summary>
/// <see cref="FakeLineMessagingClient"/> 的圖文選單批次作業部分:全部未安排。
/// The rich menu batch part of <see cref="FakeLineMessagingClient"/>: nothing is arranged.
/// </summary>
internal sealed partial class FakeLineMessagingClient
{
    /// <inheritdoc />
    public Task<Result<string>> ReplaceOrUnlinkRichMenusInBatchAsync(
        IReadOnlyList<LineRichMenuBatchOperation> operations,
        string? resumeRequestKey = null,
        CancellationToken cancellationToken = default) => throw NotArranged();

    /// <inheritdoc />
    public Task<Result> ValidateRichMenuBatchAsync(
        IReadOnlyList<LineRichMenuBatchOperation> operations,
        CancellationToken cancellationToken = default) => throw NotArranged();

    /// <inheritdoc />
    public Task<Result<LineRichMenuBatchProgress>> GetRichMenuBatchProgressAsync(string requestId, CancellationToken cancellationToken = default) =>
        throw NotArranged();
}
