using System.Globalization;

namespace Ozakboy.Line.Messaging.Messages.Flex;

/// <summary>
/// 框線粗細(<c>borderWidth</c>)的關鍵字與像素工廠。
/// Keywords and the pixel factory for a box's border width.
/// </summary>
public static class FlexBorderWidth
{
    /// <summary>沒有邊框。No border.</summary>
    public const string None = "none";

    /// <summary>細。Light.</summary>
    public const string Light = "light";

    /// <summary>一般。Normal.</summary>
    public const string Normal = "normal";

    /// <summary>中。Medium.</summary>
    public const string Medium = "medium";

    /// <summary>半粗(值為 <c>semi-bold</c>)。Semi-bold (the value is <c>semi-bold</c>).</summary>
    public const string SemiBold = "semi-bold";

    /// <summary>粗。Bold.</summary>
    public const string Bold = "bold";

    /// <summary>
    /// 以像素表示的量值,例如 <c>Px(8)</c> 得到 <c>8px</c>。
    /// A value in pixels, such as <c>Px(8)</c> giving <c>8px</c>.
    /// </summary>
    /// <param name="pixels">像素數。The number of pixels.</param>
    /// <returns>像素量值字串。The pixel value string.</returns>
    public static string Px(int pixels) => string.Create(CultureInfo.InvariantCulture, $"{pixels}px");
}
