using System.Text.Json;

namespace Ozakboy.Line.Messaging.Actions;

/// <summary>
/// 切換到另一個圖文選單的動作(分頁選單靠它)。
/// An action that switches to another rich menu, which is how tabbed menus work.
/// </summary>
/// <remarks>
/// <para>
/// 指向的是<b>別名</b>而不是選單識別碼。選單每次替換都會拿到新的識別碼,但別名可以一直指向最新那一個 ——
/// 寫識別碼的話,換一次選單就要把所有分頁動作裡的識別碼全部改一遍,漏掉一個的症狀是「按了跳到舊選單」。
/// It names an <b>alias</b> rather than a menu id. Every replacement yields a new id, but an alias can keep
/// pointing at the newest one; with ids written in, one replacement means rewriting every tab's action, and the
/// one that gets missed shows up as a tab that jumps to the old menu.
/// </para>
/// <para>
/// 切換之後 LINE 也會送一個 <c>postback</c> 事件,內容是這裡的 <see cref="Data"/>。
/// LINE also sends a <c>postback</c> event after the switch, carrying the <see cref="Data"/> given here.
/// </para>
/// </remarks>
public sealed class RichMenuSwitchAction : LineAction
{
    /// <summary>
    /// 建立切換圖文選單的動作。
    /// Creates a rich menu switch action.
    /// </summary>
    /// <param name="richMenuAliasId">目標選單的別名。The target menu's alias.</param>
    /// <param name="data">切換後回傳給 webhook 的資料。The data posted back after the switch.</param>
    /// <exception cref="ArgumentException">
    /// 任一參數為 <see langword="null"/> 或空白時擲出。
    /// Thrown when either argument is <see langword="null"/> or blank.
    /// </exception>
    public RichMenuSwitchAction(string richMenuAliasId, string data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(richMenuAliasId);
        ArgumentException.ThrowIfNullOrWhiteSpace(data);

        RichMenuAliasId = richMenuAliasId;
        Data = data;
    }

    /// <inheritdoc />
    public override string Type => "richmenuswitch";

    /// <summary>
    /// 目標選單的別名。
    /// The target menu's alias.
    /// </summary>
    public string RichMenuAliasId { get; }

    /// <summary>
    /// 切換後回傳給 webhook 的資料。
    /// The data posted back after the switch.
    /// </summary>
    public string Data { get; }

    /// <inheritdoc />
    internal override void WriteBody(Utf8JsonWriter writer)
    {
        writer.WriteString("richMenuAliasId", RichMenuAliasId);
        writer.WriteString("data", Data);
    }
}
