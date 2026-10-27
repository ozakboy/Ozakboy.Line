namespace Ozakboy.Line.Webhook;

/// <summary>
/// <c>accountLink</c> 事件的附加資訊。
/// The details of an <c>accountLink</c> event.
/// </summary>
public sealed class LineWebhookLink
{
    /// <summary>
    /// 連結結果,見 <see cref="LineWebhookLinkResults"/>。
    /// The link result; see <see cref="LineWebhookLinkResults"/>.
    /// </summary>
    public string Result { get; init; } = string.Empty;

    /// <summary>
    /// 發起連結時產生的 nonce;沒有時為 <see langword="null"/>。
    /// The nonce generated when the link was started, or <see langword="null"/> when absent.
    /// </summary>
    /// <remarks>
    /// 用它對回自己系統裡的帳號,並且在比對之後讓它失效 —— nonce 是一次性的。
    /// Use it to find the matching account in your own system, and invalidate it afterwards: a nonce is one-shot.
    /// </remarks>
    public string? Nonce { get; init; }
}
