namespace Ozakboy.Line.Messaging.Messages.Flex;

/// <summary>
/// 圖片填滿比例框的方式(<c>aspectMode</c>)。
/// How an image fills its aspect-ratio frame (<c>aspectMode</c>).
/// </summary>
public static class FlexAspectMode
{
    /// <summary>填滿框並裁掉超出的部分,LINE 的預設值。Fills the frame and crops the overflow, LINE's default.</summary>
    public const string Cover = "cover";

    /// <summary>整張圖放進框內,可能留白。Fits the whole image inside the frame, possibly leaving gaps.</summary>
    public const string Fit = "fit";
}
