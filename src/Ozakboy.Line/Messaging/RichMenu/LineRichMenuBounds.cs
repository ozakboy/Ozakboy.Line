using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.RichMenu;

/// <summary>
/// 圖文選單上一個可點擊區塊的範圍,單位為圖片上的像素。
/// The bounds of one tappable area on a rich menu, in pixels on the image.
/// </summary>
public sealed class LineRichMenuBounds
{
    /// <summary>
    /// 左上角的水平座標。
    /// The horizontal coordinate of the top-left corner.
    /// </summary>
    [JsonPropertyName("x")]
    public int X { get; set; }

    /// <summary>
    /// 左上角的垂直座標。
    /// The vertical coordinate of the top-left corner.
    /// </summary>
    [JsonPropertyName("y")]
    public int Y { get; set; }

    /// <summary>
    /// 區塊寬度。
    /// The area's width.
    /// </summary>
    [JsonPropertyName("width")]
    public int Width { get; set; }

    /// <summary>
    /// 區塊高度。
    /// The area's height.
    /// </summary>
    [JsonPropertyName("height")]
    public int Height { get; set; }
}
