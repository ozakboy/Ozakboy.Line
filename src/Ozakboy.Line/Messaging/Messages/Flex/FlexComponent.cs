using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Messages.Flex;

/// <summary>
/// Flex 版面裡的一個元件:方塊、文字、圖片、影片、圖示、按鈕、分隔線或填充。
/// One component of a Flex layout: a box, text, image, video, icon, button, separator or filler.
/// </summary>
/// <remarks>
/// <para>
/// 與 <see cref="LineMessage"/> 同理,只在本組件內可以繼承;還沒建模的元件(或 LINE 之後新增的)走
/// <see cref="RawFlexComponent"/>。已廢止的 <c>spacer</c> 刻意不建模,讀回時也會成為 <see cref="RawFlexComponent"/>。
/// As with <see cref="LineMessage"/>, derivation is limited to this assembly; a component not modelled yet (or added by
/// LINE later) goes through <see cref="RawFlexComponent"/>. The deprecated <c>spacer</c> is deliberately not modelled
/// and also comes back as a <see cref="RawFlexComponent"/>.
/// </para>
/// <para>
/// 數值型的欄位(尺寸、間距、顏色、長寬比等)一律是 <c>string</c>,關鍵字與量值工廠在 <see cref="FlexSize"/>、
/// <see cref="FlexSpacing"/>、<see cref="FlexColor"/>、<see cref="FlexAspectRatio"/> 等靜態類別。
/// Valued fields (sizes, spacings, colours, aspect ratios and so on) are all plain <c>string</c>s, with keywords and value
/// factories in the static classes <see cref="FlexSize"/>, <see cref="FlexSpacing"/>, <see cref="FlexColor"/>,
/// <see cref="FlexAspectRatio"/> and friends.
/// </para>
/// </remarks>
[JsonConverter(typeof(FlexComponentJsonConverter))]
public abstract class FlexComponent
{
    /// <summary>
    /// 只允許本組件內繼承。
    /// Derivation is limited to this assembly.
    /// </summary>
    private protected FlexComponent()
    {
    }

    /// <summary>
    /// LINE 規格裡的元件型別字串,例如 <c>box</c>、<c>text</c>。
    /// The component type string from LINE's specification, such as <c>box</c> or <c>text</c>.
    /// </summary>
    public abstract string Type { get; }

    /// <summary>
    /// 把整個元件寫成一個 JSON 物件。
    /// Writes the whole component as one JSON object.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    internal abstract void WriteTo(Utf8JsonWriter writer);
}
