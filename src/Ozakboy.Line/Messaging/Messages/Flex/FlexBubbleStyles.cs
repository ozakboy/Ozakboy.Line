using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Messages.Flex;

/// <summary>
/// 氣泡四個區域的樣式(<c>styles</c>)。
/// The styles of a bubble's four areas (<c>styles</c>).
/// </summary>
[JsonConverter(typeof(FlexBubbleStylesJsonConverter))]
public sealed class FlexBubbleStyles
{
    /// <summary>
    /// 標題區樣式。
    /// The header style.
    /// </summary>
    public FlexBlockStyle? Header { get; set; }

    /// <summary>
    /// 主視覺區樣式。
    /// The hero style.
    /// </summary>
    public FlexBlockStyle? Hero { get; set; }

    /// <summary>
    /// 內文區樣式。
    /// The body style.
    /// </summary>
    public FlexBlockStyle? Body { get; set; }

    /// <summary>
    /// 頁尾區樣式。
    /// The footer style.
    /// </summary>
    public FlexBlockStyle? Footer { get; set; }

    /// <summary>
    /// 把整個物件寫成 JSON;null 的欄位不輸出。
    /// Writes the whole object as JSON; null fields are not written.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    internal void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        if (Header is { } headerValue)
        {
            writer.WritePropertyName("header");
            headerValue.WriteTo(writer);
        }
        if (Hero is { } heroValue)
        {
            writer.WritePropertyName("hero");
            heroValue.WriteTo(writer);
        }
        if (Body is { } bodyValue)
        {
            writer.WritePropertyName("body");
            bodyValue.WriteTo(writer);
        }
        if (Footer is { } footerValue)
        {
            writer.WritePropertyName("footer");
            footerValue.WriteTo(writer);
        }
        writer.WriteEndObject();
    }

    /// <summary>
    /// 從 JSON 物件讀回;含有本套件未建模的欄位,或欄位值的型別不符時擲出 <see cref="FlexNotModelledException"/>(呼叫端據此退回原樣保存)。
    /// Reads from a JSON object; throws <see cref="FlexNotModelledException"/> when a field this package does not model is present or a value has the wrong kind (the caller then keeps the node verbatim).
    /// </summary>
    /// <param name="element">JSON 物件。The JSON object.</param>
    /// <returns>讀回的物件。The object read.</returns>
    internal static FlexBubbleStyles Read(JsonElement element)
    {
        var result = new FlexBubbleStyles();

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            switch (property.Name)
            {
                case "header":
                    result.Header = FlexBlockStyle.Read(property.Value);
                    break;
                case "hero":
                    result.Hero = FlexBlockStyle.Read(property.Value);
                    break;
                case "body":
                    result.Body = FlexBlockStyle.Read(property.Value);
                    break;
                case "footer":
                    result.Footer = FlexBlockStyle.Read(property.Value);
                    break;
                default:
                    throw new FlexNotModelledException(property.Name);
            }
        }

        return result;
    }
}
