using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.RichMenu;

/// <summary>
/// 要建立的圖文選單。
/// A rich menu to create.
/// </summary>
/// <remarks>
/// 建立選單與上傳圖片是<b>兩個步驟</b>:先 <see cref="ILineMessagingClient.CreateRichMenuAsync"/> 拿到 id,
/// 再 <see cref="ILineMessagingClient.UploadRichMenuImageAsync"/> 把圖片放上去。只建不傳的選單設得上去,
/// 但使用者那頭是一片空白。
/// Creating the menu and uploading its image are <b>two steps</b>:
/// <see cref="ILineMessagingClient.CreateRichMenuAsync"/> returns an id, and
/// <see cref="ILineMessagingClient.UploadRichMenuImageAsync"/> puts the image on it. A menu created without an
/// image can still be linked, and shows up blank.
/// </remarks>
public sealed class LineRichMenu
{
    /// <summary>
    /// 選單尺寸。
    /// The menu's size.
    /// </summary>
    [JsonPropertyName("size")]
    public LineRichMenuSize Size { get; set; } = new();

    /// <summary>
    /// 使用者開啟聊天室時選單是否預設展開。
    /// Whether the menu is expanded when the user opens the chat.
    /// </summary>
    [JsonPropertyName("selected")]
    public bool Selected { get; set; }

    /// <summary>
    /// 選單名稱,只在管理介面與 API 回應裡看得到,使用者看不到。
    /// The menu's name, visible in the console and in API responses but not to the user.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 聊天室下方那一條的文字,使用者看得到。
    /// The text on the chat bar, which the user does see.
    /// </summary>
    [JsonPropertyName("chatBarText")]
    public string ChatBarText { get; set; } = string.Empty;

    /// <summary>
    /// 可點擊的區塊。
    /// The tappable areas.
    /// </summary>
    [JsonPropertyName("areas")]
    public IList<LineRichMenuArea> Areas { get; } = [];
}
