using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Messages.Flex;

/// <summary>
/// 輪播容器(<c>carousel</c>):多個 <see cref="FlexBubble"/> 橫向排列。
/// The carousel container (<c>carousel</c>): several <see cref="FlexBubble"/>s laid out side by side.
/// </summary>
/// <remarks>
/// LINE 限制輪播最多 12 個氣泡;這個上限不在這一版的模型裡檢查。
/// LINE caps a carousel at 12 bubbles; this version of the model does not check that limit.
/// </remarks>
[JsonConverter(typeof(FlexContainerJsonConverterFactory))]
public sealed class FlexCarousel : FlexContainer
{
    /// <inheritdoc />
    public override string Type => "carousel";

    /// <summary>
    /// 輪播裡的氣泡。
    /// The bubbles in the carousel.
    /// </summary>
    public IList<FlexBubble> Contents { get; init; } = [];

    /// <inheritdoc />
    internal override void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", Type);
        writer.WriteStartArray("contents");
        foreach (var item in Contents)
        {
            item.WriteTo(writer);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    /// <summary>
    /// 從 JSON 物件讀回;含有本套件未建模的欄位,或欄位值的型別不符時擲出 <see cref="FlexNotModelledException"/>(呼叫端據此退回原樣保存)。
    /// Reads from a JSON object; throws <see cref="FlexNotModelledException"/> when a field this package does not model is present or a value has the wrong kind (the caller then keeps the node verbatim).
    /// </summary>
    /// <param name="element">JSON 物件。The JSON object.</param>
    /// <returns>讀回的物件。The object read.</returns>
    internal static FlexCarousel Read(JsonElement element)
    {
        var result = new FlexCarousel();

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
                case "contents":
                    FlexReader.ReadArray(property.Value, result.Contents, FlexReader.ReadBubble);
                    break;
                default:
                    throw new FlexNotModelledException(property.Name);
            }
        }

        return result;
    }
}
