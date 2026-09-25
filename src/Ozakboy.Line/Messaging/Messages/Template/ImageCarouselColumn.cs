using System.Text.Json;
using Ozakboy.Line.Messaging.Actions;

namespace Ozakboy.Line.Messaging.Messages.Template;

/// <summary>
/// 圖片輪播範本的一欄:一張圖與點下去的動作。
/// One column of an image carousel template: an image and the action for a tap.
/// </summary>
/// <remarks>
/// 欄位對應 LINE 的 <c>imageUrl</c>(HTTPS、JPEG 或 PNG、1:1)與 <c>action</c>。
/// The fields map to LINE's <c>imageUrl</c> (HTTPS, JPEG or PNG, 1:1) and <c>action</c>.
/// </remarks>
public sealed class ImageCarouselColumn
{
    /// <summary>
    /// 建立一欄。
    /// Creates a column.
    /// </summary>
    /// <param name="imageUrl">圖片位址。The image address.</param>
    /// <param name="action">點下去的動作。The action for a tap.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="imageUrl"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="imageUrl"/> is <see langword="null"/> or blank.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="action"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="action"/> is <see langword="null"/>.
    /// </exception>
    public ImageCarouselColumn(string imageUrl, LineAction action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageUrl);
        ArgumentNullException.ThrowIfNull(action);

        ImageUrl = imageUrl;
        Action = action;
    }

    /// <summary>
    /// 圖片位址。
    /// The image address.
    /// </summary>
    public string ImageUrl { get; }

    /// <summary>
    /// 點下去的動作。
    /// The action for a tap.
    /// </summary>
    public LineAction Action { get; }

    /// <summary>
    /// 寫出這一欄。
    /// Writes this column.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    internal void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("imageUrl", ImageUrl);
        writer.WritePropertyName("action");
        Action.WriteTo(writer);
        writer.WriteEndObject();
    }
}
