using System.Text.Json.Serialization;
using Ozakboy.Line.Messaging.Actions;

namespace Ozakboy.Line.Messaging.RichMenu;

/// <summary>
/// 圖文選單上的一個可點擊區塊。
/// One tappable area on a rich menu.
/// </summary>
/// <remarks>
/// 區塊<b>不會</b>畫在圖上 —— 圖片長什麼樣是圖片的事,哪裡可以點是這裡的事。兩者對不上時沒有任何錯誤,
/// 使用者看到的是一個按了沒反應的按鈕,或是點空白處卻觸發了動作。
/// Areas are <b>not</b> drawn on the image: what the menu looks like is the image's business, and where it can be
/// tapped is this. A disagreement between the two raises no error — the user finds a button that does nothing, or
/// blank space that fires an action.
/// </remarks>
public sealed class LineRichMenuArea
{
    /// <summary>
    /// 區塊範圍。
    /// The area's bounds.
    /// </summary>
    [JsonPropertyName("bounds")]
    public LineRichMenuBounds Bounds { get; set; } = new();

    /// <summary>
    /// 點下去要做什麼。
    /// What happens when it is tapped.
    /// </summary>
    /// <remarks>
    /// 這個屬性是必填的。沒有動作的區塊在 LINE 那頭不成立,而給它一個「預設動作」只會讓忘了設定的區塊
    /// 安靜地做出某件沒人要的事 —— 比起編譯器當場指出來,那要難查得多。
    /// This property is required. An area without an action is not something LINE accepts, and a default action
    /// would only let a forgotten one quietly do something nobody asked for — far harder to track down than the
    /// compiler saying so on the spot.
    /// </remarks>
    [JsonPropertyName("action")]
    public required LineAction Action { get; set; }
}
