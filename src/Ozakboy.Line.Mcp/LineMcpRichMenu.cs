using System.Text.Json;
using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Messaging.Actions;
using Ozakboy.Line.Messaging.RichMenu;

namespace Ozakboy.Line.Mcp;

/// <summary>
/// 把外部 AI 給的圖文選單 JSON 讀成 <see cref="LineRichMenu"/>。
/// Reads the rich menu JSON an outside AI supplied into a <see cref="LineRichMenu"/>.
/// </summary>
/// <remarks>
/// <para>
/// 逐欄位手動讀,不走 <see cref="JsonSerializer"/>。理由是 <see cref="LineRichMenu.Areas"/> 是一個
/// <b>沒有 setter 的集合屬性</b>:反序列化器預設不會去填它,結果是一個「名稱與尺寸都對、但一個區塊都沒有」
/// 的選單 —— 而那種選單 LINE 照收不誤,使用者看到的是一張完全按不動的圖。
/// Read field by field rather than through <see cref="JsonSerializer"/>. The reason is
/// <see cref="LineRichMenu.Areas"/>, a <b>collection property with no setter</b>: the deserialiser does not
/// populate it by default, and the result is a menu with the right name and size and no tappable areas at all —
/// which LINE accepts, and which the user sees as an image that does nothing.
/// </para>
/// <para>
/// 動作一律讀成 <see cref="RawAction"/>,不還原成具體型別:LINE 的動作型別會增加,
/// 而猜錯的代價是在讀取時把欄位丟掉,那正是這個套件最不能做的事 —— 選單是外部 AI 寫的,
/// 它寫了什麼就該原樣送出去。
/// Every action is read as a <see cref="RawAction"/> rather than reconstructed into a concrete type: LINE keeps
/// adding action types, and guessing wrong drops fields on the way in — the one thing this package must not do,
/// since the menu was written by an outside AI and should go out as written.
/// </para>
/// </remarks>
internal static class LineMcpRichMenu
{
    /// <summary>
    /// 讀取圖文選單定義。
    /// Reads a rich menu definition.
    /// </summary>
    /// <param name="menuJson">選單 JSON。The menu's JSON.</param>
    /// <returns>
    /// 讀得出來時為選單;否則為 <see cref="LineErrorCodes.InvalidJson"/> 失敗。
    /// The menu when it reads, otherwise a <see cref="LineErrorCodes.InvalidJson"/> failure.
    /// </returns>
    internal static Result<LineRichMenu> Parse(string menuJson)
    {
        if (string.IsNullOrWhiteSpace(menuJson))
        {
            return Error.Validation(LineErrorCodes.InvalidJson, "選單 JSON 不可為空白。The menu JSON cannot be blank.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(menuJson);
        }
        catch (JsonException exception)
        {
            var error = Error.Validation(LineErrorCodes.InvalidJson, "選單 JSON 無法解析。The menu JSON could not be parsed.");
            return Result.Failure<LineRichMenu>(error with { Exception = exception });
        }

        using (document)
        {
            return Parse(document.RootElement);
        }
    }

    /// <summary>
    /// 從已解析的元素讀取圖文選單定義。
    /// Reads a rich menu definition from an element that is already parsed.
    /// </summary>
    /// <param name="root">選單物件。The menu object.</param>
    /// <returns>
    /// 讀得出來時為選單;否則為 <see cref="LineErrorCodes.InvalidJson"/> 失敗。
    /// The menu when it reads, otherwise a <see cref="LineErrorCodes.InvalidJson"/> failure.
    /// </returns>
    internal static Result<LineRichMenu> Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return Error.Validation(LineErrorCodes.InvalidJson, "選單必須是一個 JSON 物件。A menu must be a JSON object.");
        }

        var menu = new LineRichMenu
        {
            Name = ReadString(root, "name"),
            ChatBarText = ReadString(root, "chatBarText"),
            Selected = root.TryGetProperty("selected", out var selected) && selected.ValueKind == JsonValueKind.True,
        };

        if (root.TryGetProperty("size", out var size) && size.ValueKind == JsonValueKind.Object)
        {
            menu.Size = new LineRichMenuSize
            {
                Width = ReadInt(size, "width", menu.Size.Width),
                Height = ReadInt(size, "height", menu.Size.Height),
            };
        }

        if (!root.TryGetProperty("areas", out var areas) || areas.ValueKind != JsonValueKind.Array)
        {
            return Error.Validation(
                LineErrorCodes.InvalidJson,
                "選單必須有 areas 陣列。沒有區塊的選單在使用者眼裡是一張按不動的圖。A menu must carry an areas array: a menu without areas is an image that does nothing.");
        }

        foreach (var area in areas.EnumerateArray())
        {
            if (area.ValueKind != JsonValueKind.Object
                || !area.TryGetProperty("action", out var action)
                || action.ValueKind != JsonValueKind.Object)
            {
                return Error.Validation(
                    LineErrorCodes.InvalidJson,
                    "每個區塊都必須有 action 物件。Every area must carry an action object.");
            }

            RawAction parsedAction;
            try
            {
                parsedAction = new RawAction(action);
            }
            catch (ArgumentException exception)
            {
                var error = Error.Validation(
                    LineErrorCodes.InvalidJson,
                    "區塊的 action 必須是含有字串 type 欄位的 JSON 物件。An area's action must be a JSON object carrying a string type field.");
                return Result.Failure<LineRichMenu>(error with { Exception = exception });
            }

            var bounds = new LineRichMenuBounds();
            if (area.TryGetProperty("bounds", out var boundsElement) && boundsElement.ValueKind == JsonValueKind.Object)
            {
                bounds.X = ReadInt(boundsElement, "x", 0);
                bounds.Y = ReadInt(boundsElement, "y", 0);
                bounds.Width = ReadInt(boundsElement, "width", 0);
                bounds.Height = ReadInt(boundsElement, "height", 0);
            }

            menu.Areas.Add(new LineRichMenuArea { Bounds = bounds, Action = parsedAction });
        }

        return Result.Success(menu);
    }

    /// <summary>
    /// 讀一個字串欄位,沒有時回空字串。
    /// Reads a string field, or an empty string when it is absent.
    /// </summary>
    /// <param name="element">物件。The object.</param>
    /// <param name="name">欄位名。The field name.</param>
    /// <returns>欄位值。The value.</returns>
    private static string ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    /// <summary>
    /// 讀一個整數欄位,沒有或不是數字時回預設值。
    /// Reads an integer field, falling back to a default when it is absent or not a number.
    /// </summary>
    /// <param name="element">物件。The object.</param>
    /// <param name="name">欄位名。The field name.</param>
    /// <param name="fallback">預設值。The fallback.</param>
    /// <returns>欄位值。The value.</returns>
    private static int ReadInt(JsonElement element, string name, int fallback) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : fallback;
}
