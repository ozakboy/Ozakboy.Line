using System.Text.Json;

namespace Ozakboy.Line.Messaging.Actions;

/// <summary>
/// 開啟位置選擇畫面的動作。
/// An action that opens the location picker.
/// </summary>
/// <remarks>
/// 與 <see cref="CameraAction"/> 同樣只能用在快速回覆上;使用者送出的位置會以位置訊息進 webhook
/// (<see cref="Ozakboy.Line.Webhook.LineWebhookMessage.Latitude"/> 等欄位)。
/// Like <see cref="CameraAction"/>, this works on a quick reply only; what the user sends arrives as a location
/// message, in <see cref="Ozakboy.Line.Webhook.LineWebhookMessage.Latitude"/> and its neighbours.
/// </remarks>
public sealed class LocationAction : LineAction
{
    /// <inheritdoc />
    public override string Type => "location";

    /// <inheritdoc />
    internal override void WriteBody(Utf8JsonWriter writer)
    {
        // 這個動作只有 type 與 label,沒有專屬欄位。
        // This action has nothing but a type and a label.
    }
}
