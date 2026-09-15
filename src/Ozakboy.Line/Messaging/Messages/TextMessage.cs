using System.Text.Json;

namespace Ozakboy.Line.Messaging.Messages;

/// <summary>
/// 文字訊息。
/// A text message.
/// </summary>
public sealed class TextMessage : LineMessage
{
    /// <summary>
    /// 建立文字訊息。
    /// Creates a text message.
    /// </summary>
    /// <param name="text">訊息內容。The message text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="text"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="text"/> is <see langword="null"/> or blank.
    /// </exception>
    public TextMessage(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Text = text;
    }

    /// <inheritdoc />
    public override string Type => "text";

    /// <summary>
    /// 訊息內容。
    /// The message text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// 引用的訊息權杖;要引用某則訊息回覆時設定。
    /// The quote token, set when this message quotes another.
    /// </summary>
    /// <remarks>
    /// 權杖來自 webhook 事件的 <see cref="Ozakboy.Line.Webhook.LineWebhookMessage.QuoteToken"/>,
    /// 或先前推播回應裡的 <see cref="LineSentMessage.QuoteToken"/>。
    /// The token comes from a webhook event's
    /// <see cref="Ozakboy.Line.Webhook.LineWebhookMessage.QuoteToken"/> or from an earlier push response's
    /// <see cref="LineSentMessage.QuoteToken"/>.
    /// </remarks>
    public string? QuoteToken { get; set; }

    /// <inheritdoc />
    internal override void WriteBody(Utf8JsonWriter writer)
    {
        writer.WriteString("text", Text);

        if (!string.IsNullOrWhiteSpace(QuoteToken))
        {
            writer.WriteString("quoteToken", QuoteToken);
        }
    }
}
