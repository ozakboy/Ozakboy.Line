namespace Ozakboy.Line.AspNetCore.Webhook;

/// <summary>
/// webhook 端點的行為設定。
/// How the webhook endpoint behaves.
/// </summary>
public sealed class LineWebhookEndpointOptions
{
    /// <summary>
    /// 是否在呼叫處理常式<b>之前</b>先跑一次關鍵字自動回覆,預設為 <see langword="false"/>。
    /// Whether to run keyword auto reply <b>before</b> the handler; <see langword="false"/> by default.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 預設關閉是刻意的:自動回覆會真的送訊息出去,不該因為升級套件就無聲地開始講話。
    /// It is off by default on purpose: auto reply actually sends messages, and upgrading a package should not
    /// silently start a conversation.
    /// </para>
    /// <para>
    /// 開啟時必須先 <c>services.AddLineAutoReply(...)</c>,否則掛端點時就會擲出
    /// <see cref="InvalidOperationException"/> —— 在啟動時失敗,而不是等第一個使用者傳訊息才發現沒有回應。
    /// Turning it on requires <c>services.AddLineAutoReply(...)</c> first, or mapping the endpoint throws an
    /// <see cref="InvalidOperationException"/>: it fails at startup rather than when the first user's message
    /// goes unanswered.
    /// </para>
    /// <para>
    /// 自動回覆跑完之後,處理常式<b>照常</b>被呼叫,結果放在
    /// <c>HttpContext.Items[LineWebhookItems.AutoReplyOutcome]</c>。要不要因為「已經自動回覆過了」
    /// 而略過自己的處理,由宿主自己決定 —— 這個套件不替宿主決定它的業務邏輯。
    /// The handler is called <b>as usual</b> afterwards, with the outcome in
    /// <c>HttpContext.Items[LineWebhookItems.AutoReplyOutcome]</c>. Whether to skip its own work because an auto
    /// reply already went out is the host's decision; the package does not make business decisions for it.
    /// </para>
    /// </remarks>
    public bool AutoReply { get; set; }

    /// <summary>
    /// 維護模式:開啟時,非管理員的事件不交給處理常式也不跑自動回覆,只回一則維護中的文字。
    /// Maintenance mode: while on, a non-administrator's event reaches neither the handler nor auto reply and
    /// is answered with a maintenance text.
    /// </summary>
    /// <remarks>
    /// 對所有 <c>MapLineWebhook</c> 的逐事件多載生效(含泛型的 <c>MapLineWebhook&lt;THandler&gt;</c>);
    /// 整批內容的多載由呼叫端自己分工,不套用。
    /// It applies to every per-event <c>MapLineWebhook</c> overload, the generic
    /// <c>MapLineWebhook&lt;THandler&gt;</c> included; the whole-payload overload leaves the division of work to
    /// the caller and is not covered.
    /// </remarks>
    public LineWebhookMaintenanceOptions Maintenance { get; } = new();

    /// <summary>
    /// 錯誤通知:處理常式擲出例外時,推播通知管理員。生效範圍同 <see cref="Maintenance"/>。
    /// Error notification: push a note to administrators when a handler throws. It covers the same overloads as
    /// <see cref="Maintenance"/>.
    /// </summary>
    public LineWebhookErrorNotificationOptions ErrorNotification { get; } = new();
}
