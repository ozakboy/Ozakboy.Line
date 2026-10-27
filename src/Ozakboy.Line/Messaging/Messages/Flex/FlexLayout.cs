namespace Ozakboy.Line.Messaging.Messages.Flex;

/// <summary>
/// 方塊內元件的排列方向(<c>layout</c>)。
/// The direction components are laid out in inside a box (<c>layout</c>).
/// </summary>
public static class FlexLayout
{
    /// <summary>水平排列。Left to right.</summary>
    public const string Horizontal = "horizontal";

    /// <summary>垂直排列。Top to bottom.</summary>
    public const string Vertical = "vertical";

    /// <summary>水平排列並對齊文字基線;只適合放文字與圖示。Horizontal, aligned on the text baseline; suited to text and icons only.</summary>
    public const string Baseline = "baseline";
}
