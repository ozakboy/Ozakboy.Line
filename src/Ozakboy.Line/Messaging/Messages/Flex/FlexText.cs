using System.Text.Json;
using System.Text.Json.Serialization;
using Ozakboy.Line.Messaging.Actions;

namespace Ozakboy.Line.Messaging.Messages.Flex;

/// <summary>
/// 文字(<c>text</c>):一段文字,或由多個 <see cref="FlexSpan"/> 組成的圖文混排文字。
/// Text (<c>text</c>): a run of text, or mixed-style text made of several <see cref="FlexSpan"/>s.
/// </summary>
/// <remarks>
/// <see cref="Text"/> 與 <see cref="Contents"/> 二選一;兩者都設時由 LINE 決定如何處理,本類別照寫不改。
/// <see cref="Text"/> and <see cref="Contents"/> are alternatives; if both are set LINE decides what to do and this class writes both unchanged.
/// </remarks>
[JsonConverter(typeof(FlexComponentJsonConverterFactory))]
public sealed class FlexText : FlexComponent
{
    /// <summary>
    /// 建立沒有文字的文字元件;之後設定 <see cref="Text"/> 或 <see cref="Contents"/>。
    /// Creates a text component without text; set <see cref="Text"/> or <see cref="Contents"/> afterwards.
    /// </summary>
    public FlexText()
    {
    }

    /// <summary>
    /// 建立帶文字的文字元件。
    /// Creates a text component holding text.
    /// </summary>
    /// <param name="text">文字內容,不可為空。The text, which must not be empty.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="text"/> 為 <see langword="null"/> 或空字串時擲出。
    /// Thrown when <paramref name="text"/> is <see langword="null"/> or empty.
    /// </exception>
    public FlexText(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        Text = text;
    }

    /// <inheritdoc />
    public override string Type => "text";

    /// <summary>
    /// 文字內容;與 <see cref="Contents"/> 二選一。
    /// The text; an alternative to <see cref="Contents"/>.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// 圖文混排的片段(<c>span</c>);與 <see cref="Text"/> 二選一。
    /// The mixed-style fragments (<c>span</c>); an alternative to <see cref="Text"/>.
    /// </summary>
    public IList<FlexSpan> Contents { get; init; } = [];

    /// <summary>
    /// 自動縮放模式,見 <see cref="FlexAdjustMode"/>。
    /// The auto-fit mode, see <see cref="FlexAdjustMode"/>.
    /// </summary>
    public string? AdjustMode { get; set; }

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
    /// 字級:關鍵字見 <see cref="FlexSize"/>,或像素量值。
    /// The font size: a keyword from <see cref="FlexSize"/>, or a pixel value.
    /// </summary>
    public string? Size { get; set; }

    /// <summary>
    /// 水平對齊,見 <see cref="FlexAlign"/>。
    /// Horizontal alignment, see <see cref="FlexAlign"/>.
    /// </summary>
    public string? Align { get; set; }

    /// <summary>
    /// 垂直對齊,見 <see cref="FlexGravity"/>。
    /// Vertical alignment, see <see cref="FlexGravity"/>.
    /// </summary>
    public string? Gravity { get; set; }

    /// <summary>
    /// 是否自動換行。
    /// Whether the text wraps.
    /// </summary>
    public bool? Wrap { get; set; }

    /// <summary>
    /// 行距,像素量值(如 <c>20px</c>)。
    /// The line spacing as a pixel value such as <c>20px</c>.
    /// </summary>
    public string? LineSpacing { get; set; }

    /// <summary>
    /// 最多顯示幾行(整數);<c>0</c> 表示不限。
    /// The maximum number of lines shown, as an integer; <c>0</c> means unlimited.
    /// </summary>
    public int? MaxLines { get; set; }

    /// <summary>
    /// 粗細,見 <see cref="FlexWeight"/>。
    /// The weight, see <see cref="FlexWeight"/>.
    /// </summary>
    public string? Weight { get; set; }

    /// <summary>
    /// 顏色,<c>#RRGGBB</c> 或 <c>#RRGGBBAA</c>(見 <see cref="FlexColor"/>)。
    /// The colour as <c>#RRGGBB</c> or <c>#RRGGBBAA</c> (see <see cref="FlexColor"/>).
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// 點擊時觸發的動作;沿用 <see cref="LineAction"/>(從 JSON 讀回時為 <see cref="RawAction"/>)。
    /// The action triggered on a tap; reuses <see cref="LineAction"/> (a <see cref="RawAction"/> when read back from JSON).
    /// </summary>
    public LineAction? Action { get; set; }

    /// <summary>
    /// 字型樣式,見 <see cref="FlexTextStyle"/>。
    /// The font style, see <see cref="FlexTextStyle"/>.
    /// </summary>
    public string? Style { get; set; }

    /// <summary>
    /// 裝飾線,見 <see cref="FlexDecoration"/>。
    /// The decoration line, see <see cref="FlexDecoration"/>.
    /// </summary>
    public string? Decoration { get; set; }

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
        FlexJson.WriteString(writer, "text", Text);
        if (Contents.Count > 0)
        {
            writer.WriteStartArray("contents");
            foreach (var item in Contents)
            {
                item.WriteTo(writer);
            }

            writer.WriteEndArray();
        }
        FlexJson.WriteString(writer, "adjustMode", AdjustMode);
        FlexJson.WriteInt(writer, "flex", Flex);
        FlexJson.WriteString(writer, "margin", Margin);
        FlexJson.WriteString(writer, "position", Position);
        FlexJson.WriteString(writer, "offsetTop", OffsetTop);
        FlexJson.WriteString(writer, "offsetBottom", OffsetBottom);
        FlexJson.WriteString(writer, "offsetStart", OffsetStart);
        FlexJson.WriteString(writer, "offsetEnd", OffsetEnd);
        FlexJson.WriteString(writer, "size", Size);
        FlexJson.WriteString(writer, "align", Align);
        FlexJson.WriteString(writer, "gravity", Gravity);
        FlexJson.WriteBool(writer, "wrap", Wrap);
        FlexJson.WriteString(writer, "lineSpacing", LineSpacing);
        FlexJson.WriteInt(writer, "maxLines", MaxLines);
        FlexJson.WriteString(writer, "weight", Weight);
        FlexJson.WriteString(writer, "color", Color);
        if (Action is { } actionValue)
        {
            writer.WritePropertyName("action");
            actionValue.WriteTo(writer);
        }
        FlexJson.WriteString(writer, "style", Style);
        FlexJson.WriteString(writer, "decoration", Decoration);
        FlexJson.WriteBool(writer, "scaling", Scaling);
        writer.WriteEndObject();
    }

    /// <summary>
    /// 從 JSON 物件讀回;含有本套件未建模的欄位,或欄位值的型別不符時擲出 <see cref="FlexNotModelledException"/>(呼叫端據此退回原樣保存)。
    /// Reads from a JSON object; throws <see cref="FlexNotModelledException"/> when a field this package does not model is present or a value has the wrong kind (the caller then keeps the node verbatim).
    /// </summary>
    /// <param name="element">JSON 物件。The JSON object.</param>
    /// <returns>讀回的物件。The object read.</returns>
    internal static FlexText Read(JsonElement element)
    {
        var result = new FlexText();

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
                case "text":
                    result.Text = FlexReader.ReadString(property.Value);
                    break;
                case "contents":
                    FlexReader.ReadArray(property.Value, result.Contents, FlexSpan.Read);
                    break;
                case "adjustMode":
                    result.AdjustMode = FlexReader.ReadString(property.Value);
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
                case "size":
                    result.Size = FlexReader.ReadString(property.Value);
                    break;
                case "align":
                    result.Align = FlexReader.ReadString(property.Value);
                    break;
                case "gravity":
                    result.Gravity = FlexReader.ReadString(property.Value);
                    break;
                case "wrap":
                    result.Wrap = FlexReader.ReadBool(property.Value);
                    break;
                case "lineSpacing":
                    result.LineSpacing = FlexReader.ReadString(property.Value);
                    break;
                case "maxLines":
                    result.MaxLines = FlexReader.ReadInt(property.Value);
                    break;
                case "weight":
                    result.Weight = FlexReader.ReadString(property.Value);
                    break;
                case "color":
                    result.Color = FlexReader.ReadString(property.Value);
                    break;
                case "action":
                    result.Action = FlexReader.ReadAction(property.Value);
                    break;
                case "style":
                    result.Style = FlexReader.ReadString(property.Value);
                    break;
                case "decoration":
                    result.Decoration = FlexReader.ReadString(property.Value);
                    break;
                case "scaling":
                    result.Scaling = FlexReader.ReadBool(property.Value);
                    break;
                default:
                    throw new FlexNotModelledException(property.Name);
            }
        }

        return result;
    }
}
