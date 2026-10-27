using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Messaging;

namespace Ozakboy.Line.Tests.TestSupport;

/// <summary>
/// <see cref="FakeLineMessagingClient"/> 的頻道設定、帳號連結與訊息內容補充端點;一律沒有安排行為。
/// The channel-settings, account-linking and message-content additions to <see cref="FakeLineMessagingClient"/>;
/// none has arranged behaviour.
/// </summary>
internal sealed partial class FakeLineMessagingClient
{
    /// <inheritdoc />
    public Task<Result<LineWebhookEndpointInfo>> GetWebhookEndpointAsync(CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result> SetWebhookEndpointAsync(string endpoint, CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result<LineWebhookTestResult>> TestWebhookEndpointAsync(string? endpoint = null, CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result<string>> IssueLinkTokenAsync(string userId, CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result<LineContent>> GetMessageContentPreviewAsync(string messageId, CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result<LineContentTranscodingStatus>> GetMessageContentTranscodingStatusAsync(string messageId, CancellationToken cancellationToken = default) =>
        throw NotArranged();
}
