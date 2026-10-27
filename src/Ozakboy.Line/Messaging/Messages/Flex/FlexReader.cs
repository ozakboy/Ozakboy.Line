using System.Text.Json;
using Ozakboy.Line.Messaging.Actions;

namespace Ozakboy.Line.Messaging.Messages.Flex;

/// <summary>
/// 把 JSON 讀回強型別 Flex 模型:依 <c>type</c> 分派,認不得或無法完整建模的節點原樣保存為 Raw 型別。
/// Reads JSON back into the typed Flex model: dispatches on <c>type</c>, and keeps a node it does not recognise or cannot
/// model completely verbatim as a Raw type.
/// </summary>
/// <remarks>
/// <para>
/// 原則是「讀回再寫出不掉資料」:節點含有未建模的欄位,或欄位值的 JSON 型別不符,就整個節點退回
/// <see cref="RawFlexComponent"/> / <see cref="RawFlexContainer"/>,而不是丟掉那個欄位。代價是該節點(連同子樹)
/// 不再是強型別,但輸出與輸入一致 —— LINE 之後新增欄位時,舊版套件讀進來的版面不會悄悄少一塊。
/// The rule is that reading and writing back loses nothing: a node with an unmodelled field, or a value of the wrong JSON
/// kind, falls back as a whole to <see cref="RawFlexComponent"/> / <see cref="RawFlexContainer"/> instead of dropping the
/// field. The price is that the node (and its subtree) is no longer typed, but the output matches the input — so when
/// LINE adds a field later, a layout read by an older package does not silently lose a piece.
/// </para>
/// <para>
/// 結構性的錯誤(元件不是物件、沒有 <c>type</c>)沒有 Raw 可以承接,擲 <see cref="JsonException"/>。
/// Structural errors (a component that is not an object, a missing <c>type</c>) have no Raw to fall back on and throw
/// <see cref="JsonException"/>.
/// </para>
/// </remarks>
internal static class FlexReader
{
    /// <summary>
    /// 讀回一個容器(<c>bubble</c> / <c>carousel</c> / 其他)。
    /// Reads a container (<c>bubble</c>, <c>carousel</c> or anything else).
    /// </summary>
    /// <param name="element">JSON 元素。The JSON element.</param>
    /// <returns>強型別容器,或原樣保存的 <see cref="RawFlexContainer"/>。A typed container, or a verbatim <see cref="RawFlexContainer"/>.</returns>
    /// <exception cref="JsonException">不是帶 <c>type</c> 的 JSON 物件時擲出。Thrown when it is not a JSON object carrying <c>type</c>.</exception>
    internal static FlexContainer ReadContainer(JsonElement element)
    {
        var type = TypeOf(element, "容器 container");

        try
        {
            return type switch
            {
                "bubble" => FlexBubble.Read(element),
                "carousel" => FlexCarousel.Read(element),
                _ => new RawFlexContainer(element),
            };
        }
        catch (FlexNotModelledException)
        {
            return new RawFlexContainer(element);
        }
    }

    /// <summary>
    /// 讀回一個元件。
    /// Reads a component.
    /// </summary>
    /// <param name="element">JSON 元素。The JSON element.</param>
    /// <returns>強型別元件,或原樣保存的 <see cref="RawFlexComponent"/>。A typed component, or a verbatim <see cref="RawFlexComponent"/>.</returns>
    /// <exception cref="JsonException">不是帶 <c>type</c> 的 JSON 物件時擲出。Thrown when it is not a JSON object carrying <c>type</c>.</exception>
    internal static FlexComponent ReadComponent(JsonElement element)
    {
        var type = TypeOf(element, "元件 component");

        try
        {
            return type switch
            {
                "box" => FlexBox.Read(element),
                "text" => FlexText.Read(element),
                "image" => FlexImage.Read(element),
                "video" => FlexVideo.Read(element),
                "icon" => FlexIcon.Read(element),
                "button" => FlexButton.Read(element),
                "separator" => FlexSeparator.Read(element),
                "filler" => FlexFiller.Read(element),
                _ => new RawFlexComponent(element),
            };
        }
        catch (FlexNotModelledException)
        {
            return new RawFlexComponent(element);
        }
    }

    /// <summary>
    /// 讀回一個必須是方塊的節點(氣泡的 header / body / footer)。
    /// Reads a node that must be a box (a bubble's header, body or footer).
    /// </summary>
    /// <param name="element">JSON 元素。The JSON element.</param>
    /// <returns>方塊。The box.</returns>
    /// <exception cref="FlexNotModelledException">不是可完整建模的方塊時擲出,由上層退回 Raw。Thrown when it is not a box that can be modelled completely, so the caller falls back to Raw.</exception>
    internal static FlexBox ReadBox(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object || !IsType(element, "box"))
        {
            throw new FlexNotModelledException("box");
        }

        return FlexBox.Read(element);
    }

    /// <summary>
    /// 讀回一個必須是氣泡的節點(輪播的項目)。
    /// Reads a node that must be a bubble (a carousel item).
    /// </summary>
    /// <param name="element">JSON 元素。The JSON element.</param>
    /// <returns>氣泡。The bubble.</returns>
    /// <exception cref="FlexNotModelledException">不是可完整建模的氣泡時擲出。Thrown when it is not a bubble that can be modelled completely.</exception>
    internal static FlexBubble ReadBubble(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object || !IsType(element, "bubble"))
        {
            throw new FlexNotModelledException("bubble");
        }

        return FlexBubble.Read(element);
    }

    /// <summary>
    /// 讀回字串。
    /// Reads a string.
    /// </summary>
    /// <param name="element">JSON 元素。The JSON element.</param>
    /// <returns>字串。The string.</returns>
    /// <exception cref="FlexNotModelledException">不是字串時擲出。Thrown when it is not a string.</exception>
    internal static string ReadString(JsonElement element) =>
        element.ValueKind == JsonValueKind.String
            ? element.GetString()!
            : throw new FlexNotModelledException("string");

    /// <summary>
    /// 讀回 32 位元整數。
    /// Reads a 32-bit integer.
    /// </summary>
    /// <param name="element">JSON 元素。The JSON element.</param>
    /// <returns>整數。The integer.</returns>
    /// <exception cref="FlexNotModelledException">不是整數時擲出。Thrown when it is not an integer.</exception>
    internal static int ReadInt(JsonElement element) =>
        element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number)
            ? number
            : throw new FlexNotModelledException("integer");

    /// <summary>
    /// 讀回布林值。
    /// Reads a boolean.
    /// </summary>
    /// <param name="element">JSON 元素。The JSON element.</param>
    /// <returns>布林值。The boolean.</returns>
    /// <exception cref="FlexNotModelledException">不是布林時擲出。Thrown when it is not a boolean.</exception>
    internal static bool ReadBool(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new FlexNotModelledException("boolean"),
        };

    /// <summary>
    /// 讀回動作:沿用既有設計,一律成為 <see cref="RawAction"/>(保留全部內容)。
    /// Reads an action: as in the existing design, always a <see cref="RawAction"/> keeping the whole object.
    /// </summary>
    /// <param name="element">JSON 元素。The JSON element.</param>
    /// <returns>動作。The action.</returns>
    /// <exception cref="FlexNotModelledException">不是帶 <c>type</c> 的物件時擲出。Thrown when it is not an object carrying <c>type</c>.</exception>
    internal static LineAction ReadAction(JsonElement element)
    {
        try
        {
            return new RawAction(element);
        }
        catch (ArgumentException)
        {
            throw new FlexNotModelledException("action");
        }
    }

    /// <summary>
    /// 讀回 URI 動作(影片的 <c>action</c>)。
    /// Reads a URI action (a video's <c>action</c>).
    /// </summary>
    /// <param name="element">JSON 元素。The JSON element.</param>
    /// <returns>URI 動作。The URI action.</returns>
    /// <exception cref="FlexNotModelledException">不是只含 type / label / uri / altUri.desktop 的 URI 動作時擲出,由上層退回 Raw。Thrown when it is not a URI action made only of type, label, uri and altUri.desktop, so the caller falls back to Raw.</exception>
    internal static UriAction ReadUriAction(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new FlexNotModelledException("action");
        }

        string? uri = null;
        string? label = null;
        string? desktop = null;

        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "type" when property.Value.ValueKind == JsonValueKind.String && property.Value.GetString() == "uri":
                    break;
                case "label":
                    label = ReadString(property.Value);
                    break;
                case "uri":
                    uri = ReadString(property.Value);
                    break;
                case "altUri" when property.Value.ValueKind == JsonValueKind.Object:
                    foreach (var inner in property.Value.EnumerateObject())
                    {
                        desktop = inner.Name == "desktop" ? ReadString(inner.Value) : throw new FlexNotModelledException("altUri." + inner.Name);
                    }

                    break;
                default:
                    throw new FlexNotModelledException(property.Name);
            }
        }

        if (string.IsNullOrWhiteSpace(uri))
        {
            throw new FlexNotModelledException("uri");
        }

        return new UriAction(uri) { Label = label, AltUriDesktop = desktop };
    }

    /// <summary>
    /// 讀回一個 JSON 陣列,逐項讀入目標集合。
    /// Reads a JSON array into a target collection, item by item.
    /// </summary>
    /// <typeparam name="T">項目型別。The item type.</typeparam>
    /// <param name="element">JSON 元素。The JSON element.</param>
    /// <param name="target">目標集合。The target collection.</param>
    /// <param name="readItem">讀一個項目的方法。Reads one item.</param>
    /// <exception cref="FlexNotModelledException">不是陣列時擲出。Thrown when it is not an array.</exception>
    internal static void ReadArray<T>(JsonElement element, ICollection<T> target, Func<JsonElement, T> readItem)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new FlexNotModelledException("array");
        }

        foreach (var item in element.EnumerateArray())
        {
            target.Add(readItem(item));
        }
    }

    /// <summary>
    /// 取出節點的 <c>type</c> 字串。
    /// Takes the node's <c>type</c> string.
    /// </summary>
    /// <param name="element">JSON 元素。The JSON element.</param>
    /// <param name="what">節點種類,放進錯誤訊息。The kind of node, for the error message.</param>
    /// <returns><c>type</c> 的值。The value of <c>type</c>.</returns>
    /// <exception cref="JsonException">不是物件或沒有字串型別的 <c>type</c> 時擲出。Thrown when it is not an object or has no string <c>type</c>.</exception>
    private static string TypeOf(JsonElement element, string what)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"Flex {what} 必須是 JSON 物件。A Flex {what} must be a JSON object.");
        }

        if (!element.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
        {
            throw new JsonException($"Flex {what} 必須含有字串型別的 type 欄位。A Flex {what} must carry a string type field.");
        }

        return type.GetString()!;
    }

    /// <summary>
    /// 判斷物件的 <c>type</c> 是否為指定值。
    /// Tells whether the object's <c>type</c> is the given value.
    /// </summary>
    /// <param name="element">JSON 物件。The JSON object.</param>
    /// <param name="expected">預期的型別字串。The expected type string.</param>
    /// <returns>相符時為 <see langword="true"/>。<see langword="true"/> when it matches.</returns>
    private static bool IsType(JsonElement element, string expected) =>
        element.TryGetProperty("type", out var type)
        && type.ValueKind == JsonValueKind.String
        && type.ValueEquals(expected);
}
