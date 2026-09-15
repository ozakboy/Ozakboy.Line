using System.Text.Json.Serialization;
using Ozakboy.Line.Messaging.RichMenu;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// 別名列表端點的回應形狀,只用於反序列化。
/// The alias list endpoint's response shape, used only for deserialisation.
/// </summary>
/// <remarks>
/// 與 <see cref="LineRichMenuListResponse"/> 的 <c>richmenus</c> 不同,這裡的欄位名是正常的 <c>aliases</c>;
/// 兩邊都標了明確的 <see cref="JsonPropertyNameAttribute"/>,不去記哪一個是例外。
/// Unlike <see cref="LineRichMenuListResponse"/>'s <c>richmenus</c>, this field is the ordinary <c>aliases</c>.
/// Both carry an explicit <see cref="JsonPropertyNameAttribute"/> so nobody has to remember which is the
/// exception.
/// </remarks>
internal sealed class LineRichMenuAliasListResponse
{
    /// <summary>
    /// 所有別名。
    /// Every alias.
    /// </summary>
    [JsonPropertyName("aliases")]
    public IReadOnlyList<LineRichMenuAlias>? Aliases { get; init; }
}
