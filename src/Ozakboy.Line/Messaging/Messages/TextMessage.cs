using System.Text.Json.Serialization;
using System.Globalization;
using System.Text.Json;
using Ozakboy.Core.Abstractions;

namespace Ozakboy.Line.Messaging.Messages;

/// <summary>
/// 文字訊息。
/// A text message.
/// </summary>
[JsonConverter(typeof(LineMessageJsonConverterFactory))]
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

    /// <summary>
    /// 文字裡的 LINE emoji;有項目才輸出 <c>emojis</c>。
    /// The LINE emojis in the text; <c>emojis</c> is written only when there are entries.
    /// </summary>
    /// <remarks>
    /// 每個 emoji 的 <see cref="LineTextEmoji.Index"/> 在 <see cref="Text"/> 裡必須是 <c>$</c>。
    /// 要在文字裡 @ 人或用具名替換,請改用 <see cref="TextV2Message"/>。
    /// The text must hold a <c>$</c> at each emoji's <see cref="LineTextEmoji.Index"/>. To mention people or use
    /// named substitutions, use <see cref="TextV2Message"/> instead.
    /// </remarks>
    public IList<LineTextEmoji> Emojis { get; } = [];

    /// <inheritdoc />
    public override Result Validate()
    {
        var baseResult = base.Validate();
        if (baseResult.IsFailure)
        {
            return baseResult;
        }

        if (LineMessageValidation.IsTooLong(Text, LineMessagingLimits.MaxTextLength))
        {
            return LineMessageValidation.InvalidMessage(
                LineMessageValidation.TooLong("文字訊息的 text", Text, LineMessagingLimits.MaxTextLength));
        }

        if (Emojis.Count > LineMessagingLimits.MaxTextEmojis)
        {
            return LineMessageValidation.InvalidMessage(string.Create(
                CultureInfo.InvariantCulture,
                $"文字訊息最多帶 {LineMessagingLimits.MaxTextEmojis} 個 emoji,這次是 {Emojis.Count} 個。A text message takes at most {LineMessagingLimits.MaxTextEmojis} emojis; {Emojis.Count} were supplied."));
        }

        for (var index = 0; index < Emojis.Count; index++)
        {
            var emojiIndex = Emojis[index].Index;
            if (emojiIndex >= Text.Length || Text[emojiIndex] != '$')
            {
                return LineMessageValidation.InvalidMessage(string.Create(
                    CultureInfo.InvariantCulture,
                    $"emojis[{index}] 的 index={emojiIndex} 在文字(長度 {Text.Length})裡不是 '$' 佔位字元。emojis[{index}] has index={emojiIndex}, but the text (length {Text.Length}) holds no '$' placeholder there."));
            }
        }

        return Result.Success();
    }

    /// <inheritdoc />
    internal override void WriteBody(Utf8JsonWriter writer)
    {
        writer.WriteString("text", Text);

        if (Emojis.Count > 0)
        {
            writer.WriteStartArray("emojis");
            for (var index = 0; index < Emojis.Count; index++)
            {
                var emoji = Emojis[index];
                writer.WriteStartObject();
                writer.WriteNumber("index", emoji.Index);
                writer.WriteString("productId", emoji.ProductId);
                writer.WriteString("emojiId", emoji.EmojiId);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        if (!string.IsNullOrWhiteSpace(QuoteToken))
        {
            writer.WriteString("quoteToken", QuoteToken);
        }
    }
}
