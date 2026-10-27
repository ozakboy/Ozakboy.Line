using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ozakboy.Line.AutoReply;
using Ozakboy.Line.Messaging;
using Ozakboy.Line.Webhook;
using Ozakboy.Line.Webhook.Handling;

namespace Ozakboy.Line.AspNetCore.Webhook;

public static partial class LineWebhookEndpointRouteBuilderExtensions
{
    /// <summary>
    /// 錯誤通知整體可花的最長時間;超過就放棄,回應不等它。
    /// The longest the error notification as a whole may take; past it, give up rather than hold the response.
    /// </summary>
    private static readonly TimeSpan ErrorNotificationTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 掛上 webhook 端點,事件交給 <typeparamref name="THandler"/>(<see cref="LineWebhookHandler"/> 的衍生類別)分派。
    /// Maps a webhook endpoint whose events are dispatched by <typeparamref name="THandler"/>, a
    /// <see cref="LineWebhookHandler"/> subclass.
    /// </summary>
    /// <typeparam name="THandler">處理常式型別;須已註冊進容器(<c>services.AddLineWebhookHandler&lt;THandler&gt;()</c>)。The handler type, which must be registered (<c>services.AddLineWebhookHandler&lt;THandler&gt;()</c>).</typeparam>
    /// <param name="endpoints">端點路由建構器。The endpoint route builder.</param>
    /// <param name="pattern">路由樣式。The route pattern.</param>
    /// <returns>端點慣例建構器。The endpoint convention builder.</returns>
    /// <remarks>
    /// 等同以預設的 <see cref="LineWebhookEndpointOptions"/> 呼叫另一個泛型多載。
    /// Equivalent to the other generic overload with default <see cref="LineWebhookEndpointOptions"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// 任一參數為 <see langword="null"/> 時擲出。Thrown when any argument is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="THandler"/> 沒有註冊時,在<b>掛端點當下</b>擲出。
    /// Thrown <b>while mapping</b> when <typeparamref name="THandler"/> is not registered.
    /// </exception>
    public static IEndpointConventionBuilder MapLineWebhook<THandler>(
        this IEndpointRouteBuilder endpoints,
        string pattern)
        where THandler : LineWebhookHandler =>
        endpoints.MapLineWebhook<THandler>(pattern, new LineWebhookEndpointOptions());

    /// <summary>
    /// 掛上 webhook 端點,事件交給 <typeparamref name="THandler"/> 分派,並套用端點設定。
    /// Maps a webhook endpoint whose events are dispatched by <typeparamref name="THandler"/>, with endpoint
    /// settings applied.
    /// </summary>
    /// <typeparam name="THandler">處理常式型別。The handler type.</typeparam>
    /// <param name="endpoints">端點路由建構器。The endpoint route builder.</param>
    /// <param name="pattern">路由樣式。The route pattern.</param>
    /// <param name="options">端點設定(自動回覆、維護模式、錯誤通知)。掛端點時就讀取並複製,之後再改不生效。The endpoint settings — auto reply, maintenance mode, error notification — read and copied when mapping; later changes have no effect.</param>
    /// <returns>端點慣例建構器。The endpoint convention builder.</returns>
    /// <remarks>
    /// <para>
    /// 每個請求在自己的 DI 範圍內以 <c>GetRequiredService&lt;THandler&gt;()</c> 取得處理常式,
    /// 所以 <see cref="Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped"/> 的相依(例如 DbContext)可以直接注入。
    /// 每個事件另建一個 <see cref="LineWebhookContext"/>。
    /// Each request resolves the handler with <c>GetRequiredService&lt;THandler&gt;()</c> inside its own DI scope,
    /// so scoped dependencies such as a DbContext can be injected directly. Each event gets its own
    /// <see cref="LineWebhookContext"/>.
    /// </para>
    /// <para>
    /// 錯誤隔離與狀態碼契約同逐事件的委派多載:單一事件擲例外只記錄,同一批的其餘事件照常,
    /// 驗簽失敗 401、解析失敗 400、其餘一律 200。自動回覆開啟時先跑,結果同時放進
    /// <see cref="LineWebhookContext.AutoReplyOutcome"/> 與 <c>HttpContext.Items</c>。
    /// Error isolation and the status-code contract are those of the per-event delegate overload: one event's
    /// exception is only logged and the rest of the batch carries on; a failed signature is 401, an unparsable
    /// body 400, everything else 200. Auto reply, when on, runs first and its outcome lands in both
    /// <see cref="LineWebhookContext.AutoReplyOutcome"/> and <c>HttpContext.Items</c>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// 任一參數為 <see langword="null"/> 時擲出。Thrown when any argument is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="THandler"/> 或 <see cref="ILineMessagingClient"/> 沒有註冊、開啟了自動回覆卻沒有
    /// <see cref="ILineAutoReplyService"/>、或開啟了維護模式 / 錯誤通知卻沒有管理員白名單時,在<b>掛端點當下</b>擲出。
    /// Thrown <b>while mapping</b> when <typeparamref name="THandler"/> or <see cref="ILineMessagingClient"/> is
    /// not registered, auto reply is on without an <see cref="ILineAutoReplyService"/>, or maintenance mode /
    /// error notification is on without an administrator allow-list.
    /// </exception>
    public static IEndpointConventionBuilder MapLineWebhook<THandler>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        LineWebhookEndpointOptions options)
        where THandler : LineWebhookHandler
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(options);

        RequireService(endpoints, typeof(THandler), $"{typeof(THandler).Name}(請呼叫 services.AddLineWebhookHandler<{typeof(THandler).Name}>())");
        RequireService(endpoints, typeof(ILineMessagingClient), nameof(ILineMessagingClient) + "(請呼叫 services.AddLineMessaging(...))");

        return MapEvents(endpoints, pattern, options, static async (webhookEvent, httpContext, cancellationToken) =>
        {
            var services = httpContext.RequestServices;
            var handler = services.GetRequiredService<THandler>();
            var messaging = services.GetRequiredService<ILineMessagingClient>();
            httpContext.Items.TryGetValue(LineWebhookItems.AutoReplyOutcome, out var outcome);

            var context = new LineWebhookContext(webhookEvent, messaging, string.Empty, outcome as LineAutoReplyOutcome);
            await handler.HandleAsync(webhookEvent, context, cancellationToken).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// 所有逐事件多載共用的實作:驗證設定、掛端點、逐事件走「維護模式 → 自動回覆 → 處理常式 → 錯誤通知」。
    /// The implementation every per-event overload shares: validate the settings, map the endpoint, and take each
    /// event through maintenance mode, auto reply, the handler, and error notification.
    /// </summary>
    /// <param name="endpoints">端點路由建構器。The endpoint route builder.</param>
    /// <param name="pattern">路由樣式。The route pattern.</param>
    /// <param name="options">端點設定。The endpoint settings.</param>
    /// <param name="handler">每個事件的處理常式。The handler, called for each event.</param>
    /// <returns>端點慣例建構器。The endpoint convention builder.</returns>
    private static IEndpointConventionBuilder MapEvents(
        IEndpointRouteBuilder endpoints,
        string pattern,
        LineWebhookEndpointOptions options,
        Func<LineWebhookEvent, HttpContext, CancellationToken, Task> handler)
    {
        var plan = EventPlan.Create(endpoints, options);

        return endpoints.MapLineWebhook(pattern, async (LineWebhookPayload payload, HttpContext context, CancellationToken cancellationToken) =>
        {
            var logger = CreateLogger(context);

            for (var index = 0; index < payload.Events.Count; index++)
            {
                var webhookEvent = payload.Events[index];

                if (plan.MaintenanceEnabled && !plan.IsMaintenanceAdmin(webhookEvent))
                {
                    context.Items[LineWebhookItems.MaintenanceSkipped] = true;
                    await ReplyMaintenanceAsync(context, logger, plan.MaintenanceReplyText, webhookEvent, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (plan.AutoReply)
                {
                    // 同一批的上一個事件留下的結果不能被這個事件看到:自動回覆失敗時契約是「不放值」。
                    // A previous event's outcome must not be visible to this one: the contract on an auto reply
                    // failure is that no value is present.
                    context.Items.Remove(LineWebhookItems.AutoReplyOutcome);
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

                    // 請求本身被中止(用戶端斷線)不是處理常式壞了,不通知。
                    // An aborted request (the caller hung up) is not a broken handler; no notification.
                    if (plan.NotificationEnabled && !cancellationToken.IsCancellationRequested)
                    {
                        await NotifyAdminsAsync(context, logger, plan, webhookEvent, exception, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        });
    }

    /// <summary>
    /// 要求容器裡有某個服務,否則在掛端點當下擲出。
    /// Requires a service in the container, throwing while mapping if it is missing.
    /// </summary>
    /// <param name="endpoints">端點路由建構器。The endpoint route builder.</param>
    /// <param name="serviceType">服務型別。The service type.</param>
    /// <param name="description">缺少時顯示的說明。The description shown when it is missing.</param>
    private static void RequireService(IEndpointRouteBuilder endpoints, Type serviceType, string description)
    {
        var isService = endpoints.ServiceProvider.GetService<IServiceProviderIsService>();

        // 容器不提供查詢能力時(非預設容器)放行,留給執行期的 GetRequiredService 去說明。
        // A container that cannot answer the question (a non-default one) is let through; the runtime
        // GetRequiredService will explain.
        if (isService is not null && !isService.IsService(serviceType))
        {
            throw new InvalidOperationException(
                $"webhook 端點需要容器裡有 {description}。The webhook endpoint needs {serviceType.Name} to be registered.");
        }
    }

    /// <summary>
    /// 以維護中的文字回覆一個被擋下的事件;失敗只記錄。
    /// Replies to a held-back event with the maintenance text; failures are only logged.
    /// </summary>
    /// <param name="context">請求內容。The HTTP context.</param>
    /// <param name="logger">記錄器。The logger.</param>
    /// <param name="text">要回的文字。The text to send.</param>
    /// <param name="webhookEvent">事件。The event.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    private static async Task ReplyMaintenanceAsync(
        HttpContext context,
        ILogger logger,
        string text,
        LineWebhookEvent webhookEvent,
        CancellationToken cancellationToken)
    {
        // standby:別的模組(例如人工客服)正在接手對話,回覆等於搶話。
        // standby: another module, a human operator console say, owns the conversation; replying talks over it.
        if (string.IsNullOrEmpty(webhookEvent.ReplyToken)
            || string.Equals(webhookEvent.Mode, "standby", StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            var messaging = context.RequestServices.GetRequiredService<ILineMessagingClient>();
            var result = await messaging.ReplyTextAsync(webhookEvent.ReplyToken, text, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                Log.MaintenanceReplyFailed(logger, webhookEvent.WebhookEventId, result.Error.Code);
            }
        }
#pragma warning disable CA1031 // 維護中的回覆壞了,也不能讓請求回非 2xx。
        catch (Exception exception)
#pragma warning restore CA1031
        {
            Log.MaintenanceReplyThrew(logger, webhookEvent.WebhookEventId, exception);
        }
    }

    /// <summary>
    /// 推播通知每一位管理員;各自獨立失敗,整體有時限,絕不擲出。
    /// Pushes a notification to every administrator; each fails independently, the whole is time-boxed, and
    /// nothing ever escapes.
    /// </summary>
    /// <param name="context">請求內容。The HTTP context.</param>
    /// <param name="logger">記錄器。The logger.</param>
    /// <param name="plan">端點設定的快照。The endpoint settings snapshot.</param>
    /// <param name="webhookEvent">出錯的事件。The failing event.</param>
    /// <param name="exception">處理常式擲出的例外。The exception the handler threw.</param>
    /// <param name="requestToken">請求的取消權杖。The request's cancellation token.</param>
    private static async Task NotifyAdminsAsync(
        HttpContext context,
        ILogger logger,
        EventPlan plan,
        LineWebhookEvent webhookEvent,
        Exception exception,
        CancellationToken requestToken)
    {
        try
        {
            var messaging = context.RequestServices.GetRequiredService<ILineMessagingClient>();
            var text = BuildErrorNotification(webhookEvent, exception, plan.IncludeStackTrace);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(requestToken);
            timeout.CancelAfter(ErrorNotificationTimeout);

            var pushes = new List<Task>(plan.NotificationAdminUserIds.Count);
            foreach (var adminUserId in plan.NotificationAdminUserIds)
            {
                pushes.Add(PushToAdminAsync(messaging, logger, adminUserId, text, webhookEvent.WebhookEventId, timeout.Token));
            }

            await Task.WhenAll(pushes).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // 通知本身的任何失敗都只記錄:它不得改變 200 的契約。
        catch (Exception notifyException)
#pragma warning restore CA1031
        {
            Log.ErrorNotificationThrew(logger, webhookEvent.WebhookEventId, notifyException);
        }
    }

    /// <summary>
    /// 推播給一位管理員;失敗只記錄,不擲出。
    /// Pushes to one administrator; failures are logged and never thrown.
    /// </summary>
    /// <param name="messaging">發訊用戶端。The messaging client.</param>
    /// <param name="logger">記錄器。The logger.</param>
    /// <param name="adminUserId">管理員的使用者識別碼。The administrator's user ID.</param>
    /// <param name="text">通知內容。The notification text.</param>
    /// <param name="webhookEventId">出錯事件的識別碼。The failing event's identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    private static async Task PushToAdminAsync(
        ILineMessagingClient messaging,
        ILogger logger,
        string adminUserId,
        string text,
        string webhookEventId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await messaging.PushTextAsync(adminUserId, text, null, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                Log.ErrorNotificationFailed(logger, webhookEventId, result.Error.Code);
            }
        }
#pragma warning disable CA1031 // 一位管理員收不到,不能影響其他管理員。
        catch (Exception exception)
#pragma warning restore CA1031
        {
            Log.ErrorNotificationThrew(logger, webhookEventId, exception);
        }
    }

    /// <summary>
    /// 組出錯誤通知的文字,截到 <see cref="LineMessagingLimits.MaxTextLength"/>。
    /// Builds the error notification text, cut to <see cref="LineMessagingLimits.MaxTextLength"/>.
    /// </summary>
    /// <param name="webhookEvent">出錯的事件。The failing event.</param>
    /// <param name="exception">例外。The exception.</param>
    /// <param name="includeStackTrace">是否附上堆疊。Whether to append the stack trace.</param>
    /// <returns>通知文字。The notification text.</returns>
    internal static string BuildErrorNotification(LineWebhookEvent webhookEvent, Exception exception, bool includeStackTrace)
    {
        var text = string.Create(
            CultureInfo.InvariantCulture,
            $"[Ozakboy.Line] webhook 事件處理失敗\n事件:{webhookEvent.Type} {webhookEvent.WebhookEventId}\n來源:{webhookEvent.Source.Type}/{webhookEvent.Source.UserId}\n例外:{exception.GetType().FullName}: {exception.Message}");

        if (includeStackTrace && !string.IsNullOrEmpty(exception.StackTrace))
        {
            text += "\n" + exception.StackTrace;
        }

        const int Max = LineMessagingLimits.MaxTextLength;
        if (text.Length <= Max)
        {
            return text;
        }

        // 不要把一個代理對(surrogate pair)切成兩半。
        // Do not cut a surrogate pair in half.
        var length = char.IsHighSurrogate(text[Max - 1]) ? Max - 1 : Max;
        return text[..length];
    }

    /// <summary>
    /// 掛端點當下讀取並複製的設定快照,之後呼叫端再改選項物件也不影響已掛上的端點。
    /// A snapshot of the settings taken while mapping, so later edits to the options object do not touch an
    /// endpoint that is already mapped.
    /// </summary>
    private sealed class EventPlan
    {
        private readonly HashSet<string> _maintenanceAdmins;

        private EventPlan(
            bool autoReply,
            bool maintenanceEnabled,
            HashSet<string> maintenanceAdmins,
            string maintenanceReplyText,
            bool notificationEnabled,
            IReadOnlyList<string> notificationAdminUserIds,
            bool includeStackTrace)
        {
            AutoReply = autoReply;
            MaintenanceEnabled = maintenanceEnabled;
            _maintenanceAdmins = maintenanceAdmins;
            MaintenanceReplyText = maintenanceReplyText;
            NotificationEnabled = notificationEnabled;
            NotificationAdminUserIds = notificationAdminUserIds;
            IncludeStackTrace = includeStackTrace;
        }

        /// <summary>是否跑自動回覆。Whether auto reply runs.</summary>
        internal bool AutoReply { get; }

        /// <summary>維護模式是否開啟。Whether maintenance mode is on.</summary>
        internal bool MaintenanceEnabled { get; }

        /// <summary>維護中的回覆文字。The maintenance reply text.</summary>
        internal string MaintenanceReplyText { get; }

        /// <summary>錯誤通知是否開啟。Whether error notification is on.</summary>
        internal bool NotificationEnabled { get; }

        /// <summary>要通知的管理員(已去重)。The administrators to notify, deduplicated.</summary>
        internal IReadOnlyList<string> NotificationAdminUserIds { get; }

        /// <summary>通知是否附堆疊。Whether the notification carries the stack trace.</summary>
        internal bool IncludeStackTrace { get; }

        /// <summary>
        /// 事件的來源使用者是不是維護模式的管理員;沒有 <c>UserId</c> 一律不是。
        /// Whether the event's source user is a maintenance administrator; with no <c>UserId</c> it never is.
        /// </summary>
        /// <param name="webhookEvent">事件。The event.</param>
        /// <returns>是管理員時為 <see langword="true"/>。<see langword="true"/> for an administrator.</returns>
        internal bool IsMaintenanceAdmin(LineWebhookEvent webhookEvent) =>
            webhookEvent.Source.UserId is { Length: > 0 } userId && _maintenanceAdmins.Contains(userId);

        /// <summary>
        /// 驗證並複製設定。
        /// Validates and copies the settings.
        /// </summary>
        /// <param name="endpoints">端點路由建構器。The endpoint route builder.</param>
        /// <param name="options">端點設定。The endpoint settings.</param>
        /// <returns>快照。The snapshot.</returns>
        internal static EventPlan Create(IEndpointRouteBuilder endpoints, LineWebhookEndpointOptions options)
        {
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

            var maintenance = options.Maintenance;
            var maintenanceAdmins = CopyIds(maintenance.AdminUserIds);
            if (maintenance.Enabled)
            {
                if (maintenanceAdmins.Count == 0)
                {
                    throw new InvalidOperationException(
                        "開啟了 webhook 端點的維護模式,但 Maintenance.AdminUserIds 是空的 —— 沒有管理員的維護模式會把自己也鎖在外面。Maintenance mode is enabled on the webhook endpoint but Maintenance.AdminUserIds is empty; maintenance without an administrator locks the owner out too.");
                }

                if (string.IsNullOrWhiteSpace(maintenance.ReplyText))
                {
                    throw new InvalidOperationException(
                        "開啟了 webhook 端點的維護模式,但 Maintenance.ReplyText 是空白。Maintenance mode is enabled on the webhook endpoint but Maintenance.ReplyText is blank.");
                }

                RequireService(endpoints, typeof(ILineMessagingClient), nameof(ILineMessagingClient) + "(維護模式要回覆文字;請呼叫 services.AddLineMessaging(...))");
            }

            var notification = options.ErrorNotification;
            var notificationAdmins = CopyIds(notification.AdminUserIds).ToList();
            if (notification.Enabled)
            {
                if (notificationAdmins.Count == 0)
                {
                    throw new InvalidOperationException(
                        "開啟了 webhook 端點的錯誤通知,但 ErrorNotification.AdminUserIds 是空的。Error notification is enabled on the webhook endpoint but ErrorNotification.AdminUserIds is empty.");
                }

                RequireService(endpoints, typeof(ILineMessagingClient), nameof(ILineMessagingClient) + "(錯誤通知要推播;請呼叫 services.AddLineMessaging(...))");
            }

            return new EventPlan(
                options.AutoReply,
                maintenance.Enabled,
                maintenanceAdmins,
                maintenance.ReplyText,
                notification.Enabled,
                notificationAdmins,
                notification.IncludeStackTrace);
        }

        /// <summary>
        /// 複製識別碼清單,去掉空白項目與重複。
        /// Copies a list of IDs, dropping blanks and duplicates.
        /// </summary>
        /// <param name="ids">識別碼。The IDs.</param>
        /// <returns>去重後的集合。The deduplicated set.</returns>
        private static HashSet<string> CopyIds(IEnumerable<string> ids)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                if (!string.IsNullOrWhiteSpace(id))
                {
                    set.Add(id);
                }
            }

            return set;
        }
    }
}
