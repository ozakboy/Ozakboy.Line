using System.ComponentModel;
using System.Globalization;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Ozakboy.Line.Mcp.Outbox;
using Ozakboy.Line.Messaging;
using Ozakboy.Line.Messaging.Insight;

namespace Ozakboy.Line.Mcp.Tools;

/// <summary>
/// 唯讀的查詢工具:官方帳號資訊、推播額度、好友個人檔案、webhook 狀態與投遞統計。
/// The read-only tools: the official account's information, the push quota, a friend's profile, the webhook's
/// state, and delivery statistics.
/// </summary>
/// <remarks>
/// 這一組工具<b>不會改變任何東西</b>,也不消耗待發佇列的每日額度。
/// Nothing in this group <b>changes anything</b>, and none of it counts against the outbox's daily cap.
/// </remarks>
[McpServerToolType]
public sealed class LineInfoTools
{
    private readonly ILineMessagingClient _messaging;
    private readonly ILineMcpOutboxStore _outbox;
    private readonly IOptions<LineMcpOptions> _options;

    /// <summary>
    /// 建立工具組。
    /// Creates the tool set.
    /// </summary>
    /// <param name="messaging">Messaging API 用戶端。The Messaging API client.</param>
    /// <param name="outbox">待發佇列。The outbox.</param>
    /// <param name="options">MCP 設定。The MCP settings.</param>
    /// <exception cref="ArgumentNullException">
    /// 任一參數為 <see langword="null"/> 時擲出。Thrown when any argument is <see langword="null"/>.
    /// </exception>
    public LineInfoTools(ILineMessagingClient messaging, ILineMcpOutboxStore outbox, IOptions<LineMcpOptions> options)
    {
        ArgumentNullException.ThrowIfNull(messaging);
        ArgumentNullException.ThrowIfNull(outbox);
        ArgumentNullException.ThrowIfNull(options);

        _messaging = messaging;
        _outbox = outbox;
        _options = options;
    }

    /// <summary>
    /// 查詢官方帳號自身的資訊。
    /// Reads the official account's own information.
    /// </summary>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>帳號資訊的 JSON。The account information as JSON.</returns>
    [McpServerTool(Name = "line_get_bot_info")]
    [Description("查詢這個 LINE 官方帳號自身的資訊:顯示名稱、大頭貼、基本 ID、聊天室模式(chat / bot)與 markAsReadMode。用來確認目前操作的是哪一個帳號。")]
    public async Task<string> GetBotInfoAsync(CancellationToken cancellationToken = default)
    {
        var info = await _messaging.GetBotInfoAsync(cancellationToken).ConfigureAwait(false);

        return info.IsFailure
            ? LineMcpJson.Error(info.Error)
            : LineMcpJson.Ok(new
            {
                ok = true,
                botInfo = info.GetValueOrThrow(),
            });
    }

    /// <summary>
    /// 查詢推播額度、本月已用量,以及待發佇列今日的建立數。
    /// Reads the push quota, this month's usage, and how many outbox items were created today.
    /// </summary>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>額度資訊的 JSON。The quota information as JSON.</returns>
    /// <remarks>
    /// 兩種額度放在同一個工具裡回:LINE 的月額度(發出去要花的)與本地的每日建立上限(能提幾筆)。
    /// 分成兩個工具的話,AI 通常只會問其中一個,然後對另一個的限制毫無概念。
    /// Both allowances come back from one tool: LINE's monthly quota, which sending spends, and the local daily
    /// creation cap, which limits how much can be proposed. As two tools, an AI generally asks one of them and
    /// has no idea about the other.
    /// </remarks>
    [McpServerTool(Name = "line_get_quota")]
    [Description("查詢推播額度狀況:LINE 本月的推播額度與已使用則數,以及本地待發佇列今日已建立的筆數與每日上限。發送前先查這個,可以避免提出一個額度不夠的計畫。")]
    public async Task<string> GetQuotaAsync(CancellationToken cancellationToken = default)
    {
        var quota = await _messaging.GetQuotaAsync(cancellationToken).ConfigureAwait(false);
        if (quota.IsFailure)
        {
            return LineMcpJson.Error(quota.Error);
        }

        var consumption = await _messaging.GetQuotaConsumptionAsync(cancellationToken).ConfigureAwait(false);
        var options = _options.Value;
        var (used, _) = await LineMcpDailyLimit
            .CheckAsync(_outbox, options, DateTimeOffset.UtcNow, cancellationToken)
            .ConfigureAwait(false);

        return LineMcpJson.Ok(new
        {
            ok = true,
            quota = quota.GetValueOrThrow(),
            totalUsage = consumption.IsSuccess ? consumption.GetValueOrThrow() : (long?)null,
            outboxCreatedToday = used,
            outboxDailyLimit = options.MaxSendRequestsPerDay,
            timeZone = options.TimeZoneId,
            sendMode = options.SendMode.ToString(),
        });
    }

    /// <summary>
    /// 查詢一位好友的個人檔案。
    /// Reads one friend's profile.
    /// </summary>
    /// <param name="userId">使用者識別碼。The user identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>個人檔案的 JSON。The profile as JSON.</returns>
    [McpServerTool(Name = "line_get_profile")]
    [Description("查詢一位好友的個人檔案(顯示名稱、大頭貼、狀態訊息)。userId 需為 LINE 的使用者識別碼(U 開頭)。對方尚未加好友或已封鎖時會查不到,那是正常情況而非設定錯誤。")]
    public async Task<string> GetProfileAsync(
        [Description("LINE 使用者識別碼,U 開頭")] string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return LineMcpJson.Error("userId 不可為空白。");
        }

        var profile = await _messaging.GetProfileAsync(userId, cancellationToken).ConfigureAwait(false);

        return profile.IsFailure
            ? LineMcpJson.Error(profile.Error)
            : LineMcpJson.Ok(new
            {
                ok = true,
                profile = profile.GetValueOrThrow(),
            });
    }

    /// <summary>
    /// 查詢目前設定的 webhook 位址與是否啟用。
    /// Reads the configured webhook endpoint and whether it is active.
    /// </summary>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>webhook 資訊的 JSON。The webhook information as JSON.</returns>
    [McpServerTool(Name = "line_get_webhook_info")]
    [Description("查詢這個官方帳號目前在 LINE 後台設定的 webhook 位址(endpoint)以及是否啟用(active)。" +
                 "收不到使用者訊息時先查這個:位址不對或被停用都會讓 webhook 完全沒有動靜。唯讀,不會改變任何設定。")]
    public async Task<string> GetWebhookInfoAsync(CancellationToken cancellationToken = default)
    {
        var info = await _messaging.GetWebhookEndpointAsync(cancellationToken).ConfigureAwait(false);

        return info.IsFailure
            ? LineMcpJson.Error(info.Error)
            : LineMcpJson.Ok(new
            {
                ok = true,
                webhook = info.GetValueOrThrow(),
            });
    }

    /// <summary>
    /// 請 LINE 對 webhook 位址送一個驗證請求。
    /// Asks LINE to send a verification request to the webhook endpoint.
    /// </summary>
    /// <param name="endpoint">要測試的位址;省略則測目前設定的位址。The endpoint to test; the configured one when omitted.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>測試結果的 JSON。The test result as JSON.</returns>
    [McpServerTool(Name = "line_test_webhook")]
    [Description("請 LINE 對 webhook 位址送出一個驗證請求,回報對方伺服器的回應(success、statusCode、reason、detail)。" +
                 "endpoint 省略時測試目前設定的位址;提供時必須是 https 且不超過 500 字元,否則在本地就失敗、不會送出。" +
                 "注意:對方伺服器沒有回 2xx 時本工具仍回 ok=true,要看 webhook.success 欄位判斷驗證有沒有通過。" +
                 "這只是測試,不會改變 webhook 設定,也不會送訊息給任何使用者。")]
    public async Task<string> TestWebhookAsync(
        [Description("要測試的 webhook 位址(https);省略則測目前設定的位址")] string? endpoint = null,
        CancellationToken cancellationToken = default)
    {
        var tested = await _messaging
            .TestWebhookEndpointAsync(string.IsNullOrWhiteSpace(endpoint) ? null : endpoint, cancellationToken)
            .ConfigureAwait(false);

        return tested.IsFailure
            ? LineMcpJson.Error(tested.Error)
            : LineMcpJson.Ok(new
            {
                ok = true,
                webhook = tested.GetValueOrThrow(),
            });
    }

    /// <summary>
    /// 查詢某一天四種管道各送達多少則訊息。
    /// Reads how many messages each of the four channels delivered on one day.
    /// </summary>
    /// <param name="date">日期,<c>yyyy-MM-dd</c>。The date, <c>yyyy-MM-dd</c>.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>四種管道投遞數的 JSON。The four channels' delivery counts as JSON.</returns>
    /// <remarks>
    /// 逐一呼叫 <see cref="ILineMessagingClient.GetMessageDeliveryCountAsync"/>。統計尚未備妥(unready)時
    /// 如實回報那一種的狀態,<c>success</c> 為 <see langword="null"/> —— 那不是零,把它當零會讓報表少算。
    /// Calls <see cref="ILineMessagingClient.GetMessageDeliveryCountAsync"/> once per channel. When the figures are
    /// not ready yet the channel's status is reported as it is, with <c>success</c> <see langword="null"/> — which
    /// is not zero, and reading it as zero makes a report undercount.
    /// </remarks>
    [McpServerTool(Name = "line_get_delivery_stats")]
    [Description("查詢某一天這個官方帳號各管道送達的訊息數,一次回傳四種:reply(回覆)、push(推播)、multicast(群發)、broadcast(廣播)。" +
                 "date 格式為 yyyy-MM-dd;LINE 以日本標準時間(JST,UTC+9)計日,所以台灣時間凌晨 0 到 1 點算的是前一天。" +
                 "每一種都有 status:ready 時 success 才是實際數字;unready(統計尚未備妥,通常要等隔天)時 success 為 null,那不是 0。" +
                 "日期格式錯誤時回傳錯誤;四種全部查詢失敗時回傳第一個錯誤,部分失敗時失敗的那幾種在 error 欄位說明。唯讀,不計每日額度。")]
    public async Task<string> GetDeliveryStatsAsync(
        [Description("要查詢的日期,格式 yyyy-MM-dd(以 JST 計日)")] string date,
        CancellationToken cancellationToken = default)
    {
        if (!DateOnly.TryParseExact(date?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            return LineMcpJson.Error("date 必須是 yyyy-MM-dd 格式的日期,例如 2026-10-27。");
        }

        var deliveries = new List<object>();
        Ozakboy.Core.Abstractions.Error? firstError = null;
        var failures = 0;

        foreach (var type in new[]
        {
            LineMessageDeliveryType.Reply,
            LineMessageDeliveryType.Push,
            LineMessageDeliveryType.Multicast,
            LineMessageDeliveryType.Broadcast,
        })
        {
            var name = type.ToString().ToLowerInvariant();
            var count = await _messaging.GetMessageDeliveryCountAsync(type, day, cancellationToken).ConfigureAwait(false);

            if (count.IsFailure)
            {
                failures++;
                firstError ??= count.Error;
                deliveries.Add(new { type = name, ok = false, error = count.Error.Message, code = count.Error.Code });
                continue;
            }

            var value = count.GetValueOrThrow();
            deliveries.Add(new { type = name, ok = true, status = value.Status, success = value.Success });
        }

        if (failures == deliveries.Count && firstError is not null)
        {
            return LineMcpJson.Error(firstError);
        }

        return LineMcpJson.Ok(new
        {
            ok = true,
            date = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            dayBoundary = "JST (UTC+9)",
            deliveries,
        });
    }

    /// <summary>
    /// 查詢某一次發送的互動統計。
    /// Reads the engagement statistics of one send.
    /// </summary>
    /// <param name="requestId">發送時取得的請求識別碼。The request id obtained at send time.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>互動統計的 JSON。The statistics as JSON.</returns>
    [McpServerTool(Name = "line_get_message_event_insight")]
    [Description("查詢某一次發送的互動統計:送達數、開啟數、點擊數(含各連結的點擊)等。" +
                 "requestId 是發送當時 LINE 回的請求識別碼(X-Line-Request-Id),不是待發佇列的 outboxId。" +
                 "統計尚未備妥或已過保存期限時 LINE 會回傳錯誤或空資料;欄位為 null 代表該指標不適用,不是 0。唯讀,不計每日額度。")]
    public async Task<string> GetMessageEventInsightAsync(
        [Description("發送時 LINE 回的請求識別碼(X-Line-Request-Id)")] string requestId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            return LineMcpJson.Error("requestId 不可為空白。");
        }

        var insight = await _messaging.GetMessageEventInsightAsync(requestId.Trim(), cancellationToken).ConfigureAwait(false);

        return insight.IsFailure
            ? LineMcpJson.Error(insight.Error)
            : LineMcpJson.Ok(new
            {
                ok = true,
                insight = insight.GetValueOrThrow(),
            });
    }
}
