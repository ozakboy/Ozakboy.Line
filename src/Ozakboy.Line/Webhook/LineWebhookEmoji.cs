namespace Ozakboy.Line.Webhook;

/// <summary>
/// 收到的文字訊息裡的一個 LINE emoji。
/// One LINE emoji in a received text message.
/// </summary>
/// <remarks>
/// <see cref="Index"/> 與 <see cref="Length"/> 以 UTF-16 字元計,與 .NET 的 <see cref="string"/> 索引一致;
/// 文字裡該段內容是 <c>$</c> 佔位字元。
/// <see cref="Index"/> and <see cref="Length"/> count UTF-16 characters, matching a .NET <see cref="string"/>
/// index; the text holds a <c>$</c> placeholder in that span.
/// </remarks>
public sealed class LineWebhookEmoji
{
    /// <summary>
    /// 在文字中的位置(從 0 起算)。
    /// The position in the text, from 0.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// 佔用的字元數。
    /// How many characters it occupies.
    /// </summary>
    public int Length { get; init; }

    /// <summary>
    /// emoji 商品識別碼。
    /// The emoji product identifier.
    /// </summary>
    public string ProductId { get; init; } = string.Empty;

    /// <summary>
    /// emoji 識別碼。
    /// The emoji identifier.
    /// </summary>
    public string EmojiId { get; init; } = string.Empty;
}
