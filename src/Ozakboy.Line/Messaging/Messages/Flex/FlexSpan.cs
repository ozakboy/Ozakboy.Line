using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Messages.Flex;

/// <summary>
/// 文字片段(<c>span</c>):<see cref="FlexText"/> 的 <c>contents</c> 裡一段可各自設定樣式的文字。
/// A text fragment (<c>span</c>): one run inside a <see cref="FlexText"/>'s <c>contents</c> that carries its own styling.
/// </summary>
[JsonConverter(typeof(FlexSpanJsonConverter))]
public sealed class FlexSpan
{
    /// <summary>
    /// 建立文字片段。
    /// Creates a text fragment.
    /// </summary>
    /// <param name="text">片段文字,不可為空。The fragment text, which must not be empty.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="text"/> 為 <see langword="null"/> 或空字串時擲出。
    /// Thrown when <paramref name="text"/> is <see langword="null"/> or empty.
    /// </exception>
    public FlexSpan(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        Text = text;
    }

    /// <summary>
    /// 供讀回 JSON 時使用,文字稍後再填。
    /// For reading JSON back; the text is filled in afterwards.
    /// </summary>
    internal FlexSpan()
    {
        Text = string.Empty;
    }

    /// <summary>
    /// 片段文字。
    /// The fragment text.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// 顏色,<c>#RRGGBB</c> 或 <c>#RRGGBBAA</c>(見 <see cref="FlexColor"/>)。
    /// The colour as <c>#RRGGBB</c> or <c>#RRGGBBAA</c> (see <see cref="FlexColor"/>).
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// 字級:關鍵字見 <see cref="FlexSize"/>,或像素量值。
    /// The font size: a keyword from <see cref="FlexSize"/>, or a pixel value.
    /// </summary>
    public string? Size { get; set; }

    /// <summary>
    /// 粗細,見 <see cref="FlexWeight"/>。
    /// The weight, see <see cref="FlexWeight"/>.
    /// </summary>
    public string? Weight { get; set; }

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
    /// 把整個物件寫成 JSON;null 的欄位不輸出。
    /// Writes the whole object as JSON; null fields are not written.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    internal void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "span");
        writer.WriteString("text", Text);
        FlexJson.WriteString(writer, "color", Color);
        FlexJson.WriteString(writer, "size", Size);
        FlexJson.WriteString(writer, "weight", Weight);
        FlexJson.WriteString(writer, "style", Style);
        FlexJson.WriteString(writer, "decoration", Decoration);
        writer.WriteEndObject();
    }

    /// <summary>
    /// 從 JSON 物件讀回;含有本套件未建模的欄位,或欄位值的型別不符時擲出 <see cref="FlexNotModelledException"/>(呼叫端據此退回原樣保存)。
    /// Reads from a JSON object; throws <see cref="FlexNotModelledException"/> when a field this package does not model is present or a value has the wrong kind (the caller then keeps the node verbatim).
    /// </summary>
    /// <param name="element">JSON 物件。The JSON object.</param>
    /// <returns>讀回的物件。The object read.</returns>
    internal static FlexSpan Read(JsonElement element)
    {
        var result = new FlexSpan();
        var seenText = false;

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
                    seenText = true;
                    break;
                case "color":
                    result.Color = FlexReader.ReadString(property.Value);
                    break;
                case "size":
                    result.Size = FlexReader.ReadString(property.Value);
                    break;
                case "weight":
                    result.Weight = FlexReader.ReadString(property.Value);
                    break;
                case "style":
                    result.Style = FlexReader.ReadString(property.Value);
                    break;
                case "decoration":
                    result.Decoration = FlexReader.ReadString(property.Value);
                    break;
                default:
                    throw new FlexNotModelledException(property.Name);
            }
        }

        if (!seenText)
        {
            throw new FlexNotModelledException("text");
        }

        return result;
    }
}
