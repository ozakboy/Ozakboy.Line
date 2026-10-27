using System.Text.Json;
using Ozakboy.Line.Messaging.Messages.Flex;
using Ozakboy.Line.Tests.TestSupport;

namespace Ozakboy.Line.Tests.Flex;

/// <summary>
/// Flex 測試共用的小工具:把 JSON 正規化成緊湊字串、序列化容器、走訪節點樹。
/// Shared helpers for the Flex tests: normalise JSON into a compact string, serialise a container, walk the node tree.
/// </summary>
internal static class FlexTestSupport
{
    /// <summary>
    /// 把 JSON 文字重寫成緊湊且與 Utf8JsonWriter 預設跳脫一致的字串(欄位順序保留)。
    /// Rewrites JSON text into a compact string escaped the way Utf8JsonWriter does by default, keeping field order.
    /// </summary>
    /// <param name="json">JSON 文字。The JSON text.</param>
    /// <returns>正規化後的字串。The normalised string.</returns>
    internal static string Canonical(string json)
    {
        using var document = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(document.RootElement);
    }

    /// <summary>
    /// 讀取 <c>Samples/</c> 的樣本並正規化。
    /// Reads a sample under <c>Samples/</c> and normalises it.
    /// </summary>
    /// <param name="name">樣本檔名(不含副檔名)。The sample file name without extension.</param>
    /// <returns>正規化後的 body。The normalised body.</returns>
    internal static string Sample(string name) => Canonical(Samples.Body(name));

    /// <summary>
    /// 由 JSON 文字建立原樣元件。
    /// Creates a verbatim component from JSON text.
    /// </summary>
    /// <param name="json">JSON 文字。The JSON text.</param>
    /// <returns>原樣元件。The verbatim component.</returns>
    internal static RawFlexComponent Raw(string json)
    {
        using var document = JsonDocument.Parse(json);
        return new RawFlexComponent(document.RootElement);
    }

    /// <summary>
    /// 以基底型別序列化容器。
    /// Serialises a container with the base type.
    /// </summary>
    /// <param name="container">容器。The container.</param>
    /// <returns>JSON 字串。The JSON string.</returns>
    internal static string Write(FlexContainer container) => JsonSerializer.Serialize(container);

    /// <summary>
    /// 走訪容器下的所有元件(含巢狀,不含容器自己)。
    /// Walks every component under a container, nested ones included, the container itself excluded.
    /// </summary>
    /// <param name="container">容器。The container.</param>
    /// <returns>所有元件。Every component.</returns>
    internal static IEnumerable<FlexComponent> Components(FlexContainer container)
    {
        switch (container)
        {
            case FlexBubble bubble:
                foreach (var root in new FlexComponent?[] { bubble.Header, bubble.Hero, bubble.Body, bubble.Footer })
                {
                    if (root is null)
                    {
                        continue;
                    }

                    foreach (var component in Components(root))
                    {
                        yield return component;
                    }
                }

                break;
            case FlexCarousel carousel:
                foreach (var item in carousel.Contents)
                {
                    foreach (var component in Components(item))
                    {
                        yield return component;
                    }
                }

                break;
        }
    }

    /// <summary>
    /// 走訪元件自己與它底下的所有元件。
    /// Walks a component and everything under it.
    /// </summary>
    /// <param name="component">元件。The component.</param>
    /// <returns>元件自己與其子孫。The component and its descendants.</returns>
    internal static IEnumerable<FlexComponent> Components(FlexComponent component)
    {
        yield return component;

        switch (component)
        {
            case FlexBox box:
                foreach (var child in box.Contents)
                {
                    foreach (var inner in Components(child))
                    {
                        yield return inner;
                    }
                }

                break;
            case FlexVideo video:
                foreach (var inner in Components(video.AltContent))
                {
                    yield return inner;
                }

                break;
        }
    }
}
