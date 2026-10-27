using System.Globalization;

namespace Ozakboy.Line.Messaging.Messages.Flex;

/// <summary>
/// 間距、內距與位移(<c>spacing</c>、<c>margin</c>、<c>padding*</c>、<c>offset*</c>)的關鍵字與像素工廠。
/// Keywords and the pixel factory for spacing, margin, padding and offset values.
/// </summary>
public static class FlexSpacing
{
    /// <summary>沒有間距。No spacing.</summary>
    public const string None = "none";

    /// <summary>特小間距。Extra small spacing.</summary>
    public const string Xs = "xs";

    /// <summary>小間距。Small spacing.</summary>
    public const string Sm = "sm";

    /// <summary>中間距。Medium spacing.</summary>
    public const string Md = "md";

    /// <summary>大間距。Large spacing.</summary>
    public const string Lg = "lg";

    /// <summary>特大間距。Extra large spacing.</summary>
    public const string Xl = "xl";

    /// <summary>2 倍特大間距。Double extra large spacing.</summary>
    public const string Xxl = "xxl";

    /// <summary>
    /// 以像素表示的量值,例如 <c>Px(8)</c> 得到 <c>8px</c>。
    /// A value in pixels, such as <c>Px(8)</c> giving <c>8px</c>.
    /// </summary>
    /// <param name="pixels">像素數。The number of pixels.</param>
    /// <returns>像素量值字串。The pixel value string.</returns>
    public static string Px(int pixels) => string.Create(CultureInfo.InvariantCulture, $"{pixels}px");
}
