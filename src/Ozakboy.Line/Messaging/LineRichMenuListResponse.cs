using System.Text.Json.Serialization;
using Ozakboy.Line.Messaging.RichMenu;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// 圖文選單列表端點的回應形狀,只用於反序列化。
/// The rich menu list endpoint's response shape, used only for deserialisation.
/// </summary>
/// <remarks>
/// 欄位名是全小寫的 <c>richmenus</c>,不是 <c>richMenus</c> —— 這是 LINE 規格裡少數不走 camelCase 的地方,
/// 靠命名原則自動轉會得到一個永遠是空的清單,而且不會有任何錯誤。
/// The field is the all-lowercase <c>richmenus</c> rather than <c>richMenus</c>: one of the few places LINE's
/// specification departs from camelCase, where relying on a naming policy yields a list that is always empty and
/// never an error.
/// </remarks>
internal sealed class LineRichMenuListResponse
{
    /// <summary>
    /// 所有圖文選單。
    /// Every rich menu.
    /// </summary>
    [JsonPropertyName("richmenus")]
    public IReadOnlyList<LineRichMenuInfo>? RichMenus { get; init; }
}
