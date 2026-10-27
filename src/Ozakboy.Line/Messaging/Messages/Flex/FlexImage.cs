using System.Text.Json;
using System.Text.Json.Serialization;
using Ozakboy.Line.Messaging.Actions;

namespace Ozakboy.Line.Messaging.Messages.Flex;

/// <summary>
/// 圖片(<c>image</c>)。
/// An image (<c>image</c>).
/// </summary>
[JsonConverter(typeof(FlexComponentJsonConverterFactory))]
public sealed class FlexImage : FlexComponent
{
    /// <summary>
    /// 建立圖片元件。
    /// Creates an image component.
    /// </summary>
    /// <param name="url">圖片位址(HTTPS)。The image address (HTTPS).</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="url"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="url"/> is <see langword="null"/> or blank.
    /// </exception>
    public FlexImage(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        Url = url;
    }

    /// <summary>
    /// 供讀回 JSON 時使用,位址稍後再填。
    /// For reading JSON back; the address is filled in afterwards.
    /// </summary>
    internal FlexImage()
    {
        Url = string.Empty;
    }

    /// <inheritdoc />
    public override string Type => "image";

    /// <summary>
    /// 圖片位址(HTTPS)。
    /// The image address (HTTPS).
    /// </summary>
    public string Url { get; set; }

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
    /// 寬度:關鍵字見 <see cref="FlexSize"/>(含 <c>full</c>),或像素 / 百分比量值。
    /// The width: a keyword from <see cref="FlexSize"/> (including <c>full</c>), or a pixel or percentage value.
    /// </summary>
    public string? Size { get; set; }

    /// <summary>
    /// 長寬比,格式為 <c>寬:高</c>(見 <see cref="FlexAspectRatio"/>)。
    /// The aspect ratio as <c>width:height</c> (see <see cref="FlexAspectRatio"/>).
    /// </summary>
    public string? AspectRatio { get; set; }

    /// <summary>
    /// 圖片填滿比例框的方式,見 <see cref="FlexAspectMode"/>。
    /// How the image fills its aspect-ratio frame, see <see cref="FlexAspectMode"/>.
    /// </summary>
    public string? AspectMode { get; set; }

    /// <summary>
    /// 背景色,<c>#RRGGBB</c> 或 <c>#RRGGBBAA</c>。
    /// The background colour as <c>#RRGGBB</c> or <c>#RRGGBBAA</c>.
    /// </summary>
    public string? BackgroundColor { get; set; }

    /// <summary>
    /// 點擊時觸發的動作;沿用 <see cref="LineAction"/>(從 JSON 讀回時為 <see cref="RawAction"/>)。
    /// The action triggered on a tap; reuses <see cref="LineAction"/> (a <see cref="RawAction"/> when read back from JSON).
    /// </summary>
    public LineAction? Action { get; set; }

    /// <summary>
    /// 是否播放動態圖片(APNG);預設不播放。
    /// Whether an animated image (APNG) plays; it does not by default.
    /// </summary>
    public bool? Animated { get; set; }

    /// <inheritdoc />
    internal override void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", Type);
        writer.WriteString("url", Url);
        FlexJson.WriteInt(writer, "flex", Flex);
        FlexJson.WriteString(writer, "margin", Margin);
        FlexJson.WriteString(writer, "position", Position);
        FlexJson.WriteString(writer, "offsetTop", OffsetTop);
        FlexJson.WriteString(writer, "offsetBottom", OffsetBottom);
        FlexJson.WriteString(writer, "offsetStart", OffsetStart);
        FlexJson.WriteString(writer, "offsetEnd", OffsetEnd);
        FlexJson.WriteString(writer, "align", Align);
        FlexJson.WriteString(writer, "gravity", Gravity);
        FlexJson.WriteString(writer, "size", Size);
        FlexJson.WriteString(writer, "aspectRatio", AspectRatio);
        FlexJson.WriteString(writer, "aspectMode", AspectMode);
        FlexJson.WriteString(writer, "backgroundColor", BackgroundColor);
        if (Action is { } actionValue)
        {
            writer.WritePropertyName("action");
            actionValue.WriteTo(writer);
        }
        FlexJson.WriteBool(writer, "animated", Animated);
        writer.WriteEndObject();
    }

    /// <summary>
    /// 從 JSON 物件讀回;含有本套件未建模的欄位,或欄位值的型別不符時擲出 <see cref="FlexNotModelledException"/>(呼叫端據此退回原樣保存)。
    /// Reads from a JSON object; throws <see cref="FlexNotModelledException"/> when a field this package does not model is present or a value has the wrong kind (the caller then keeps the node verbatim).
    /// </summary>
    /// <param name="element">JSON 物件。The JSON object.</param>
    /// <returns>讀回的物件。The object read.</returns>
    internal static FlexImage Read(JsonElement element)
    {
        var result = new FlexImage();
        var seenUrl = false;

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
                case "url":
                    result.Url = FlexReader.ReadString(property.Value);
                    seenUrl = true;
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
                case "align":
                    result.Align = FlexReader.ReadString(property.Value);
                    break;
                case "gravity":
                    result.Gravity = FlexReader.ReadString(property.Value);
                    break;
                case "size":
                    result.Size = FlexReader.ReadString(property.Value);
                    break;
                case "aspectRatio":
                    result.AspectRatio = FlexReader.ReadString(property.Value);
                    break;
                case "aspectMode":
                    result.AspectMode = FlexReader.ReadString(property.Value);
                    break;
                case "backgroundColor":
                    result.BackgroundColor = FlexReader.ReadString(property.Value);
                    break;
                case "action":
                    result.Action = FlexReader.ReadAction(property.Value);
                    break;
                case "animated":
                    result.Animated = FlexReader.ReadBool(property.Value);
                    break;
                default:
                    throw new FlexNotModelledException(property.Name);
            }
        }

        if (!seenUrl)
        {
            throw new FlexNotModelledException("url");
        }

        return result;
    }
}
