namespace Ozakboy.Line.Webhook;

/// <summary>
/// <c>beacon</c> 事件的附加資訊。
/// The details of a <c>beacon</c> event.
/// </summary>
public sealed class LineWebhookBeacon
{
    /// <summary>
    /// beacon 裝置的硬體識別碼。
    /// The beacon device's hardware identifier.
    /// </summary>
    public string Hwid { get; init; } = string.Empty;

    /// <summary>
    /// 事件型別,見 <see cref="LineWebhookBeaconTypes"/>。
    /// The event type; see <see cref="LineWebhookBeaconTypes"/>.
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// 裝置訊息(十六進位字串,最多 13 個位元組);沒有時為 <see langword="null"/>。
    /// The device message — a hexadecimal string of up to 13 bytes — or <see langword="null"/> when absent.
    /// </summary>
    public string? Dm { get; init; }
}
