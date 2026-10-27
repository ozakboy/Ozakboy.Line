namespace Ozakboy.Line;

/// <summary>
/// LINE Messaging API 的數量上限(Flex 訊息)。
/// The Messaging API's size limits — Flex messages.
/// </summary>
public static partial class LineMessagingLimits
{
    /// <summary>
    /// Flex 輪播最多幾個氣泡。
    /// The most bubbles a Flex carousel may hold.
    /// </summary>
    public const int MaxFlexCarouselBubbles = 12;

    /// <summary>
    /// Flex 輪播至少幾個氣泡。
    /// The fewest bubbles a Flex carousel may hold.
    /// </summary>
    public const int MinFlexCarouselBubbles = 1;

    /// <summary>
    /// 單一氣泡(以及其他非輪播容器)版面 JSON 的大小上限,以 UTF-8 位元組計(30 KB)。
    /// The size limit of the layout JSON of one bubble (and of any non-carousel container), in UTF-8 bytes (30 KB).
    /// </summary>
    public const int MaxFlexBubbleBytes = 30 * 1024;

    /// <summary>
    /// 輪播版面 JSON 的大小上限,以 UTF-8 位元組計(50 KB)。
    /// The size limit of a carousel's layout JSON, in UTF-8 bytes (50 KB).
    /// </summary>
    public const int MaxFlexCarouselBytes = 50 * 1024;
}
