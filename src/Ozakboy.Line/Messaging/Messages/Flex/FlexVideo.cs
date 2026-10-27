using System.Text.Json;
using System.Text.Json.Serialization;
using Ozakboy.Line.Messaging.Actions;

namespace Ozakboy.Line.Messaging.Messages.Flex;

/// <summary>
/// 影片(<c>video</c>):只能放在氣泡的 hero 區。
/// A video (<c>video</c>): allowed only in a bubble's hero area.
/// </summary>
/// <remarks>
/// <see cref="AltContent"/> 是影片無法播放時顯示的替代內容,LINE 要求它是圖片或方塊。
/// <see cref="AltContent"/> is the fallback shown when the video cannot play; LINE requires it to be an image or a box.
/// </remarks>
[JsonConverter(typeof(FlexComponentJsonConverterFactory))]
public sealed class FlexVideo : FlexComponent
{
    /// <summary>
    /// 建立影片元件。
    /// Creates a video component.
    /// </summary>
    /// <param name="url">影片位址(HTTPS)。The video address (HTTPS).</param>
    /// <param name="previewUrl">預覽圖位址(HTTPS)。The preview image address (HTTPS).</param>
    /// <param name="altContent">無法播放時的替代內容(圖片或方塊)。The fallback content (an image or a box).</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="url"/> 或 <paramref name="previewUrl"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="url"/> or <paramref name="previewUrl"/> is <see langword="null"/> or blank.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="altContent"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="altContent"/> is <see langword="null"/>.
    /// </exception>
    public FlexVideo(string url, string previewUrl, FlexComponent altContent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(previewUrl);
        ArgumentNullException.ThrowIfNull(altContent);
        Url = url;
        PreviewUrl = previewUrl;
        AltContent = altContent;
    }

    /// <summary>
    /// 供讀回 JSON 時使用,必填欄位稍後再填。
    /// For reading JSON back; the required fields are filled in afterwards.
    /// </summary>
    internal FlexVideo()
    {
        Url = string.Empty;
        PreviewUrl = string.Empty;
        AltContent = null!;
    }

    /// <inheritdoc />
    public override string Type => "video";

    /// <summary>
    /// 影片位址(HTTPS)。
    /// The video address (HTTPS).
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// 預覽圖位址(HTTPS)。
    /// The preview image address (HTTPS).
    /// </summary>
    public string PreviewUrl { get; set; }

    /// <summary>
    /// 影片無法播放時顯示的替代內容(圖片或方塊)。
    /// The fallback content shown when the video cannot play (an image or a box).
    /// </summary>
    public FlexComponent AltContent { get; set; }

    /// <summary>
    /// 長寬比,格式為 <c>寬:高</c>(見 <see cref="FlexAspectRatio"/>)。
    /// The aspect ratio as <c>width:height</c> (see <see cref="FlexAspectRatio"/>).
    /// </summary>
    public string? AspectRatio { get; set; }

    /// <summary>
    /// 影片播放結束後出現的連結動作;只接受 URI 動作。
    /// The link action shown when the video ends; only a URI action is accepted.
    /// </summary>
    public UriAction? Action { get; set; }

    /// <inheritdoc />
    internal override void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", Type);
        writer.WriteString("url", Url);
        writer.WriteString("previewUrl", PreviewUrl);
        writer.WritePropertyName("altContent");
        AltContent.WriteTo(writer);
        FlexJson.WriteString(writer, "aspectRatio", AspectRatio);
        if (Action is { } actionValue)
        {
            writer.WritePropertyName("action");
            actionValue.WriteTo(writer);
        }
        writer.WriteEndObject();
    }

    /// <summary>
    /// 從 JSON 物件讀回;含有本套件未建模的欄位,或欄位值的型別不符時擲出 <see cref="FlexNotModelledException"/>(呼叫端據此退回原樣保存)。
    /// Reads from a JSON object; throws <see cref="FlexNotModelledException"/> when a field this package does not model is present or a value has the wrong kind (the caller then keeps the node verbatim).
    /// </summary>
    /// <param name="element">JSON 物件。The JSON object.</param>
    /// <returns>讀回的物件。The object read.</returns>
    internal static FlexVideo Read(JsonElement element)
    {
        var result = new FlexVideo();
        var seenUrl = false;
        var seenPreviewUrl = false;
        var seenAltContent = false;

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            switch (property.Name)
            {
                case "type":
                    break;
                case "url":
                    result.Url = FlexReader.ReadString(property.Value);
                    seenUrl = true;
                    break;
                case "previewUrl":
                    result.PreviewUrl = FlexReader.ReadString(property.Value);
                    seenPreviewUrl = true;
                    break;
                case "altContent":
                    result.AltContent = FlexReader.ReadComponent(property.Value);
                    seenAltContent = true;
                    break;
                case "aspectRatio":
                    result.AspectRatio = FlexReader.ReadString(property.Value);
                    break;
                case "action":
                    result.Action = FlexReader.ReadUriAction(property.Value);
                    break;
                default:
                    throw new FlexNotModelledException(property.Name);
            }
        }

        if (!seenUrl)
        {
            throw new FlexNotModelledException("url");
        }

        if (!seenPreviewUrl)
        {
            throw new FlexNotModelledException("previewUrl");
        }

        if (!seenAltContent)
        {
            throw new FlexNotModelledException("altContent");
        }

        return result;
    }
}
