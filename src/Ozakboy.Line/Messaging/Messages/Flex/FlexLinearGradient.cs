using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Messages.Flex;

/// <summary>
/// 方塊的線性漸層背景(<c>background</c>)。
/// A box's linear-gradient background (<c>background</c>).
/// </summary>
[JsonConverter(typeof(FlexLinearGradientJsonConverter))]
public sealed class FlexLinearGradient
{
    /// <summary>
    /// 漸層角度,例如 <c>90deg</c>。
    /// The gradient angle, such as <c>90deg</c>.
    /// </summary>
    public required string Angle { get; set; }

    /// <summary>
    /// 起始顏色。
    /// The start colour.
    /// </summary>
    public required string StartColor { get; set; }

    /// <summary>
    /// 結束顏色。
    /// The end colour.
    /// </summary>
    public required string EndColor { get; set; }

    /// <summary>
    /// 中間顏色;不設定時只有起訖兩色。
    /// The centre colour; without it only the start and end colours are used.
    /// </summary>
    public string? CenterColor { get; set; }

    /// <summary>
    /// 中間顏色的位置,百分比,例如 <c>50%</c>。
    /// The position of the centre colour as a percentage, such as <c>50%</c>.
    /// </summary>
    public string? CenterPosition { get; set; }

    /// <summary>
    /// 把整個物件寫成 JSON;null 的欄位不輸出。
    /// Writes the whole object as JSON; null fields are not written.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    internal void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "linearGradient");
        writer.WriteString("angle", Angle);
        writer.WriteString("startColor", StartColor);
        writer.WriteString("endColor", EndColor);
        FlexJson.WriteString(writer, "centerColor", CenterColor);
        FlexJson.WriteString(writer, "centerPosition", CenterPosition);
        writer.WriteEndObject();
    }

    /// <summary>
    /// 從 JSON 物件讀回;含有本套件未建模的欄位,或欄位值的型別不符時擲出 <see cref="FlexNotModelledException"/>(呼叫端據此退回原樣保存)。
    /// Reads from a JSON object; throws <see cref="FlexNotModelledException"/> when a field this package does not model is present or a value has the wrong kind (the caller then keeps the node verbatim).
    /// </summary>
    /// <param name="element">JSON 物件。The JSON object.</param>
    /// <returns>讀回的物件。The object read.</returns>
    internal static FlexLinearGradient Read(JsonElement element)
    {
        var result = new FlexLinearGradient { Angle = string.Empty, StartColor = string.Empty, EndColor = string.Empty };
        var seenAngle = false;
        var seenStartColor = false;
        var seenEndColor = false;

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
                case "angle":
                    result.Angle = FlexReader.ReadString(property.Value);
                    seenAngle = true;
                    break;
                case "startColor":
                    result.StartColor = FlexReader.ReadString(property.Value);
                    seenStartColor = true;
                    break;
                case "endColor":
                    result.EndColor = FlexReader.ReadString(property.Value);
                    seenEndColor = true;
                    break;
                case "centerColor":
                    result.CenterColor = FlexReader.ReadString(property.Value);
                    break;
                case "centerPosition":
                    result.CenterPosition = FlexReader.ReadString(property.Value);
                    break;
                default:
                    throw new FlexNotModelledException(property.Name);
            }
        }

        if (!seenAngle)
        {
            throw new FlexNotModelledException("angle");
        }

        if (!seenStartColor)
        {
            throw new FlexNotModelledException("startColor");
        }

        if (!seenEndColor)
        {
            throw new FlexNotModelledException("endColor");
        }

        return result;
    }
}
