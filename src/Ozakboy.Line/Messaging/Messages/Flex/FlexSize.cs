using System.Globalization;

namespace Ozakboy.Line.Messaging.Messages.Flex;

/// <summary>
/// Flex 元件的尺寸:關鍵字、像素與百分比。
/// Flex component sizes: keywords, pixels and percentages.
/// </summary>
/// <remarks>
/// 尺寸在模型裡一律是 <c>string</c>,不用列舉:<c>3xl</c>、<c>20px</c>、<c>50%</c> 這類值放不進列舉,硬做就會有一半的合法值寫不出來。這個類別只提供關鍵字常數與量值工廠。
/// Sizes are plain <c>string</c>s in the model, never an enum: values such as <c>3xl</c>, <c>20px</c> and <c>50%</c> do not fit one, and forcing it would leave half of the legal values unwritable. This class only supplies keyword constants and value factories.
/// </remarks>
public static class FlexSize
{
    /// <summary>最小的尺寸。The smallest size.</summary>
    public const string Xxs = "xxs";

    /// <summary>特小。Extra small.</summary>
    public const string Xs = "xs";

    /// <summary>小。Small.</summary>
    public const string Sm = "sm";

    /// <summary>中。Medium.</summary>
    public const string Md = "md";

    /// <summary>大。Large.</summary>
    public const string Lg = "lg";

    /// <summary>特大。Extra large.</summary>
    public const string Xl = "xl";

    /// <summary>2 倍特大。Double extra large.</summary>
    public const string Xxl = "xxl";

    /// <summary>3 倍特大(值為 <c>3xl</c>,識別字不能以數字開頭)。Triple extra large (the value is <c>3xl</c>; an identifier cannot start with a digit).</summary>
    public const string ThreeXl = "3xl";

    /// <summary>4 倍特大(值為 <c>4xl</c>)。Quadruple extra large (the value is <c>4xl</c>).</summary>
    public const string FourXl = "4xl";

    /// <summary>5 倍特大(值為 <c>5xl</c>)。Quintuple extra large (the value is <c>5xl</c>).</summary>
    public const string FiveXl = "5xl";

    /// <summary>填滿可用寬度,只有圖片的 <c>size</c> 接受。Fills the available width; accepted only by an image's <c>size</c>.</summary>
    public const string Full = "full";

    /// <summary>
    /// 以像素表示的尺寸,例如 <c>Px(20)</c> 得到 <c>20px</c>。
    /// A size in pixels, such as <c>Px(20)</c> giving <c>20px</c>.
    /// </summary>
    /// <param name="pixels">像素數。The number of pixels.</param>
    /// <returns>像素量值字串。The pixel value string.</returns>
    public static string Px(int pixels) => string.Create(CultureInfo.InvariantCulture, $"{pixels}px");

    /// <summary>
    /// 以百分比表示的尺寸,例如 <c>Percent(50)</c> 得到 <c>50%</c>。
    /// A size as a percentage, such as <c>Percent(50)</c> giving <c>50%</c>.
    /// </summary>
    /// <param name="percent">百分比。The percentage.</param>
    /// <returns>百分比量值字串。The percentage value string.</returns>
    public static string Percent(int percent) => string.Create(CultureInfo.InvariantCulture, $"{percent}%");
}
