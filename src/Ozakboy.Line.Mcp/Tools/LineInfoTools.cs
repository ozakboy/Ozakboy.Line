using System.ComponentModel;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Ozakboy.Line.Mcp.Outbox;
using Ozakboy.Line.Messaging;

namespace Ozakboy.Line.Mcp.Tools;

/// <summary>
/// 唯讀的查詢工具:官方帳號資訊、推播額度、好友個人檔案。
/// The read-only tools: the official account's information, the push quota, and a friend's profile.
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
}
