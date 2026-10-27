using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Messages.Flex;

/// <summary>
/// 填充(<c>filler</c>):吃掉剩餘空間的空元件,常用來把內容推向一側。
/// A filler (<c>filler</c>): an empty component that takes up the remaining space, often used to push content to one side.
/// </summary>
[JsonConverter(typeof(FlexComponentJsonConverterFactory))]
public sealed class FlexFiller : FlexComponent
{
    /// <inheritdoc />
    public override string Type => "filler";

    /// <summary>
    /// 在父方塊主軸方向上占的比例(整數);<c>0</c> 表示不伸縮、只占內容所需的空間。
    /// The share of the parent box's main axis this takes, as an integer; <c>0</c> means it does not stretch and takes only the space its content needs.
    /// </summary>
    public int? Flex { get; set; }

    /// <inheritdoc />
    internal override void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", Type);
        FlexJson.WriteInt(writer, "flex", Flex);
        writer.WriteEndObject();
    }

    /// <summary>
    /// 從 JSON 物件讀回;含有本套件未建模的欄位,或欄位值的型別不符時擲出 <see cref="FlexNotModelledException"/>(呼叫端據此退回原樣保存)。
    /// Reads from a JSON object; throws <see cref="FlexNotModelledException"/> when a field this package does not model is present or a value has the wrong kind (the caller then keeps the node verbatim).
    /// </summary>
    /// <param name="element">JSON 物件。The JSON object.</param>
    /// <returns>讀回的物件。The object read.</returns>
    internal static FlexFiller Read(JsonElement element)
    {
        var result = new FlexFiller();

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
                case "flex":
                    result.Flex = FlexReader.ReadInt(property.Value);
                    break;
                default:
                    throw new FlexNotModelledException(property.Name);
            }
        }

        return result;
    }
}
