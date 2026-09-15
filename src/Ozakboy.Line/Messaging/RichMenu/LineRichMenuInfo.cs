using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.RichMenu;

/// <summary>
/// 已存在的圖文選單。
/// A rich menu that already exists.
/// </summary>
/// <remarks>
/// 與 <see cref="LineRichMenu"/> 的差別只在多了 <see cref="RichMenuId"/>。分成兩個型別,是因為
/// 「要建立的東西」不該有識別碼欄位 —— 有的話呼叫端遲早會填,而那個值 LINE 根本不看。
/// The only difference from <see cref="LineRichMenu"/> is <see cref="RichMenuId"/>. They are two types because
/// something yet to be created should not carry an identifier field: given one, a caller eventually fills it in,
/// and LINE does not read it.
/// </remarks>
public sealed class LineRichMenuInfo
{
    /// <summary>
    /// 選單識別碼。
    /// The menu's identifier.
    /// </summary>
    [JsonPropertyName("richMenuId")]
    public string RichMenuId { get; init; } = string.Empty;

    /// <summary>
    /// 選單尺寸。
    /// The menu's size.
    /// </summary>
    [JsonPropertyName("size")]
    public LineRichMenuSize Size { get; init; } = new();

    /// <summary>
    /// 是否預設展開。
    /// Whether it is expanded by default.
    /// </summary>
    [JsonPropertyName("selected")]
    public bool Selected { get; init; }

    /// <summary>
    /// 選單名稱。
    /// The menu's name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 聊天室下方那一條的文字。
    /// The text on the chat bar.
    /// </summary>
    [JsonPropertyName("chatBarText")]
    public string ChatBarText { get; init; } = string.Empty;

    /// <summary>
    /// 可點擊的區塊;其中的動作一律以
    /// <see cref="Ozakboy.Line.Messaging.Actions.RawAction"/> 呈現。
    /// The tappable areas, whose actions always arrive as
    /// <see cref="Ozakboy.Line.Messaging.Actions.RawAction"/>.
    /// </summary>
    [JsonPropertyName("areas")]
    public IReadOnlyList<LineRichMenuArea> Areas { get; init; } = [];
}
