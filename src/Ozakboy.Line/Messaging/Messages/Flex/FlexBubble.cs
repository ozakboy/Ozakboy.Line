using System.Text.Json;
using System.Text.Json.Serialization;
using Ozakboy.Line.Messaging.Actions;

namespace Ozakboy.Line.Messaging.Messages.Flex;

/// <summary>
/// 氣泡容器(<c>bubble</c>):單一個 Flex 區塊,由 header、hero、body、footer 四個區域組成。
/// The bubble container (<c>bubble</c>): one Flex block made of header, hero, body and footer areas.
/// </summary>
/// <remarks>
/// hero 可以是圖片、方塊或影片(<see cref="FlexComponent"/>),header、body、footer 一律是 <see cref="FlexBox"/>。
/// The hero may be an image, a box or a video (a <see cref="FlexComponent"/>), while header, body and footer are always a <see cref="FlexBox"/>.
/// </remarks>
[JsonConverter(typeof(FlexContainerJsonConverterFactory))]
public sealed class FlexBubble : FlexContainer
{
    /// <inheritdoc />
    public override string Type => "bubble";

    /// <summary>
    /// 氣泡寬度,見 <see cref="FlexBubbleSize"/>。
    /// The bubble width, see <see cref="FlexBubbleSize"/>.
    /// </summary>
    public string? Size { get; set; }

    /// <summary>
    /// 文字書寫方向,見 <see cref="FlexDirection"/>。
    /// The text direction, see <see cref="FlexDirection"/>.
    /// </summary>
    public string? Direction { get; set; }

    /// <summary>
    /// 標題區。
    /// The header area.
    /// </summary>
    public FlexBox? Header { get; set; }

    /// <summary>
    /// 主視覺區:圖片、方塊或影片。
    /// The hero area: an image, a box or a video.
    /// </summary>
    public FlexComponent? Hero { get; set; }

    /// <summary>
    /// 內文區。
    /// The body area.
    /// </summary>
    public FlexBox? Body { get; set; }

    /// <summary>
    /// 頁尾區。
    /// The footer area.
    /// </summary>
    public FlexBox? Footer { get; set; }

    /// <summary>
    /// 各區域的樣式。
    /// The style of each area.
    /// </summary>
    public FlexBubbleStyles? Styles { get; set; }

    /// <summary>
    /// 點擊氣泡時觸發的動作(點到有自己動作的元件時以元件為準)。
    /// The action triggered by a tap on the bubble (a component with its own action takes precedence).
    /// </summary>
    public LineAction? Action { get; set; }

    /// <inheritdoc />
    internal override void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", Type);
        FlexJson.WriteString(writer, "size", Size);
        FlexJson.WriteString(writer, "direction", Direction);
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
        if (Styles is { } stylesValue)
        {
            writer.WritePropertyName("styles");
            stylesValue.WriteTo(writer);
        }
        if (Action is { } actionValue)
        {
            writer.WritePropertyName("action");
            actionValue.WriteTo(writer);
        }
        writer.WriteEndObject();
    }

    /// <summary>
    /// 從 JSON 物件讀回;含有本套件未建模的欄位,或欄位值的型別不符時擲出 <see cref="FlexNotModelledException"/>(呼叫端據此退回原樣保存)。
    /// Reads from a JSON object; throws <see cref="FlexNotModelledException"/> when a field this package does not model is present or a value has the wrong kind (the caller then keeps the node verbatim).
    /// </summary>
    /// <param name="element">JSON 物件。The JSON object.</param>
    /// <returns>讀回的物件。The object read.</returns>
    internal static FlexBubble Read(JsonElement element)
    {
        var result = new FlexBubble();

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
                case "size":
                    result.Size = FlexReader.ReadString(property.Value);
                    break;
                case "direction":
                    result.Direction = FlexReader.ReadString(property.Value);
                    break;
                case "header":
                    result.Header = FlexReader.ReadBox(property.Value);
                    break;
                case "hero":
                    result.Hero = FlexReader.ReadComponent(property.Value);
                    break;
                case "body":
                    result.Body = FlexReader.ReadBox(property.Value);
                    break;
                case "footer":
                    result.Footer = FlexReader.ReadBox(property.Value);
                    break;
                case "styles":
                    result.Styles = FlexBubbleStyles.Read(property.Value);
                    break;
                case "action":
                    result.Action = FlexReader.ReadAction(property.Value);
                    break;
                default:
                    throw new FlexNotModelledException(property.Name);
            }
        }

        return result;
    }
}
