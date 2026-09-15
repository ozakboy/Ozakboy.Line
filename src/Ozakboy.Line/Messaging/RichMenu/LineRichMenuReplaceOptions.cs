namespace Ozakboy.Line.Messaging.RichMenu;

/// <summary>
/// 「以新換舊」時的可選步驟。
/// The optional steps of replacing a rich menu with a new one.
/// </summary>
/// <remarks>
/// 三個欄位都不填也是合法的:那就只是「建一個選單並上傳圖片」。
/// Leaving all three unset is valid too, and means no more than creating a menu and uploading its image.
/// </remarks>
public sealed class LineRichMenuReplaceOptions
{
    /// <summary>
    /// 換完之後要刪掉的舊選單;不刪時為 <see langword="null"/>。
    /// The old menu to delete afterwards, or <see langword="null"/> to keep it.
    /// </summary>
    /// <remarks>
    /// 刪舊是<b>盡力而為</b>:刪不掉只記錄,不影響整體結果。新選單這時已經上線,為了刪不掉一個舊選單
    /// 而回報整件事失敗,只會讓呼叫端以為要重做一次 —— 而重做的結果是又多一個選單。
    /// Deleting the old menu is <b>best effort</b>: a failure is logged and changes nothing about the outcome. The
    /// new menu is live by then, and reporting the whole operation as failed would have the caller redo it — and
    /// a redo means yet another menu.
    /// </remarks>
    public string? OldRichMenuId { get; set; }

    /// <summary>
    /// 是否把新選單設為所有使用者的預設選單。
    /// Whether to make the new menu the default for every user.
    /// </summary>
    /// <remarks>
    /// 個別使用者身上的連結優先於預設選單,所以設了預設卻有人看到舊的,通常是那些人還掛著舊的個別連結。
    /// A per-user link wins over the default, so someone still seeing the old menu after this usually has an old
    /// per-user link on them.
    /// </remarks>
    public bool SetAsDefault { get; set; }

    /// <summary>
    /// 要指向新選單的別名;別名不存在就建立,已存在就改指向。
    /// The alias to point at the new menu; it is created when absent and repointed when it already exists.
    /// </summary>
    public string? AliasId { get; set; }
}
