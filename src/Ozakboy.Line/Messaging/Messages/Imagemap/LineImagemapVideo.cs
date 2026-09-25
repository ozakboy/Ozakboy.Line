using System.Text.Json;

namespace Ozakboy.Line.Messaging.Messages.Imagemap;

/// <summary>
/// 疊在圖片地圖上播放的影片,以及影片播完後顯示的連結。
/// A video played on top of an imagemap, plus the link shown once it finishes.
/// </summary>
/// <remarks>
/// 欄位對應 LINE 的 <c>video</c> 物件:<c>originalContentUrl</c>(HTTPS、MP4)、<c>previewImageUrl</c>
/// (HTTPS、JPEG 或 PNG)、<c>area</c>,與可選的 <c>externalLink</c>(<c>linkUri</c> 與 <c>label</c>,
/// 兩個要一起給)。
/// The fields map to LINE's <c>video</c> object: <c>originalContentUrl</c> (HTTPS, MP4),
/// <c>previewImageUrl</c> (HTTPS, JPEG or PNG), <c>area</c>, and the optional <c>externalLink</c>
/// (<c>linkUri</c> and <c>label</c>, which go together).
/// </remarks>
public sealed class LineImagemapVideo
{
    /// <summary>
    /// 建立影片設定。
    /// Creates the video settings.
    /// </summary>
    /// <param name="originalContentUrl">影片位址。The video address.</param>
    /// <param name="previewImageUrl">預覽圖位址。The preview image address.</param>
    /// <param name="area">影片顯示的範圍。The area the video plays in.</param>
    /// <exception cref="ArgumentException">
    /// 任一位址為 <see langword="null"/> 或空白時擲出。Thrown when either address is <see langword="null"/> or blank.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="area"/> 為 <see langword="null"/> 時擲出。Thrown when <paramref name="area"/> is <see langword="null"/>.
    /// </exception>
    public LineImagemapVideo(string originalContentUrl, string previewImageUrl, LineImagemapArea area)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalContentUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(previewImageUrl);
        ArgumentNullException.ThrowIfNull(area);

        OriginalContentUrl = originalContentUrl;
        PreviewImageUrl = previewImageUrl;
        Area = area;
    }

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

    /// <summary>
    /// 影片顯示的範圍。
    /// The area the video plays in.
    /// </summary>
    public LineImagemapArea Area { get; }

    /// <summary>
    /// 影片播完後顯示的連結位址;不顯示時為 <see langword="null"/>。要與 <see cref="ExternalLinkLabel"/> 一起給。
    /// The address of the link shown after the video, or <see langword="null"/> for none. Goes together with
    /// <see cref="ExternalLinkLabel"/>.
    /// </summary>
    public string? ExternalLinkUri { get; set; }

    /// <summary>
    /// 影片播完後顯示的連結文字(最長 30 字元)。
    /// The text of the link shown after the video, up to 30 characters.
    /// </summary>
    public string? ExternalLinkLabel { get; set; }

    /// <summary>
    /// 寫出 <c>video</c> 物件。
    /// Writes the <c>video</c> object.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    internal void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("originalContentUrl", OriginalContentUrl);
        writer.WriteString("previewImageUrl", PreviewImageUrl);
        writer.WritePropertyName("area");
        Area.WriteTo(writer);

        if (!string.IsNullOrWhiteSpace(ExternalLinkUri) && !string.IsNullOrWhiteSpace(ExternalLinkLabel))
        {
            writer.WriteStartObject("externalLink");
            writer.WriteString("linkUri", ExternalLinkUri);
            writer.WriteString("label", ExternalLinkLabel);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }
}
