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

    /// <summary>
    /// 追蹤識別碼;設定後使用者看完影片時 LINE 會送 <c>videoPlayComplete</c> 事件,帶著同一個值回來。
    /// The tracking id. When set, LINE sends a <c>videoPlayComplete</c> event carrying this same value once the
    /// user finishes watching.
    /// </summary>
    /// <remarks>
    /// 對應 LINE 的 <c>trackingId</c> 欄位(最長 100 字元),有值才輸出;事件那頭由
    /// <see cref="Ozakboy.Line.Webhook.LineWebhookVideoPlayComplete.TrackingId"/> 接住。LINE 註明這個欄位
    /// 只能用在一對一聊天,送進群組或聊天室會被退回。
    /// Maps to LINE's <c>trackingId</c> field (up to 100 characters) and is written only when set; the event side
    /// arrives as <see cref="Ozakboy.Line.Webhook.LineWebhookVideoPlayComplete.TrackingId"/>. LINE notes that the
    /// field is for one-to-one chats only: sent to a group or room, the message is rejected.
    /// </remarks>
    public string? TrackingId { get; set; }

    /// <inheritdoc />
    internal override void WriteBody(Utf8JsonWriter writer)
    {
        writer.WriteString("originalContentUrl", OriginalContentUrl);
        writer.WriteString("previewImageUrl", PreviewImageUrl);

        if (!string.IsNullOrWhiteSpace(TrackingId))
        {
            writer.WriteString("trackingId", TrackingId);
        }
    }
}
