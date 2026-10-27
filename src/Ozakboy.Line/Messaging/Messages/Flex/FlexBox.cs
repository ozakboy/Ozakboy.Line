using System.Text.Json;
using System.Text.Json.Serialization;
using Ozakboy.Line.Messaging.Actions;

namespace Ozakboy.Line.Messaging.Messages.Flex;

/// <summary>
/// 方塊(<c>box</c>):排列其他元件的容器,可巢狀。
/// A box (<c>box</c>): a container that lays out other components and can be nested.
/// </summary>
/// <remarks>
/// <see cref="Layout"/> 必填;<see cref="Contents"/> 一律會寫出(空方塊寫成 <c>[]</c>),因為 LINE 要求這個欄位存在。
/// <see cref="Layout"/> is required; <see cref="Contents"/> is always written (an empty box as <c>[]</c>) because LINE requires the field to exist.
/// </remarks>
[JsonConverter(typeof(FlexComponentJsonConverterFactory))]
public sealed class FlexBox : FlexComponent
{
    /// <inheritdoc />
    public override string Type => "box";

    /// <summary>
    /// 內容的排列方向,見 <see cref="FlexLayout"/>。
    /// The direction contents are laid out in, see <see cref="FlexLayout"/>.
    /// </summary>
    public required string Layout { get; set; }

    /// <summary>
    /// 方塊裡的元件。
    /// The components inside the box.
    /// </summary>
    public IList<FlexComponent> Contents { get; init; } = [];

    /// <summary>
    /// 在父方塊主軸方向上占的比例(整數);<c>0</c> 表示不伸縮、只占內容所需的空間。
    /// The share of the parent box's main axis this takes, as an integer; <c>0</c> means it does not stretch and takes only the space its content needs.
    /// </summary>
    public int? Flex { get; set; }

    /// <summary>
    /// 子元件之間的間距:關鍵字見 <see cref="FlexSpacing"/>,或像素量值。
    /// The gap between child components: a keyword from <see cref="FlexSpacing"/>, or a pixel value.
    /// </summary>
    public string? Spacing { get; set; }

    /// <summary>
    /// 與前一個元件之間的間距:關鍵字見 <see cref="FlexSpacing"/>,或像素量值(如 <c>20px</c>)。
    /// The gap before the previous component: a keyword from <see cref="FlexSpacing"/>, or a pixel value such as <c>20px</c>.
    /// </summary>
    public string? Margin { get; set; }

    /// <summary>
    /// 定位方式,見 <see cref="FlexPosition"/>;<c>absolute</c> 時搭配 <c>Offset*</c>。
    /// The positioning mode, see <see cref="FlexPosition"/>; with <c>absolute</c> it works together with <c>Offset*</c>.
    /// </summary>
    public string? Position { get; set; }

    /// <summary>
    /// 距離上緣的位移:關鍵字見 <see cref="FlexSpacing"/>,或像素量值。
    /// The offset from the top edge: a keyword from <see cref="FlexSpacing"/>, or a pixel value.
    /// </summary>
    public string? OffsetTop { get; set; }

    /// <summary>
    /// 距離下緣的位移:關鍵字見 <see cref="FlexSpacing"/>,或像素量值。
    /// The offset from the bottom edge: a keyword from <see cref="FlexSpacing"/>, or a pixel value.
    /// </summary>
    public string? OffsetBottom { get; set; }

    /// <summary>
    /// 距離起始側的位移:關鍵字見 <see cref="FlexSpacing"/>,或像素量值。
    /// The offset from the start edge: a keyword from <see cref="FlexSpacing"/>, or a pixel value.
    /// </summary>
    public string? OffsetStart { get; set; }

    /// <summary>
    /// 距離結束側的位移:關鍵字見 <see cref="FlexSpacing"/>,或像素量值。
    /// The offset from the end edge: a keyword from <see cref="FlexSpacing"/>, or a pixel value.
    /// </summary>
    public string? OffsetEnd { get; set; }

    /// <summary>
    /// 背景色,<c>#RRGGBB</c> 或 <c>#RRGGBBAA</c>。
    /// The background colour as <c>#RRGGBB</c> or <c>#RRGGBBAA</c>.
    /// </summary>
    public string? BackgroundColor { get; set; }

    /// <summary>
    /// 框線顏色。
    /// The border colour.
    /// </summary>
    public string? BorderColor { get; set; }

    /// <summary>
    /// 框線粗細:關鍵字見 <see cref="FlexBorderWidth"/>,或像素量值。
    /// The border width: a keyword from <see cref="FlexBorderWidth"/>, or a pixel value.
    /// </summary>
    public string? BorderWidth { get; set; }

    /// <summary>
    /// 圓角半徑:關鍵字見 <see cref="FlexCornerRadius"/>,或像素量值。
    /// The corner radius: a keyword from <see cref="FlexCornerRadius"/>, or a pixel value.
    /// </summary>
    public string? CornerRadius { get; set; }

    /// <summary>
    /// 寬度,像素(<c>100px</c>)或百分比(<c>50%</c>)。
    /// The width, in pixels (<c>100px</c>) or as a percentage (<c>50%</c>).
    /// </summary>
    public string? Width { get; set; }

    /// <summary>
    /// 最大寬度,像素或百分比。
    /// The maximum width, in pixels or as a percentage.
    /// </summary>
    public string? MaxWidth { get; set; }

    /// <summary>
    /// 高度,像素或百分比。
    /// The height, in pixels or as a percentage.
    /// </summary>
    public string? Height { get; set; }

    /// <summary>
    /// 最大高度,像素或百分比。
    /// The maximum height, in pixels or as a percentage.
    /// </summary>
    public string? MaxHeight { get; set; }

    /// <summary>
    /// 四邊的內距:關鍵字見 <see cref="FlexSpacing"/>,或像素量值。
    /// The padding on all four sides: a keyword from <see cref="FlexSpacing"/>, or a pixel value.
    /// </summary>
    public string? PaddingAll { get; set; }

    /// <summary>
    /// 上內距。
    /// The top padding.
    /// </summary>
    public string? PaddingTop { get; set; }

    /// <summary>
    /// 下內距。
    /// The bottom padding.
    /// </summary>
    public string? PaddingBottom { get; set; }

    /// <summary>
    /// 起始側內距。
    /// The start-side padding.
    /// </summary>
    public string? PaddingStart { get; set; }

    /// <summary>
    /// 結束側內距。
    /// The end-side padding.
    /// </summary>
    public string? PaddingEnd { get; set; }

    /// <summary>
    /// 主軸方向的內容分佈,見 <see cref="FlexJustifyContent"/>。
    /// How content is distributed along the main axis, see <see cref="FlexJustifyContent"/>.
    /// </summary>
    public string? JustifyContent { get; set; }

    /// <summary>
    /// 交叉軸方向的元件對齊,見 <see cref="FlexAlignItems"/>。
    /// How components align along the cross axis, see <see cref="FlexAlignItems"/>.
    /// </summary>
    public string? AlignItems { get; set; }

    /// <summary>
    /// 漸層背景。
    /// The gradient background.
    /// </summary>
    public FlexLinearGradient? Background { get; set; }

    /// <summary>
    /// 點擊時觸發的動作;沿用 <see cref="LineAction"/>(從 JSON 讀回時為 <see cref="RawAction"/>)。
    /// The action triggered on a tap; reuses <see cref="LineAction"/> (a <see cref="RawAction"/> when read back from JSON).
    /// </summary>
    public LineAction? Action { get; set; }

    /// <inheritdoc />
    internal override void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", Type);
        writer.WriteString("layout", Layout);
        writer.WriteStartArray("contents");
        foreach (var item in Contents)
        {
            item.WriteTo(writer);
        }

        writer.WriteEndArray();
        FlexJson.WriteInt(writer, "flex", Flex);
        FlexJson.WriteString(writer, "spacing", Spacing);
        FlexJson.WriteString(writer, "margin", Margin);
        FlexJson.WriteString(writer, "position", Position);
        FlexJson.WriteString(writer, "offsetTop", OffsetTop);
        FlexJson.WriteString(writer, "offsetBottom", OffsetBottom);
        FlexJson.WriteString(writer, "offsetStart", OffsetStart);
        FlexJson.WriteString(writer, "offsetEnd", OffsetEnd);
        FlexJson.WriteString(writer, "backgroundColor", BackgroundColor);
        FlexJson.WriteString(writer, "borderColor", BorderColor);
        FlexJson.WriteString(writer, "borderWidth", BorderWidth);
        FlexJson.WriteString(writer, "cornerRadius", CornerRadius);
        FlexJson.WriteString(writer, "width", Width);
        FlexJson.WriteString(writer, "maxWidth", MaxWidth);
        FlexJson.WriteString(writer, "height", Height);
        FlexJson.WriteString(writer, "maxHeight", MaxHeight);
        FlexJson.WriteString(writer, "paddingAll", PaddingAll);
        FlexJson.WriteString(writer, "paddingTop", PaddingTop);
        FlexJson.WriteString(writer, "paddingBottom", PaddingBottom);
        FlexJson.WriteString(writer, "paddingStart", PaddingStart);
        FlexJson.WriteString(writer, "paddingEnd", PaddingEnd);
        FlexJson.WriteString(writer, "justifyContent", JustifyContent);
        FlexJson.WriteString(writer, "alignItems", AlignItems);
        if (Background is { } backgroundValue)
        {
            writer.WritePropertyName("background");
            backgroundValue.WriteTo(writer);
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
    internal static FlexBox Read(JsonElement element)
    {
        var result = new FlexBox { Layout = string.Empty };
        var seenLayout = false;

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
                case "layout":
                    result.Layout = FlexReader.ReadString(property.Value);
                    seenLayout = true;
                    break;
                case "contents":
                    FlexReader.ReadArray(property.Value, result.Contents, FlexReader.ReadComponent);
                    break;
                case "flex":
                    result.Flex = FlexReader.ReadInt(property.Value);
                    break;
                case "spacing":
                    result.Spacing = FlexReader.ReadString(property.Value);
                    break;
                case "margin":
                    result.Margin = FlexReader.ReadString(property.Value);
                    break;
                case "position":
                    result.Position = FlexReader.ReadString(property.Value);
                    break;
                case "offsetTop":
                    result.OffsetTop = FlexReader.ReadString(property.Value);
                    break;
                case "offsetBottom":
                    result.OffsetBottom = FlexReader.ReadString(property.Value);
                    break;
                case "offsetStart":
                    result.OffsetStart = FlexReader.ReadString(property.Value);
                    break;
                case "offsetEnd":
                    result.OffsetEnd = FlexReader.ReadString(property.Value);
                    break;
                case "backgroundColor":
                    result.BackgroundColor = FlexReader.ReadString(property.Value);
                    break;
                case "borderColor":
                    result.BorderColor = FlexReader.ReadString(property.Value);
                    break;
                case "borderWidth":
                    result.BorderWidth = FlexReader.ReadString(property.Value);
                    break;
                case "cornerRadius":
                    result.CornerRadius = FlexReader.ReadString(property.Value);
                    break;
                case "width":
                    result.Width = FlexReader.ReadString(property.Value);
                    break;
                case "maxWidth":
                    result.MaxWidth = FlexReader.ReadString(property.Value);
                    break;
                case "height":
                    result.Height = FlexReader.ReadString(property.Value);
                    break;
                case "maxHeight":
                    result.MaxHeight = FlexReader.ReadString(property.Value);
                    break;
                case "paddingAll":
                    result.PaddingAll = FlexReader.ReadString(property.Value);
                    break;
                case "paddingTop":
                    result.PaddingTop = FlexReader.ReadString(property.Value);
                    break;
                case "paddingBottom":
                    result.PaddingBottom = FlexReader.ReadString(property.Value);
                    break;
                case "paddingStart":
                    result.PaddingStart = FlexReader.ReadString(property.Value);
                    break;
                case "paddingEnd":
                    result.PaddingEnd = FlexReader.ReadString(property.Value);
                    break;
                case "justifyContent":
                    result.JustifyContent = FlexReader.ReadString(property.Value);
                    break;
                case "alignItems":
                    result.AlignItems = FlexReader.ReadString(property.Value);
                    break;
                case "background":
                    result.Background = FlexLinearGradient.Read(property.Value);
                    break;
                case "action":
                    result.Action = FlexReader.ReadAction(property.Value);
                    break;
                default:
                    throw new FlexNotModelledException(property.Name);
            }
        }

        if (!seenLayout)
        {
            throw new FlexNotModelledException("layout");
        }

        return result;
    }
}
