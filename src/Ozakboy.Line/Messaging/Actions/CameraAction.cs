using System.Text.Json;

namespace Ozakboy.Line.Messaging.Actions;

/// <summary>
/// 開啟相機的動作。
/// An action that opens the camera.
/// </summary>
/// <remarks>
/// 只能用在快速回覆上,圖文選單放這個動作 LINE 會拒絕整份選單。拍完的照片以一般的圖片訊息進 webhook,
/// 沒有任何欄位說明「這張是從哪個按鈕來的」—— 要對得起來,得靠對話當下的狀態自己記。
/// This works on a quick reply only; a rich menu carrying it has LINE refuse the whole menu. The photo arrives as
/// an ordinary image message with nothing to say which button produced it, so matching them up means keeping the
/// conversation's state yourself.
/// </remarks>
public sealed class CameraAction : LineAction
{
    /// <inheritdoc />
    public override string Type => "camera";

    /// <inheritdoc />
    internal override void WriteBody(Utf8JsonWriter writer)
    {
        // 這個動作只有 type 與 label,沒有專屬欄位。
        // This action has nothing but a type and a label.
    }
}
