using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// 一則已送出的訊息。
/// One message that has been sent.
/// </summary>
public sealed class LineSentMessage
{
    /// <summary>
    /// 訊息識別碼。
    /// The message identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// 引用權杖:要在後續訊息中引用這一則時使用。
    /// The quote token, used to quote this message from a later one.
    /// </summary>
    /// <remarks>
    /// 只有文字與貼圖訊息會拿到權杖;其他型別這裡是 <see langword="null"/>。
    /// Only text and sticker messages come back with a token; for other types this is <see langword="null"/>.
    /// </remarks>
    [JsonPropertyName("quoteToken")]
    public string? QuoteToken { get; init; }
}
