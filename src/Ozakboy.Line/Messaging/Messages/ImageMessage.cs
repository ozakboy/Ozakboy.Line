using System.Text.Json;

namespace Ozakboy.Line.Messaging.Messages;

/// <summary>
/// 圖片訊息。
/// An image message.
/// </summary>
/// <remarks>
/// 兩個位址都必須是 HTTPS,而且由 LINE 的伺服器來抓 —— 內網位址或需要登入的位址不會有任何錯誤訊息,
/// 使用者看到的就是一張載不出來的圖。
/// Both addresses must be HTTPS and are fetched by LINE's servers: an address on a private network, or one behind
/// a login, produces no error at all — the user simply sees an image that never loads.
/// </remarks>
public sealed class ImageMessage : LineMessage
{
    /// <summary>
    /// 建立圖片訊息。
    /// Creates an image message.
    /// </summary>
    /// <param name="originalContentUrl">原圖位址。The full-size image address.</param>
    /// <param name="previewImageUrl">預覽圖位址。The preview image address.</param>
    /// <exception cref="ArgumentException">
    /// 任一位址為 <see langword="null"/> 或空白時擲出。Thrown when either address is <see langword="null"/> or blank.
    /// </exception>
    public ImageMessage(string originalContentUrl, string previewImageUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalContentUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(previewImageUrl);

        OriginalContentUrl = originalContentUrl;
        PreviewImageUrl = previewImageUrl;
    }

    /// <inheritdoc />
    public override string Type => "image";

    /// <summary>
    /// 原圖位址。
    /// The full-size image address.
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
