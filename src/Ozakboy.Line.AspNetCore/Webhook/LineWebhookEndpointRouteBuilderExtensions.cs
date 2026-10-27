using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ozakboy.Core.Abstractions;
using Ozakboy.Line.AutoReply;
using Ozakboy.Line.Messaging;
using Ozakboy.Line.Webhook;

namespace Ozakboy.Line.AspNetCore.Webhook;

/// <summary>
/// 掛上一個會先驗簽再解析的 LINE webhook 端點。
/// Maps a LINE webhook endpoint that verifies the signature before parsing.
/// </summary>
/// <example>
/// <code>
/// app.MapLineWebhook("/line/webhook", async (evt, context, ct) =>
/// {
///     if (evt.Type == LineWebhookEventTypes.Message &amp;&amp; evt.Message?.Text is { } text)
///     {
///         var line = context.RequestServices.GetRequiredService&lt;ILineMessagingClient&gt;();
///         await line.ReplyTextAsync(evt.ReplyToken!, $"收到:{text}", ct);
///     }
/// });
/// </code>
/// </example>
public static partial class LineWebhookEndpointRouteBuilderExtensions
{
    /// <summary>
    /// 掛上 webhook 端點,逐一事件呼叫處理常式。
    /// Maps a webhook endpoint that invokes the handler once per event.
    /// </summary>
    /// <param name="endpoints">端點路由建構器。The endpoint route builder.</param>
    /// <param name="pattern">路由樣式。The route pattern.</param>
    /// <param name="handler">每個事件的處理常式。The handler, called for each event.</param>
    /// <returns>端點慣例建構器。The endpoint convention builder.</returns>
    /// <remarks>
    /// <para>
    /// 單一事件的例外<b>只記錄,不中斷同一批的其他事件,也不影響回應狀態碼</b>。LINE 一次可以送來多個事件,
    /// 而回應非 2xx 會讓 LINE 重送<b>整批</b> —— 一個事件的處理常式壞掉就讓整批都回 500,
    /// 結果是其他本來成功的事件被反覆重做,而真正壞掉的那個仍然壞掉。
    /// An exception from one event's handler is <b>logged, and stops neither the rest of the batch nor the
    /// response</b>. LINE can deliver several events at once, and a non-2xx answer makes it redeliver the
    /// <b>whole batch</b>: letting one broken handler return a 500 means the events that did succeed get redone
    /// over and over while the broken one stays broken.
    /// </para>
    /// <para>
    /// 處理常式應該要快。LINE 對 webhook 有回應時限,超時就當作失敗重送;耗時的工作請排進佇列,
    /// 在這裡只做「收下並排隊」。
    /// The handler should be quick. LINE applies a response deadline to webhooks and treats an overrun as a
    /// failure worth redelivering, so slow work belongs on a queue and this endpoint should do no more than
    /// accept and enqueue.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// 任一參數為 <see langword="null"/> 時擲出。Thrown when any argument is <see langword="null"/>.
    /// </exception>
    public static IEndpointConventionBuilder MapLineWebhook(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<LineWebhookEvent, HttpContext, CancellationToken, Task> handler) =>
        endpoints.MapLineWebhook(pattern, configure: null, handler);

    /// <summary>
    /// 掛上 webhook 端點,逐一事件呼叫處理常式,並可先跑一次關鍵字自動回覆。
    /// Maps a webhook endpoint that invokes the handler once per event, optionally running keyword auto reply
    /// first.
    /// </summary>
    /// <param name="endpoints">端點路由建構器。The endpoint route builder.</param>
    /// <param name="pattern">路由樣式。The route pattern.</param>
    /// <param name="configure">端點設定;為 <see langword="null"/> 時採用預設。The endpoint settings, or <see langword="null"/> for the defaults.</param>
    /// <param name="handler">每個事件的處理常式。The handler, called for each event.</param>
    /// <returns>端點慣例建構器。The endpoint convention builder.</returns>
    /// <remarks>
    /// <para>
    /// <see cref="LineWebhookEndpointOptions.AutoReply"/> 為 <see langword="true"/> 時,每個事件會<b>先</b>交給
    /// <see cref="ILineAutoReplyService"/>,結果放進
    /// <c>HttpContext.Items[LineWebhookItems.AutoReplyOutcome]</c>,然後<b>照常</b>呼叫處理常式。
    /// 自動回覆不取代處理常式,只是搶在它前面把「規則回得了的」回掉。
    /// With <see cref="LineWebhookEndpointOptions.AutoReply"/> set, each event goes to
    /// <see cref="ILineAutoReplyService"/> <b>first</b>, the outcome lands in
    /// <c>HttpContext.Items[LineWebhookItems.AutoReplyOutcome]</c>, and the handler is then called <b>as
    /// usual</b>. Auto reply does not replace the handler; it gets in front of it and answers what the rules can
    /// answer.
    /// </para>
    /// <para>
    /// 自動回覆的失敗(規則渲染不出來、LINE 拒絕回覆)只記錄,不中斷同一批的其他事件,也不影響狀態碼 ——
    /// 理由與處理常式擲例外時相同:回非 2xx 會讓 LINE 重送整批。
    /// A failing auto reply — a rule that will not render, a reply LINE refused — is logged and stops neither the
    /// rest of the batch nor the response, for the same reason as a throwing handler: a non-2xx answer makes LINE
    /// redeliver the whole batch.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="endpoints"/> 或 <paramref name="handler"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="endpoints"/> or <paramref name="handler"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// 開啟了自動回覆卻沒有註冊 <see cref="ILineAutoReplyService"/>,或開啟了維護模式 / 錯誤通知卻沒有管理員白名單
    /// (或沒有註冊 <see cref="ILineMessagingClient"/>)時,在<b>掛端點當下</b>擲出。
    /// Thrown <b>while mapping</b> when auto reply is on but no <see cref="ILineAutoReplyService"/> is registered,
    /// or maintenance mode / error notification is on without an administrator allow-list (or without an
    /// <see cref="ILineMessagingClient"/>).
    /// </exception>
    public static IEndpointConventionBuilder MapLineWebhook(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Action<LineWebhookEndpointOptions>? configure,
        Func<LineWebhookEvent, HttpContext, CancellationToken, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(handler);

        var options = new LineWebhookEndpointOptions();
        configure?.Invoke(options);

        return MapEvents(endpoints, pattern, options, handler);
    }

    /// <summary>
    /// 掛上 webhook 端點,把整批內容交給處理常式。
    /// Maps a webhook endpoint that hands the whole payload to the handler.
    /// </summary>
    /// <param name="endpoints">端點路由建構器。The endpoint route builder.</param>
    /// <param name="pattern">路由樣式。The route pattern.</param>
    /// <param name="handler">整批內容的處理常式。The handler, called once with the whole payload.</param>
    /// <returns>端點慣例建構器。The endpoint convention builder.</returns>
    /// <remarks>
    /// 需要自己決定「一批事件怎麼分工」時用這個多載 —— 例如要把整批塞進同一個佇列訊息,
    /// 或要依 <see cref="LineWebhookPayload.Destination"/> 分流到不同的官方帳號處理器。
    /// This overload is for deciding how a batch is handled as a whole: enqueuing it as one message, say, or
    /// routing by <see cref="LineWebhookPayload.Destination"/> to different official accounts' handlers.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// 任一參數為 <see langword="null"/> 時擲出。Thrown when any argument is <see langword="null"/>.
    /// </exception>
    public static IEndpointConventionBuilder MapLineWebhook(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<LineWebhookPayload, HttpContext, CancellationToken, Task> handler) =>
        endpoints.MapLineWebhook(pattern, handler, new LineWebhookEndpointOptions());

    /// <summary>
    /// 掛上 webhook 端點,把整批內容交給處理常式,並可指定這個端點對應的 channel。
    /// Maps a webhook endpoint that hands the whole payload to the handler, optionally pinning the channel the
    /// endpoint serves.
    /// </summary>
    /// <param name="endpoints">端點路由建構器。The endpoint route builder.</param>
    /// <param name="pattern">路由樣式。The route pattern.</param>
    /// <param name="handler">整批內容的處理常式。The handler, called once with the whole payload.</param>
    /// <param name="options">端點設定;只有 <see cref="LineWebhookEndpointOptions.ChannelName"/> 與 <see cref="LineWebhookEndpointOptions.ChannelRouteParameter"/> 對整批多載生效。The endpoint settings; of them only <see cref="LineWebhookEndpointOptions.ChannelName"/> and <see cref="LineWebhookEndpointOptions.ChannelRouteParameter"/> apply to this whole-payload overload.</param>
    /// <returns>端點慣例建構器。The endpoint convention builder.</returns>
    /// <remarks>
    /// 選項物件排在處理常式<b>之後</b>:排在前面的話,<see langword="null"/> 或未標型別的 lambda 會與逐事件多載的
    /// <c>Action&lt;LineWebhookEndpointOptions&gt;?</c> 參數產生歧義,那是既有程式碼的原始碼相容性問題。
    /// The options object comes <b>after</b> the handler: ahead of it, a <see langword="null"/> or an untyped
    /// lambda would be ambiguous with the per-event overload's <c>Action&lt;LineWebhookEndpointOptions&gt;?</c>
    /// parameter, a source-compatibility problem for existing code.
    /// 驗簽通過後,channel 名稱放在 <c>HttpContext.Items[LineWebhookItems.ChannelName]</c>。
    /// 維護模式、錯誤通知與自動回覆是逐事件的功能,這個多載不套用。
    /// Once the signature verifies, the channel name is in <c>HttpContext.Items[LineWebhookItems.ChannelName]</c>.
    /// Maintenance mode, error notification and auto reply are per-event features and do not apply here.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// 任一參數為 <see langword="null"/> 時擲出。
    /// Thrown when any argument is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <c>ChannelName</c> 與 <c>ChannelRouteParameter</c> 同時設定,或指定了 channel 卻沒有註冊
    /// <see cref="ILineMessagingClientFactory"/> 時,在<b>掛端點當下</b>擲出。
    /// Thrown <b>while mapping</b> when both <c>ChannelName</c> and <c>ChannelRouteParameter</c> are set, or a
    /// channel is specified without an <see cref="ILineMessagingClientFactory"/> registered.
    /// </exception>
    public static IEndpointConventionBuilder MapLineWebhook(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<LineWebhookPayload, HttpContext, CancellationToken, Task> handler,
        LineWebhookEndpointOptions options)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(options);

        return MapPayload(endpoints, pattern, LineWebhookChannelSelection.Create(options), handler);
    }

    /// <summary>
    /// 所有 webhook 端點共用的實作:先決定 channel、再驗簽、再解析、再交給處理常式。
    /// The implementation every webhook endpoint shares: decide the channel, verify the signature, parse, then
    /// hand over to the handler.
    /// </summary>
    /// <param name="endpoints">端點路由建構器。The endpoint route builder.</param>
    /// <param name="pattern">路由樣式。The route pattern.</param>
    /// <param name="channel">channel 挑選方式。How the channel is selected.</param>
    /// <param name="handler">整批內容的處理常式。The handler, called once with the whole payload.</param>
    /// <returns>端點慣例建構器。The endpoint convention builder.</returns>
    private static IEndpointConventionBuilder MapPayload(
        IEndpointRouteBuilder endpoints,
        string pattern,
        LineWebhookChannelSelection channel,
        Func<LineWebhookPayload, HttpContext, CancellationToken, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        if (channel.IsSelected)
        {
            RequireService(endpoints, typeof(ILineMessagingClientFactory), nameof(ILineMessagingClientFactory) + "(指定了 webhook 的 channel;請呼叫 services.AddLineMessaging(...))");
        }

        return endpoints.MapPost(pattern, async (HttpContext context) =>
        {
            var cancellationToken = context.RequestAborted;
            var channelName = string.Empty;
            string channelSecret;

            if (channel.IsSelected)
            {
                // 先確認 channel 存在才碰密鑰:名稱來自路由(不可信的輸入),不在清單內就在這裡回 401,
                // 不讀內容、不取選項、不拿空密鑰去驗簽。
                // Check the channel exists before touching any secret. The name comes from the route, which is
                // untrusted input: one outside the list is answered with a 401 right here — no body read, no
                // options fetched, and no signature checked against an empty key.
                channelName = channel.Resolve(context);
                var factory = context.RequestServices.GetRequiredService<ILineMessagingClientFactory>();
                if (!factory.Contains(channelName))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    Log.WebhookUnknownChannel(CreateLogger(context));
                    return;
                }

                channelSecret = context.RequestServices
                    .GetRequiredService<IOptionsMonitor<LineMessagingOptions>>()
                    .Get(channelName).ChannelSecret;
            }
            else
            {
                channelSecret = context.RequestServices
                    .GetRequiredService<IOptions<LineMessagingOptions>>()
                    .Value.ChannelSecret;
            }

            var payload = await context.Request
                .ReadLineWebhookAsync(channelSecret, cancellationToken)
                .ConfigureAwait(false);

            if (payload.IsFailure)
            {
                // 驗簽失敗回 401 而不是 400:這不是「內容有問題」,而是「這個請求沒有證明自己是 LINE 送的」。
                // 回 400 會讓 LINE 以為是自己送錯了而重送,而重送的簽章一樣過不了。
                // A failed signature answers 401 rather than 400: the problem is not a malformed body but a
                // request that has not shown it came from LINE. A 400 would suggest LINE sent something wrong and
                // invite a redelivery whose signature fails just the same.
                context.Response.StatusCode = string.Equals(payload.Error.Code, LineErrorCodes.WebhookInvalidSignature, StringComparison.Ordinal)
                    ? StatusCodes.Status401Unauthorized
                    : StatusCodes.Status400BadRequest;

                Log.WebhookRejected(CreateLogger(context), payload.Error.Code);
                return;
            }

            context.Items[LineWebhookItems.ChannelName] = channelName;

            await handler(payload.GetValueOrThrow(), context, cancellationToken).ConfigureAwait(false);

            // 一律 200。LINE 以狀態碼決定要不要重送整批,而到了這一步「收到且已交給處理常式」是事實,
            // 處理常式之後做得順不順已經是應用程式自己的事。
            // Always 200. LINE decides whether to redeliver the batch from this status code, and by this point it
            // is a fact that the batch arrived and reached the handler; how the handler fares afterwards is the
            // application's own business.
            context.Response.StatusCode = StatusCodes.Status200OK;
        });
    }

    /// <summary>
    /// 對一個事件跑自動回覆,並把結果放進 <c>HttpContext.Items</c>。
    /// Runs auto reply for one event and puts the outcome into <c>HttpContext.Items</c>.
    /// </summary>
    /// <param name="context">請求內容。The HTTP context.</param>
    /// <param name="logger">記錄器。The logger.</param>
    /// <param name="webhookEvent">事件。The event.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    private static async Task RunAutoReplyAsync(
        HttpContext context,
        ILogger logger,
        LineWebhookEvent webhookEvent,
        CancellationToken cancellationToken)
    {
        var service = context.RequestServices.GetRequiredService<ILineAutoReplyService>();

        Result<LineAutoReplyOutcome> outcome;
        try
        {
            outcome = await service.HandleAsync(webhookEvent, GetChannelName(context), cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // 理由同處理常式:一個事件的失敗不得讓整批回非 2xx 而被 LINE 重送。
        catch (Exception exception)
#pragma warning restore CA1031
        {
            Log.AutoReplyThrew(logger, webhookEvent.WebhookEventId, exception);
            return;
        }

        if (outcome.IsFailure)
        {
            Log.AutoReplyFailed(logger, webhookEvent.WebhookEventId, outcome.Error.Code);
            return;
        }

        context.Items[LineWebhookItems.AutoReplyOutcome] = outcome.GetValueOrThrow();
    }

    /// <summary>
    /// 取得驗簽通過後放在 <c>HttpContext.Items</c> 的 channel 名稱;沒有時為空字串(預設 channel)。
    /// Gets the channel name stored in <c>HttpContext.Items</c> after verification, or an empty string (the
    /// default channel) when absent.
    /// </summary>
    /// <param name="context">請求內容。The HTTP context.</param>
    /// <returns>channel 名稱。The channel name.</returns>
    private static string GetChannelName(HttpContext context) =>
        context.Items.TryGetValue(LineWebhookItems.ChannelName, out var name) && name is string text ? text : string.Empty;

    /// <summary>
    /// 取得某個 channel 的發訊用戶端:預設 channel 優先用直接註冊的 <see cref="ILineMessagingClient"/>
    /// (宿主可能替換它),具名 channel 走工廠。
    /// Gets a channel's messaging client: the default channel prefers a directly registered
    /// <see cref="ILineMessagingClient"/> (a host may have replaced it), and a named channel goes through the
    /// factory.
    /// </summary>
    /// <param name="services">請求的服務提供者。The request's service provider.</param>
    /// <param name="channelName">channel 名稱。The channel name.</param>
    /// <returns>發訊用戶端。The messaging client.</returns>
    private static ILineMessagingClient ResolveMessaging(IServiceProvider services, string channelName)
    {
        if (channelName.Length == 0 && services.GetService<ILineMessagingClient>() is { } defaultClient)
        {
            return defaultClient;
        }

        return services.GetRequiredService<ILineMessagingClientFactory>().Get(channelName);
    }

    /// <summary>
    /// 取得這個端點用的記錄器。
    /// Gets the logger for this endpoint.
    /// </summary>
    /// <param name="context">請求內容。The HTTP context.</param>
    /// <returns>記錄器。The logger.</returns>
    private static ILogger CreateLogger(HttpContext context) =>
        context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Ozakboy.Line.AspNetCore.Webhook");

    /// <summary>
    /// 記錄訊息的定義。
    /// The log message definitions.
    /// </summary>
    private static partial class Log
    {
        /// <summary>
        /// 單一事件的處理常式擲出例外時的記錄。
        /// Logged when one event's handler threw.
        /// </summary>
        /// <param name="logger">記錄器。The logger.</param>
        /// <param name="eventType">事件型別。The event type.</param>
        /// <param name="webhookEventId">事件識別碼。The event identifier.</param>
        /// <param name="exception">例外。The exception.</param>
        [LoggerMessage(
            EventId = 2200,
            Level = LogLevel.Error,
            Message = "LINE webhook 事件處理失敗:型別 {EventType}、識別碼 {WebhookEventId};同一批的其他事件不受影響。Handling a LINE webhook event failed: type {EventType}, id {WebhookEventId}; the rest of the batch is unaffected.")]
        internal static partial void EventHandlerFailed(ILogger logger, string eventType, string webhookEventId, Exception exception);

        /// <summary>
        /// webhook 請求被拒絕時的記錄。
        /// Logged when a webhook request is rejected.
        /// </summary>
        /// <param name="logger">記錄器。The logger.</param>
        /// <param name="errorCode">失敗代碼。The failure code.</param>
        [LoggerMessage(
            EventId = 2201,
            Level = LogLevel.Warning,
            Message = "LINE webhook 請求被拒絕({ErrorCode})。A LINE webhook request was rejected ({ErrorCode}).")]
        internal static partial void WebhookRejected(ILogger logger, string errorCode);

        /// <summary>
        /// webhook 請求指向不存在的 channel 時的記錄(不記名稱:那是未驗證的輸入)。
        /// Logged when a webhook request targets a channel that does not exist. The name is not logged, being
        /// unverified input.
        /// </summary>
        /// <param name="logger">記錄器。The logger.</param>
        [LoggerMessage(
            EventId = 2208,
            Level = LogLevel.Warning,
            Message = "LINE webhook 請求指向未註冊的 channel,回 401。A LINE webhook request targeted an unregistered channel and was answered with 401.")]
        internal static partial void WebhookUnknownChannel(ILogger logger);

        /// <summary>
        /// 自動回覆回報失敗時的記錄。
        /// Logged when the auto reply reported a failure.
        /// </summary>
        /// <param name="logger">記錄器。The logger.</param>
        /// <param name="webhookEventId">事件識別碼。The event identifier.</param>
        /// <param name="errorCode">失敗代碼。The failure code.</param>
        [LoggerMessage(
            EventId = 2202,
            Level = LogLevel.Warning,
            Message = "事件 {WebhookEventId} 的自動回覆失敗({ErrorCode});處理常式照常執行。Auto reply failed for event {WebhookEventId} ({ErrorCode}); the handler still runs.")]
        internal static partial void AutoReplyFailed(ILogger logger, string webhookEventId, string errorCode);

        /// <summary>
        /// 自動回覆擲出例外時的記錄。
        /// Logged when the auto reply threw.
        /// </summary>
        /// <param name="logger">記錄器。The logger.</param>
        /// <param name="webhookEventId">事件識別碼。The event identifier.</param>
        /// <param name="exception">例外。The exception.</param>
        [LoggerMessage(
            EventId = 2203,
            Level = LogLevel.Error,
            Message = "事件 {WebhookEventId} 的自動回覆擲出例外;處理常式照常執行。Auto reply threw for event {WebhookEventId}; the handler still runs.")]
        internal static partial void AutoReplyThrew(ILogger logger, string webhookEventId, Exception exception);

        /// <summary>
        /// 維護模式的回覆沒有送出時的記錄。
        /// Logged when the maintenance reply could not be sent.
        /// </summary>
        /// <param name="logger">記錄器。The logger.</param>
        /// <param name="webhookEventId">事件識別碼。The event identifier.</param>
        /// <param name="errorCode">失敗代碼。The failure code.</param>
        [LoggerMessage(
            EventId = 2204,
            Level = LogLevel.Warning,
            Message = "事件 {WebhookEventId} 的維護中回覆沒有送出({ErrorCode})。The maintenance reply for event {WebhookEventId} was not sent ({ErrorCode}).")]
        internal static partial void MaintenanceReplyFailed(ILogger logger, string webhookEventId, string errorCode);

        /// <summary>
        /// 維護模式的回覆擲出例外時的記錄。
        /// Logged when sending the maintenance reply threw.
        /// </summary>
        /// <param name="logger">記錄器。The logger.</param>
        /// <param name="webhookEventId">事件識別碼。The event identifier.</param>
        /// <param name="exception">例外。The exception.</param>
        [LoggerMessage(
            EventId = 2205,
            Level = LogLevel.Error,
            Message = "事件 {WebhookEventId} 的維護中回覆擲出例外。Sending the maintenance reply for event {WebhookEventId} threw.")]
        internal static partial void MaintenanceReplyThrew(ILogger logger, string webhookEventId, Exception exception);

        /// <summary>
        /// 錯誤通知沒有送達某位管理員時的記錄。
        /// Logged when an error notification did not reach one administrator.
        /// </summary>
        /// <param name="logger">記錄器。The logger.</param>
        /// <param name="webhookEventId">觸發通知的事件識別碼。The identifier of the event that triggered it.</param>
        /// <param name="errorCode">失敗代碼。The failure code.</param>
        [LoggerMessage(
            EventId = 2206,
            Level = LogLevel.Warning,
            Message = "事件 {WebhookEventId} 的錯誤通知沒有送達一位管理員({ErrorCode})。The error notification for event {WebhookEventId} did not reach an administrator ({ErrorCode}).")]
        internal static partial void ErrorNotificationFailed(ILogger logger, string webhookEventId, string errorCode);

        /// <summary>
        /// 錯誤通知擲出例外(含逾時)時的記錄。
        /// Logged when an error notification threw, a timeout included.
        /// </summary>
        /// <param name="logger">記錄器。The logger.</param>
        /// <param name="webhookEventId">觸發通知的事件識別碼。The identifier of the event that triggered it.</param>
        /// <param name="exception">例外。The exception.</param>
        [LoggerMessage(
            EventId = 2207,
            Level = LogLevel.Error,
            Message = "事件 {WebhookEventId} 的錯誤通知擲出例外。Sending the error notification for event {WebhookEventId} threw.")]
        internal static partial void ErrorNotificationThrew(ILogger logger, string webhookEventId, Exception exception);
    }
}
