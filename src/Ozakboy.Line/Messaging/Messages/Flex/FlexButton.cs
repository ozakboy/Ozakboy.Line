using System.Text.Json;
using System.Text.Json.Serialization;
using Ozakboy.Line.Messaging.Actions;

namespace Ozakboy.Line.Messaging.Messages.Flex;

/// <summary>
/// 按鈕(<c>button</c>)。
/// A button (<c>button</c>).
/// </summary>
[JsonConverter(typeof(FlexComponentJsonConverterFactory))]
public sealed class FlexButton : FlexComponent
{
    /// <summary>
    /// 建立按鈕元件。
    /// Creates a button component.
    /// </summary>
    /// <param name="action">按下時觸發的動作。The action triggered on a tap.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="action"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="action"/> is <see langword="null"/>.
    /// </exception>
    public FlexButton(LineAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Action = action;
    }

    /// <summary>
    /// 供讀回 JSON 時使用,動作稍後再填。
    /// For reading JSON back; the action is filled in afterwards.
    /// </summary>
    internal FlexButton()
    {
        Action = null!;
    }

    /// <inheritdoc />
    public override string Type => "button";

    /// <summary>
    /// 按下時觸發的動作(必填)。
    /// The action triggered on a tap (required).
    /// </summary>
    public LineAction Action { get; set; }

    /// <summary>
    /// 在父方塊主軸方向上占的比例(整數);<c>0</c> 表示不伸縮、只占內容所需的空間。
    /// The share of the parent box's main axis this takes, as an integer; <c>0</c> means it does not stretch and takes only the space its content needs.
    /// </summary>
    public int? Flex { get; set; }

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
    /// 按鈕高度,見 <see cref="FlexButtonHeight"/>。
    /// The button height, see <see cref="FlexButtonHeight"/>.
    /// </summary>
    public string? Height { get; set; }

    /// <summary>
    /// 按鈕樣式,見 <see cref="FlexButtonStyle"/>。
    /// The button style, see <see cref="FlexButtonStyle"/>.
    /// </summary>
    public string? Style { get; set; }

    /// <summary>
    /// <c>link</c> 樣式時為文字色,其餘樣式為按鈕底色。
    /// The text colour for the <c>link</c> style and the button's background colour for the others.
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// 垂直對齊,見 <see cref="FlexGravity"/>。
    /// Vertical alignment, see <see cref="FlexGravity"/>.
    /// </summary>
    public string? Gravity { get; set; }

    /// <summary>
    /// 自動縮放模式,見 <see cref="FlexAdjustMode"/>。
    /// The auto-fit mode, see <see cref="FlexAdjustMode"/>.
    /// </summary>
    public string? AdjustMode { get; set; }

    /// <summary>
    /// 是否依使用者的字級設定縮放。
    /// Whether it scales with the user's font-size setting.
    /// </summary>
    public bool? Scaling { get; set; }

    /// <inheritdoc />
    internal override void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", Type);
        writer.WritePropertyName("action");
        Action.WriteTo(writer);
        FlexJson.WriteInt(writer, "flex", Flex);
        FlexJson.WriteString(writer, "margin", Margin);
        FlexJson.WriteString(writer, "position", Position);
        FlexJson.WriteString(writer, "offsetTop", OffsetTop);
        FlexJson.WriteString(writer, "offsetBottom", OffsetBottom);
        FlexJson.WriteString(writer, "offsetStart", OffsetStart);
        FlexJson.WriteString(writer, "offsetEnd", OffsetEnd);
        FlexJson.WriteString(writer, "height", Height);
        FlexJson.WriteString(writer, "style", Style);
        FlexJson.WriteString(writer, "color", Color);
        FlexJson.WriteString(writer, "gravity", Gravity);
        FlexJson.WriteString(writer, "adjustMode", AdjustMode);
        FlexJson.WriteBool(writer, "scaling", Scaling);
        writer.WriteEndObject();
    }

    /// <summary>
    /// 從 JSON 物件讀回;含有本套件未建模的欄位,或欄位值的型別不符時擲出 <see cref="FlexNotModelledException"/>(呼叫端據此退回原樣保存)。
    /// Reads from a JSON object; throws <see cref="FlexNotModelledException"/> when a field this package does not model is present or a value has the wrong kind (the caller then keeps the node verbatim).
    /// </summary>
    /// <param name="element">JSON 物件。The JSON object.</param>
    /// <returns>讀回的物件。The object read.</returns>
    internal static FlexButton Read(JsonElement element)
    {
        var result = new FlexButton();
        var seenAction = false;

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
                case "action":
                    result.Action = FlexReader.ReadAction(property.Value);
                    seenAction = true;
                    break;
                case "flex":
                    result.Flex = FlexReader.ReadInt(property.Value);
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
                case "height":
                    result.Height = FlexReader.ReadString(property.Value);
                    break;
                case "style":
                    result.Style = FlexReader.ReadString(property.Value);
                    break;
                case "color":
                    result.Color = FlexReader.ReadString(property.Value);
                    break;
                case "gravity":
                    result.Gravity = FlexReader.ReadString(property.Value);
                    break;
                case "adjustMode":
                    result.AdjustMode = FlexReader.ReadString(property.Value);
                    break;
                case "scaling":
                    result.Scaling = FlexReader.ReadBool(property.Value);
                    break;
                default:
                    throw new FlexNotModelledException(property.Name);
            }
        }

        if (!seenAction)
        {
            throw new FlexNotModelledException("action");
        }

        return result;
    }
}
