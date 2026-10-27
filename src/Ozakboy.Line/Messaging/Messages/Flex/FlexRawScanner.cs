using System.Globalization;

namespace Ozakboy.Line.Messaging.Messages.Flex;

/// <summary>
/// 找出版面裡以 Raw 型別保存的節點(讀回時本套件沒建模的部分),路徑格式與 <see cref="FlexValidator"/> 相同。
/// Finds the nodes of a layout kept as Raw types — the parts this package did not model when reading — using the same
/// path format as <see cref="FlexValidator"/>.
/// </summary>
internal static class FlexRawScanner
{
    /// <summary>
    /// 走訪容器,回傳所有 Raw 節點的路徑;容器自己是 Raw 時路徑為空字串。
    /// Walks a container and returns the path of every Raw node; the path is empty when the container itself is Raw.
    /// </summary>
    /// <param name="container">容器。The container.</param>
    /// <returns>路徑清單(依文件順序)。The paths, in document order.</returns>
    internal static List<string> Scan(FlexContainer container)
    {
        var found = new List<string>();
        Container(container, string.Empty, found);
        return found;
    }

    private static void Container(FlexContainer container, string path, List<string> found)
    {
        switch (container)
        {
            case RawFlexContainer:
                found.Add(path);
                break;
            case FlexBubble bubble:
                Bubble(bubble, path, found);
                break;
            case FlexCarousel carousel:
                for (var index = 0; index < carousel.Contents.Count; index++)
                {
                    Bubble(carousel.Contents[index], Index("contents", index), found);
                }

                break;
        }
    }

    private static void Bubble(FlexBubble bubble, string path, List<string> found)
    {
        Component(bubble.Header, Join(path, "header"), found);
        Component(bubble.Hero, Join(path, "hero"), found);
        Component(bubble.Body, Join(path, "body"), found);
        Component(bubble.Footer, Join(path, "footer"), found);
    }

    private static void Component(FlexComponent? component, string path, List<string> found)
    {
        switch (component)
        {
            case RawFlexComponent:
                found.Add(path);
                break;
            case FlexBox box:
                for (var index = 0; index < box.Contents.Count; index++)
                {
                    Component(box.Contents[index], Index(Join(path, "contents"), index), found);
                }

                break;
            case FlexVideo video:
                Component(video.AltContent, Join(path, "altContent"), found);
                break;
        }
    }

    private static string Join(string path, string name) => path.Length == 0 ? name : string.Concat(path, ".", name);

    private static string Index(string path, int index) => string.Create(CultureInfo.InvariantCulture, $"{path}[{index}]");
}
