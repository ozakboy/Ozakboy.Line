using System.Text.Json.Serialization;
using System.Text.Json;
using Ozakboy.Core.Abstractions;

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
[JsonConverter(typeof(LineMessageJsonConverterFactory))]
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

    /// <summary>
    /// 送出前的本地檢查:兩個位址都必須是 https:// 而且不超過 2000 字。
    /// The local check before sending: both addresses must be https:// and at most 2000 characters.
    /// </summary>
    /// <returns>
    /// 通過時為成功,否則為 <see cref="LineErrorCodes.InvalidMessage"/> 失敗(快速回覆的問題照該處的代碼)。
    /// Success when it passes, otherwise an <see cref="LineErrorCodes.InvalidMessage"/> failure (quick reply problems keep their own codes).
    /// </returns>
    public override Result Validate()
    {
        var baseResult = base.Validate();
        if (baseResult.IsFailure)
        {
            return baseResult;
        }

        if (LineMessageValidation.CheckMediaUrl("圖片訊息的 originalContentUrl", OriginalContentUrl) is { } original)
        {
            return LineMessageValidation.InvalidMessage(original);
        }

        return LineMessageValidation.CheckMediaUrl("圖片訊息的 previewImageUrl", PreviewImageUrl) is { } preview
            ? LineMessageValidation.InvalidMessage(preview)
            : Result.Success();
    }

    /// <inheritdoc />
    internal override void WriteBody(Utf8JsonWriter writer)
    {
        writer.WriteString("originalContentUrl", OriginalContentUrl);
        writer.WriteString("previewImageUrl", PreviewImageUrl);
    }
}
