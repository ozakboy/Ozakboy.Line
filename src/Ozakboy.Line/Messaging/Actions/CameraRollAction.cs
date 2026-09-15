using System.Text.Json;

namespace Ozakboy.Line.Messaging.Actions;

/// <summary>
/// 開啟相簿的動作。
/// An action that opens the photo library.
/// </summary>
/// <remarks>
/// 與 <see cref="CameraAction"/> 同樣只能用在快速回覆上。
/// Like <see cref="CameraAction"/>, this works on a quick reply only.
/// </remarks>
public sealed class CameraRollAction : LineAction
{
    /// <inheritdoc />
    public override string Type => "cameraRoll";

    /// <inheritdoc />
    internal override void WriteBody(Utf8JsonWriter writer)
    {
        // 這個動作只有 type 與 label,沒有專屬欄位。
        // This action has nothing but a type and a label.
    }
}
