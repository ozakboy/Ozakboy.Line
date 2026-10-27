using System.Globalization;

namespace Ozakboy.Line.Messaging.Messages.Flex;

/// <summary>
/// Flex 圖片、影片與圖示的長寬比(<c>aspectRatio</c>),格式為 <c>寬:高</c>。
/// The aspect ratio of a Flex image, video or icon, written as <c>width:height</c>.
/// </summary>
/// <remarks>
/// 長寬比是 <c>string</c>,因為 <c>1.91:1</c> 這類帶小數的值放不進整數對。
/// The ratio is a <c>string</c> because values with a fraction, such as <c>1.91:1</c>, do not fit a pair of integers.
/// </remarks>
public static class FlexAspectRatio
{
    /// <summary>1:1 正方形,LINE 的預設值。1:1 square, LINE's default.</summary>
    public const string Square = "1:1";

    /// <summary>20:13,官方範例最常用的橫幅比例。20:13, the banner ratio the official samples use most.</summary>
    public const string Banner = "20:13";

    /// <summary>
    /// 由整數的寬與高組出長寬比,例如 <c>Of(16, 9)</c> 得到 <c>16:9</c>。
    /// Builds a ratio from integer width and height, such as <c>Of(16, 9)</c> giving <c>16:9</c>.
    /// </summary>
    /// <param name="width">寬。The width.</param>
    /// <param name="height">高。The height.</param>
    /// <returns>長寬比字串。The ratio string.</returns>
    public static string Of(int width, int height) =>
        string.Create(CultureInfo.InvariantCulture, $"{width}:{height}");

    /// <summary>
    /// 由可帶小數的寬與高組出長寬比,例如 <c>Of(1.91, 1)</c> 得到 <c>1.91:1</c>。
    /// Builds a ratio from a width and height that may carry a fraction, such as <c>Of(1.91, 1)</c> giving <c>1.91:1</c>.
    /// </summary>
    /// <param name="width">寬。The width.</param>
    /// <param name="height">高。The height.</param>
    /// <returns>長寬比字串。The ratio string.</returns>
    public static string Of(double width, double height) =>
        string.Create(CultureInfo.InvariantCulture, $"{width}:{height}");
}
