using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Messages.Flex;

/// <summary>
/// 圖示(<c>icon</c>):只能放在 <c>baseline</c> 排列的方塊裡,與文字對齊基線。
/// An icon (<c>icon</c>): allowed only in a box laid out as <c>baseline</c>, where it aligns with the text baseline.
/// </summary>
[JsonConverter(typeof(FlexComponentJsonConverterFactory))]
public sealed class FlexIcon : FlexComponent
{
    /// <summary>
    /// 建立圖示元件。
    /// Creates an icon component.
    /// </summary>
    /// <param name="url">圖示位址(HTTPS)。The icon address (HTTPS).</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="url"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="url"/> is <see langword="null"/> or blank.
    /// </exception>
    public FlexIcon(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        Url = url;
    }

    /// <summary>
    /// 供讀回 JSON 時使用,位址稍後再填。
    /// For reading JSON back; the address is filled in afterwards.
    /// </summary>
    internal FlexIcon()
    {
        Url = string.Empty;
    }

    /// <inheritdoc />
    public override string Type => "icon";

    /// <summary>
    /// 圖示位址(HTTPS)。
    /// The icon address (HTTPS).
    /// </summary>
    public string Url { get; set; }

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
    /// 圖示大小:關鍵字見 <see cref="FlexSize"/>,或像素量值。
    /// The icon size: a keyword from <see cref="FlexSize"/>, or a pixel value.
    /// </summary>
    public string? Size { get; set; }

    /// <summary>
    /// 長寬比,格式為 <c>寬:高</c>(見 <see cref="FlexAspectRatio"/>)。
    /// The aspect ratio as <c>width:height</c> (see <see cref="FlexAspectRatio"/>).
    /// </summary>
    public string? AspectRatio { get; set; }

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
        writer.WriteString("url", Url);
        FlexJson.WriteString(writer, "margin", Margin);
        FlexJson.WriteString(writer, "position", Position);
        FlexJson.WriteString(writer, "offsetTop", OffsetTop);
        FlexJson.WriteString(writer, "offsetBottom", OffsetBottom);
        FlexJson.WriteString(writer, "offsetStart", OffsetStart);
        FlexJson.WriteString(writer, "offsetEnd", OffsetEnd);
        FlexJson.WriteString(writer, "size", Size);
        FlexJson.WriteString(writer, "aspectRatio", AspectRatio);
        FlexJson.WriteBool(writer, "scaling", Scaling);
        writer.WriteEndObject();
    }

    /// <summary>
    /// 從 JSON 物件讀回;含有本套件未建模的欄位,或欄位值的型別不符時擲出 <see cref="FlexNotModelledException"/>(呼叫端據此退回原樣保存)。
    /// Reads from a JSON object; throws <see cref="FlexNotModelledException"/> when a field this package does not model is present or a value has the wrong kind (the caller then keeps the node verbatim).
    /// </summary>
    /// <param name="element">JSON 物件。The JSON object.</param>
    /// <returns>讀回的物件。The object read.</returns>
    internal static FlexIcon Read(JsonElement element)
    {
        var result = new FlexIcon();
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
                case "aspectRatio":
                    result.AspectRatio = FlexReader.ReadString(property.Value);
                    break;
                case "scaling":
                    result.Scaling = FlexReader.ReadBool(property.Value);
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
