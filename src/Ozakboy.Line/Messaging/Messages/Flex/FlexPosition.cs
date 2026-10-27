namespace Ozakboy.Line.Messaging.Messages.Flex;

/// <summary>
/// 元件的定位方式(<c>position</c>)。
/// How a component is positioned (<c>position</c>).
/// </summary>
public static class FlexPosition
{
    /// <summary>依文件流排列,LINE 的預設值。Laid out in flow, LINE's default.</summary>
    public const string Relative = "relative";

    /// <summary>以父方塊為基準的絕對定位,搭配 <c>offset*</c> 使用。Positioned against the parent box, used together with <c>offset*</c>.</summary>
    public const string Absolute = "absolute";
}
