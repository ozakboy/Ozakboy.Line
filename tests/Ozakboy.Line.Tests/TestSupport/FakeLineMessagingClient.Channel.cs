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
    /// <summary>
    /// <see cref="GetWebhookEndpointAsync"/> 要回的結果;為 <see langword="null"/> 時沒有安排行為。
    /// What <see cref="GetWebhookEndpointAsync"/> answers with, or <see langword="null"/> when not arranged.
    /// </summary>
    internal Result<LineWebhookEndpointInfo>? WebhookEndpointResult { get; set; }

    /// <summary>
    /// <see cref="TestWebhookEndpointAsync"/> 要回的結果;為 <see langword="null"/> 時沒有安排行為。
    /// What <see cref="TestWebhookEndpointAsync"/> answers with, or <see langword="null"/> when not arranged.
    /// </summary>
    internal Result<LineWebhookTestResult>? WebhookTestResultValue { get; set; }

    /// <summary>
    /// 每一次 <see cref="TestWebhookEndpointAsync"/> 收到的位址。
    /// The endpoint every <see cref="TestWebhookEndpointAsync"/> call received.
    /// </summary>
    internal List<string?> WebhookTests { get; } = [];

    /// <inheritdoc />
    public Task<Result<LineWebhookEndpointInfo>> GetWebhookEndpointAsync(CancellationToken cancellationToken = default) =>
        WebhookEndpointResult is { } arranged ? Task.FromResult(arranged) : throw NotArranged();

    /// <inheritdoc />
    public Task<Result> SetWebhookEndpointAsync(string endpoint, CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result<LineWebhookTestResult>> TestWebhookEndpointAsync(string? endpoint = null, CancellationToken cancellationToken = default)
    {
        WebhookTests.Add(endpoint);
        return WebhookTestResultValue is { } arranged ? Task.FromResult(arranged) : throw NotArranged();
    }

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
