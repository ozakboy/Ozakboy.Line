namespace Ozakboy.Line.Webhook;

/// <summary>
/// <c>accountLink</c> 事件的 <see cref="LineWebhookLink.Result"/> 字串。
/// The <see cref="LineWebhookLink.Result"/> strings of an <c>accountLink</c> event.
/// </summary>
public static class LineWebhookLinkResults
{
    /// <summary>連結成功。The link succeeded.</summary>
    public const string Ok = "ok";

    /// <summary>連結失敗。The link failed.</summary>
    public const string Failed = "failed";
}
