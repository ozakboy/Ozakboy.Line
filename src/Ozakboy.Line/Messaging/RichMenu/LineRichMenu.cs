using System.Globalization;
using System.Text.Json.Serialization;
using Ozakboy.Core.Abstractions;

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

    /// <summary>
    /// 送出前的本地檢查:每個區塊的動作都要符合規格(標籤 ≤ 20 字、postback data ≤ 300、uri 協定白名單等)。
    /// The local check before sending: every area's action must meet the specification (label of at most 20
    /// characters, postback data of at most 300, a uri scheme from the allow-list, and so on).
    /// </summary>
    /// <returns>
    /// 通過時為成功;第一個不合規格的動作回 <see cref="LineErrorCodes.InvalidAction"/> 失敗,
    /// 訊息裡帶 <c>richMenu.areas[i]</c> 指出是第幾個區塊。
    /// Success when it passes; the first action that breaks the specification fails with
    /// <see cref="LineErrorCodes.InvalidAction"/>, the message naming <c>richMenu.areas[i]</c> to point at the area.
    /// </returns>
    /// <remarks>
    /// <see cref="Actions.RawAction"/> 不驗(逃生口,內容由呼叫端負責),所以 MCP 工具讀進來的選單(一律是 RawAction)
    /// 不會在這裡被擋 —— 它們的最後防線仍是 LINE 自己的 validate 端點。
    /// <see cref="Actions.RawAction"/> is not checked (it is the escape hatch, its content is the caller's
    /// responsibility), so a menu an MCP tool reads in — always RawAction — is not stopped here; its last line of
    /// defence is LINE's own validate endpoint.
    /// </remarks>
    public Result Validate()
    {
        for (var index = 0; index < Areas.Count; index++)
        {
            var checkedAction = Areas[index].Action.Validate(
                LineMessagingLimits.MaxActionLabelLength,
                string.Create(CultureInfo.InvariantCulture, $"richMenu.areas[{index}]"));
            if (checkedAction.IsFailure)
            {
                return checkedAction;
            }
        }

        return Result.Success();
    }
}
