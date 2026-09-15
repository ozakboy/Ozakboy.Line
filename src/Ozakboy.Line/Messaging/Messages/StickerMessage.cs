using System.Text.Json;

namespace Ozakboy.Line.Messaging.Messages;

/// <summary>
/// 貼圖訊息。
/// A sticker message.
/// </summary>
/// <remarks>
/// 只能送 LINE 公開清單上的貼圖;自製或需要購買的貼圖包送出去會被拒絕。
/// Only stickers from LINE's published list can be sent; custom or paid sticker packs are refused.
/// </remarks>
public sealed class StickerMessage : LineMessage
{
    /// <summary>
    /// 建立貼圖訊息。
    /// Creates a sticker message.
    /// </summary>
    /// <param name="packageId">貼圖包識別碼。The sticker package identifier.</param>
    /// <param name="stickerId">貼圖識別碼。The sticker identifier.</param>
    /// <exception cref="ArgumentException">
    /// 任一識別碼為 <see langword="null"/> 或空白時擲出。Thrown when either identifier is <see langword="null"/> or blank.
    /// </exception>
    public StickerMessage(string packageId, string stickerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(stickerId);

        PackageId = packageId;
        StickerId = stickerId;
    }

    /// <inheritdoc />
    public override string Type => "sticker";

    /// <summary>
    /// 貼圖包識別碼。
    /// The sticker package identifier.
    /// </summary>
    public string PackageId { get; }

    /// <summary>
    /// 貼圖識別碼。
    /// The sticker identifier.
    /// </summary>
    public string StickerId { get; }

    /// <inheritdoc />
    internal override void WriteBody(Utf8JsonWriter writer)
    {
        writer.WriteString("packageId", PackageId);
        writer.WriteString("stickerId", StickerId);
    }
}
