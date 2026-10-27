namespace Ozakboy.Line.AspNetCore.Webhook;

/// <summary>
/// webhook 端點的維護模式設定。
/// The webhook endpoint's maintenance mode.
/// </summary>
/// <remarks>
/// <para>
/// 開啟時,<b>來源使用者</b>(<c>Source.UserId</c>)不在 <see cref="AdminUserIds"/> 的事件不會交給處理常式、
/// 也不會跑自動回覆;若事件有回覆權杖,改以 <see cref="ReplyText"/> 回一則文字。管理員的事件照常處理,
/// 方便在維護期間驗證修正。
/// While on, an event whose <b>source user</b> (<c>Source.UserId</c>) is not in <see cref="AdminUserIds"/>
/// reaches neither the handler nor auto reply; if it carries a reply token, <see cref="ReplyText"/> is sent
/// back instead. Administrators' events are handled as usual so a fix can be verified during maintenance.
/// </para>
/// <para>
/// 判斷一律看 <c>UserId</c>,<b>群組事件也一樣</b>(看的是發話的人,不是 groupId);沒有 <c>UserId</c> 的事件
/// 視為非管理員。<c>standby</c> 模式的事件不回覆(別的模組正在接手對話),但同樣不交給處理常式。
/// The check is always on <c>UserId</c>, <b>group events included</b> (who spoke, not the groupId); an event with
/// no <c>UserId</c> counts as a non-administrator. A <c>standby</c> event is not answered — another module owns
/// the conversation — but is kept from the handler all the same.
/// </para>
/// <para>
/// 被擋下的請求會在 <c>HttpContext.Items[LineWebhookItems.MaintenanceSkipped]</c> 標上 <see langword="true"/>,
/// 回應狀態碼不變(一律 200,否則 LINE 會重送)。
/// A request that had events held back gets <see langword="true"/> in
/// <c>HttpContext.Items[LineWebhookItems.MaintenanceSkipped]</c>; the status code is unchanged (still 200, or
/// LINE would redeliver).
/// </para>
/// </remarks>
public sealed class LineWebhookMaintenanceOptions
{
    /// <summary>
    /// 是否開啟維護模式,預設為 <see langword="false"/>。
    /// Whether maintenance mode is on; <see langword="false"/> by default.
    /// </summary>
    /// <remarks>
    /// 開啟但 <see cref="AdminUserIds"/> 為空時,掛端點當下擲出 <see cref="InvalidOperationException"/> ——
    /// 沒有管理員的維護模式等於把自己也鎖在外面。
    /// Turning it on with an empty <see cref="AdminUserIds"/> makes mapping throw an
    /// <see cref="InvalidOperationException"/>: maintenance without an administrator locks the owner out too.
    /// </remarks>
    public bool Enabled { get; set; }

    /// <summary>
    /// 管理員的 LINE 使用者識別碼白名單。
    /// The allow-list of administrators' LINE user IDs.
    /// </summary>
    public IList<string> AdminUserIds { get; } = [];

    /// <summary>
    /// 回給非管理員的文字,預設為「系統維護中,請稍後再試。」。
    /// The text sent to non-administrators; "系統維護中,請稍後再試。" by default.
    /// </summary>
    public string ReplyText { get; set; } = "系統維護中,請稍後再試。";
}
