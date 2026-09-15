using System.Text.Json;

namespace Ozakboy.Line.Messaging.Messages;

/// <summary>
/// 影片訊息。
/// A video message.
/// </summary>
public sealed class VideoMessage : LineMessage
{
    /// <summary>
    /// 建立影片訊息。
    /// Creates a video message.
    /// </summary>
    /// <param name="originalContentUrl">影片位址。The video address.</param>
    /// <param name="previewImageUrl">預覽圖位址。The preview image address.</param>
    /// <exception cref="ArgumentException">
    /// 任一位址為 <see langword="null"/> 或空白時擲出。Thrown when either address is <see langword="null"/> or blank.
    /// </exception>
    public VideoMessage(string originalContentUrl, string previewImageUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalContentUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(previewImageUrl);

        OriginalContentUrl = originalContentUrl;
        PreviewImageUrl = previewImageUrl;
    }

    /// <inheritdoc />
    public override string Type => "video";

    /// <summary>
    /// 影片位址。
    /// The video address.
    /// </summary>
    public string OriginalContentUrl { get; }

    /// <summary>
    /// 預覽圖位址。
    /// The preview image address.
    /// </summary>
    public string PreviewImageUrl { get; }

    /// <inheritdoc />
    internal override void WriteBody(Utf8JsonWriter writer)
    {
        writer.WriteString("originalContentUrl", OriginalContentUrl);
        writer.WriteString("previewImageUrl", PreviewImageUrl);
    }
}
