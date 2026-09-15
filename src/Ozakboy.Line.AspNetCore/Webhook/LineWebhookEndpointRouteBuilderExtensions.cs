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
    /// 開啟了自動回覆卻沒有註冊 <see cref="ILineAutoReplyService"/> 時,在<b>掛端點當下</b>擲出。
    /// Thrown <b>while mapping</b> when auto reply is on but no <see cref="ILineAutoReplyService"/> is registered.
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

        if (options.AutoReply && endpoints.ServiceProvider.GetService<ILineAutoReplyService>() is null)
        {
            // 在掛端點當下就失敗,而不是等第一個使用者傳訊息、發現沒有回應才開始查。
            // 漏掉註冊在執行期沒有任何徵兆:端點回 200、log 一片乾淨,只是機器人不說話。
            // Fail while mapping rather than when the first user's message goes unanswered. A missing
            // registration leaves no trace at runtime: the endpoint answers 200, the log is clean, and the bot
            // simply never speaks.
            throw new InvalidOperationException(
                "開啟了 webhook 端點的自動回覆,但容器裡沒有 ILineAutoReplyService,請先呼叫 services.AddLineAutoReply(...)。Auto reply is enabled on the webhook endpoint but no ILineAutoReplyService is registered; call services.AddLineAutoReply(...) first.");
        }

        return endpoints.MapLineWebhook(pattern, async (payload, context, cancellationToken) =>
        {
            var logger = CreateLogger(context);

            for (var index = 0; index < payload.Events.Count; index++)
            {
                var webhookEvent = payload.Events[index];

                if (options.AutoReply)
                {
                    await RunAutoReplyAsync(context, logger, webhookEvent, cancellationToken).ConfigureAwait(false);
                }

                try
                {
                    await handler(webhookEvent, context, cancellationToken).ConfigureAwait(false);
                }
#pragma warning disable CA1031 // 這裡刻意攔截所有例外:一個事件的失敗不得影響同一批的其他事件。
                catch (Exception exception)
#pragma warning restore CA1031
                {
                    Log.EventHandlerFailed(logger, webhookEvent.Type, webhookEvent.WebhookEventId, exception);
                }
            }
        });
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
        Func<LineWebhookPayload, HttpContext, CancellationToken, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(handler);

        return endpoints.MapPost(pattern, async (HttpContext context) =>
        {
            var cancellationToken = context.RequestAborted;
            var channelSecret = context.RequestServices
                .GetRequiredService<IOptions<LineMessagingOptions>>()
                .Value.ChannelSecret;

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
            outcome = await service.HandleAsync(webhookEvent, cancellationToken).ConfigureAwait(false);
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
    }
}
