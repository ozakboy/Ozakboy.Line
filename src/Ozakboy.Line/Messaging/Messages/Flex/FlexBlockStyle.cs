using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Messages.Flex;

/// <summary>
/// 氣泡某個區域的樣式(<c>styles.header</c> 等)。
/// The style of one bubble area (<c>styles.header</c> and the like).
/// </summary>
[JsonConverter(typeof(FlexBlockStyleJsonConverter))]
public sealed class FlexBlockStyle
{
    /// <summary>
    /// 該區域的背景色。
    /// The area's background colour.
    /// </summary>
    public string? BackgroundColor { get; set; }

    /// <summary>
    /// 是否在該區域上緣畫分隔線。
    /// Whether a separator line is drawn along the area's top edge.
    /// </summary>
    public bool? Separator { get; set; }

    /// <summary>
    /// 分隔線顏色。
    /// The separator colour.
    /// </summary>
    public string? SeparatorColor { get; set; }

    /// <summary>
    /// 把整個物件寫成 JSON;null 的欄位不輸出。
    /// Writes the whole object as JSON; null fields are not written.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    internal void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        FlexJson.WriteString(writer, "backgroundColor", BackgroundColor);
        FlexJson.WriteBool(writer, "separator", Separator);
        FlexJson.WriteString(writer, "separatorColor", SeparatorColor);
        writer.WriteEndObject();
    }

    /// <summary>
    /// 從 JSON 物件讀回;含有本套件未建模的欄位,或欄位值的型別不符時擲出 <see cref="FlexNotModelledException"/>(呼叫端據此退回原樣保存)。
    /// Reads from a JSON object; throws <see cref="FlexNotModelledException"/> when a field this package does not model is present or a value has the wrong kind (the caller then keeps the node verbatim).
    /// </summary>
    /// <param name="element">JSON 物件。The JSON object.</param>
    /// <returns>讀回的物件。The object read.</returns>
    internal static FlexBlockStyle Read(JsonElement element)
    {
        var result = new FlexBlockStyle();

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            switch (property.Name)
            {
                case "backgroundColor":
                    result.BackgroundColor = FlexReader.ReadString(property.Value);
                    break;
                case "separator":
                    result.Separator = FlexReader.ReadBool(property.Value);
                    break;
                case "separatorColor":
                    result.SeparatorColor = FlexReader.ReadString(property.Value);
                    break;
                default:
                    throw new FlexNotModelledException(property.Name);
            }
        }

        return result;
    }
}
