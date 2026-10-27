using System.Globalization;

namespace Ozakboy.Line.Messaging.Messages.Flex;

/// <summary>
/// Flex 的顏色值:<c>#RRGGBB</c> 或帶透明度的 <c>#RRGGBBAA</c>。
/// A Flex colour value: <c>#RRGGBB</c>, or <c>#RRGGBBAA</c> with an alpha channel.
/// </summary>
/// <remarks>
/// 顏色屬性本身是 <c>string</c>,直接寫 <c>"#06C755"</c> 即可;這個類別只在想用 0~255 的分量組色時才需要。
/// Colour properties are plain <c>string</c>s, so writing <c>"#06C755"</c> directly is fine; this class is only for composing a colour from 0–255 components.
/// </remarks>
public static class FlexColor
{
    /// <summary>
    /// 由紅綠藍分量組出 <c>#RRGGBB</c>(大寫十六進位)。
    /// Builds <c>#RRGGBB</c> (upper-case hexadecimal) from red, green and blue components.
    /// </summary>
    /// <param name="red">紅,0~255。Red, 0–255.</param>
    /// <param name="green">綠,0~255。Green, 0–255.</param>
    /// <param name="blue">藍,0~255。Blue, 0–255.</param>
    /// <returns>顏色字串。The colour string.</returns>
    public static string Rgb(byte red, byte green, byte blue) =>
        string.Create(CultureInfo.InvariantCulture, $"#{red:X2}{green:X2}{blue:X2}");

    /// <summary>
    /// 由紅綠藍與透明度分量組出 <c>#RRGGBBAA</c>(大寫十六進位)。
    /// Builds <c>#RRGGBBAA</c> (upper-case hexadecimal) from red, green, blue and alpha components.
    /// </summary>
    /// <param name="red">紅,0~255。Red, 0–255.</param>
    /// <param name="green">綠,0~255。Green, 0–255.</param>
    /// <param name="blue">藍,0~255。Blue, 0–255.</param>
    /// <param name="alpha">透明度,0(全透明)~255(不透明)。Alpha, from 0 (transparent) to 255 (opaque).</param>
    /// <returns>顏色字串。The colour string.</returns>
    public static string Rgb(byte red, byte green, byte blue, byte alpha) =>
        string.Create(CultureInfo.InvariantCulture, $"#{red:X2}{green:X2}{blue:X2}{alpha:X2}");
}
