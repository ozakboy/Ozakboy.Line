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
    /// 固定使用的 channel 名稱;<see langword="null"/>(預設)表示沒有指定,沿用單一 channel 的行為。
    /// A fixed channel name; <see langword="null"/> (the default) means none is specified and the
    /// single-channel behaviour applies.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 驗簽用 <c>IOptionsMonitor&lt;LineMessagingOptions&gt;.Get(name).ChannelSecret</c>,回覆與推播走
    /// <see cref="Ozakboy.Line.Messaging.ILineMessagingClientFactory"/> 取得的對應用戶端。名稱不在
    /// <see cref="Ozakboy.Line.Messaging.ILineMessagingClientFactory.ChannelNames"/> 內一律回 401,
    /// 而且在驗簽之前不碰任何狀態 —— 絕不拿空密鑰去驗一個不認得的 channel。
    /// The signature is checked against <c>IOptionsMonitor&lt;LineMessagingOptions&gt;.Get(name).ChannelSecret</c>,
    /// and replies and pushes go through the matching client from
    /// <see cref="Ozakboy.Line.Messaging.ILineMessagingClientFactory"/>. A name outside
    /// <see cref="Ozakboy.Line.Messaging.ILineMessagingClientFactory.ChannelNames"/> is always a 401, and nothing is
    /// touched before the check — an unknown channel is never verified against an empty secret.
    /// </para>
    /// <para>
    /// 與 <see cref="ChannelRouteParameter"/> 互斥,兩者都設會在<b>掛端點當下</b>擲出
    /// <see cref="InvalidOperationException"/>。空字串是預設 channel。
    /// It is mutually exclusive with <see cref="ChannelRouteParameter"/>; setting both throws an
    /// <see cref="InvalidOperationException"/> <b>while mapping</b>. The empty string is the default channel.
    /// </para>
    /// </remarks>
    public string? ChannelName { get; set; }

    /// <summary>
    /// 從路由值取得 channel 名稱時的路由參數名(例如 <c>"channel"</c>,搭配樣式 <c>/line/webhook/{channel}</c>);
    /// <see langword="null"/>(預設)表示不從路由取。
    /// The route parameter that carries the channel name (<c>"channel"</c>, say, with the pattern
    /// <c>/line/webhook/{channel}</c>); <see langword="null"/> (the default) means the route is not consulted.
    /// </summary>
    /// <remarks>
    /// 行為同 <see cref="ChannelName"/>;路由值缺漏(選擇性參數沒給)視為空字串,即預設 channel。
    /// 與 <see cref="ChannelName"/> 互斥。
    /// It behaves as <see cref="ChannelName"/>; an absent route value (an optional parameter left out) counts as the
    /// empty string, the default channel. It is mutually exclusive with <see cref="ChannelName"/>.
    /// </remarks>
    public string? ChannelRouteParameter { get; set; }

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
