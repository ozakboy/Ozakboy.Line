using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.RichMenu;

/// <summary>
/// 圖文選單別名:一個固定的名字,指向某一個會換來換去的選單。
/// A rich menu alias: a fixed name pointing at a menu that keeps being replaced.
/// </summary>
/// <remarks>
/// 別名的存在是為了讓「選單內容」與「指向選單的動作」脫鉤。
/// <see cref="Actions.RichMenuSwitchAction"/> 寫的是別名,換選單時只要把別名改指向新的識別碼,
/// 所有動作都不用動。反過來把識別碼直接寫進動作,換一次選單就是一次全面改寫。
/// An alias exists to decouple a menu's contents from the actions that point at it.
/// <see cref="Actions.RichMenuSwitchAction"/> names an alias, so replacing a menu means repointing the alias and
/// nothing else. Writing ids into actions instead makes every replacement a rewrite.
/// </remarks>
public sealed class LineRichMenuAlias
{
    /// <summary>
    /// 別名識別碼。
    /// The alias identifier.
    /// </summary>
    [JsonPropertyName("richMenuAliasId")]
    public string RichMenuAliasId { get; init; } = string.Empty;

    /// <summary>
    /// 這個別名目前指向的選單識別碼。
    /// The menu identifier this alias currently points at.
    /// </summary>
    [JsonPropertyName("richMenuId")]
    public string RichMenuId { get; init; } = string.Empty;
}
