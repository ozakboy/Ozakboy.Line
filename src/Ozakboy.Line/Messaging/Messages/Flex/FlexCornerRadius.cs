using System.Globalization;

namespace Ozakboy.Line.Messaging.Messages.Flex;

/// <summary>
/// 圓角半徑(<c>cornerRadius</c>)的關鍵字與像素工廠。
/// Keywords and the pixel factory for a box's corner radius.
/// </summary>
public static class FlexCornerRadius
{
    /// <summary>沒有圓角。No rounding.</summary>
    public const string None = "none";

    /// <summary>特小圓角。Extra small.</summary>
    public const string Xs = "xs";

    /// <summary>小圓角。Small.</summary>
    public const string Sm = "sm";

    /// <summary>中圓角。Medium.</summary>
    public const string Md = "md";

    /// <summary>大圓角。Large.</summary>
    public const string Lg = "lg";

    /// <summary>特大圓角。Extra large.</summary>
    public const string Xl = "xl";

    /// <summary>2 倍特大圓角。Double extra large.</summary>
    public const string Xxl = "xxl";

    /// <summary>
    /// 以像素表示的量值,例如 <c>Px(8)</c> 得到 <c>8px</c>。
    /// A value in pixels, such as <c>Px(8)</c> giving <c>8px</c>.
    /// </summary>
    /// <param name="pixels">像素數。The number of pixels.</param>
    /// <returns>像素量值字串。The pixel value string.</returns>
    public static string Px(int pixels) => string.Create(CultureInfo.InvariantCulture, $"{pixels}px");
}
