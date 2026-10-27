namespace Ozakboy.Line.Messaging.Messages.Flex;

/// <summary>
/// 方塊主軸方向的內容分佈(<c>justifyContent</c>)。
/// How a box distributes its content along the main axis (<c>justifyContent</c>).
/// </summary>
public static class FlexJustifyContent
{
    /// <summary>靠起點。Pack toward the start.</summary>
    public const string FlexStart = "flex-start";

    /// <summary>置中。Centred.</summary>
    public const string Center = "center";

    /// <summary>靠終點。Pack toward the end.</summary>
    public const string FlexEnd = "flex-end";

    /// <summary>兩端貼齊、中間平均分配。Flush at both ends, even gaps between.</summary>
    public const string SpaceBetween = "space-between";

    /// <summary>每個元件兩側的空白相等。Equal space around every component.</summary>
    public const string SpaceAround = "space-around";

    /// <summary>所有空白完全相等。All gaps exactly equal.</summary>
    public const string SpaceEvenly = "space-evenly";
}
