using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Ozakboy.Line.Liff;
using Ozakboy.Line.Mcp.Outbox;

namespace Ozakboy.Line.Mcp.Tools;

/// <summary>
/// LIFF 應用程式的查詢、建立與刪除工具。
/// The tools for listing, creating and deleting LIFF apps.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ILineLiffClient"/> 是<b>可選註冊</b>:宿主可能只呼叫了 <c>AddLineMcp</c> 而沒有 <c>AddLineLiff</c>
/// (LIFF 用的是 LINE Login 頻道的權杖,和 Messaging API 不是同一組憑證)。這三個工具因此照樣掛上,
/// 沒註冊時回 <see cref="LineErrorCodes.NotConfigured"/> 失敗 —— AI 讀得到「有這個功能,只是這個站沒開」。
/// <see cref="ILineLiffClient"/> is an <b>optional registration</b>: a host may have called <c>AddLineMcp</c>
/// without <c>AddLineLiff</c> (LIFF uses the LINE Login channel's token, which is not the Messaging API's
/// credential). The three tools are mounted all the same and answer with a
/// <see cref="LineErrorCodes.NotConfigured"/> failure when it is missing, so the AI reads that the capability
/// exists and this site has not turned it on.
/// </para>
/// <para>
/// 建立與刪除是帳號設定的變更,與 <c>line_replace_rich_menu</c> 一樣<b>走待審佇列</b>:預設的
/// <see cref="LineMcpSendMode.Review"/> 模式下它們什麼都不執行,只把計畫排進待發佇列,由宿主的人核准後才生效
/// (刪除會讓所有指向該 liffId 的連結立刻失效,不能讓 AI 單獨決定)。直接模式先執行、再記錄一筆。
/// 查詢是唯讀的,直接查、不計每日額度。
/// Creating and deleting change the account's configuration and, like <c>line_replace_rich_menu</c>, <b>go through
/// the review queue</b>: in the default <see cref="LineMcpSendMode.Review"/> mode they execute nothing and only queue
/// the plan, taking effect once a person on the host's side approves (a deletion breaks every link that points at
/// the liffId, which an AI must not decide alone). Direct mode executes first and records afterwards. Listing is
/// read-only: it queries directly and does not count against the daily cap.
/// </para>
/// </remarks>
[McpServerToolType]
public sealed class LineLiffTools
{
    private readonly ILineMcpOutboxStore _outbox;
    private readonly IOptions<LineMcpOptions> _options;
    private readonly ILineLiffClient? _liff;
    private readonly ILogger<LineLiffTools>? _logger;

    /// <summary>
    /// 建立工具組。
    /// Creates the tool set.
    /// </summary>
    /// <param name="outbox">待發佇列。The outbox.</param>
    /// <param name="options">MCP 設定。The MCP settings.</param>
    /// <param name="liff">
    /// LIFF 用戶端;宿主沒呼叫 <c>AddLineLiff</c> 時為 <see langword="null"/>。
    /// The LIFF client; <see langword="null"/> when the host did not call <c>AddLineLiff</c>.
    /// </param>
    /// <param name="logger">日誌記錄器,可為 <see langword="null"/>。The logger; may be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="outbox"/> 或 <paramref name="options"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="outbox"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    public LineLiffTools(
        ILineMcpOutboxStore outbox,
        IOptions<LineMcpOptions> options,
        ILineLiffClient? liff = null,
        ILogger<LineLiffTools>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(outbox);
        ArgumentNullException.ThrowIfNull(options);

        _outbox = outbox;
        _options = options;
        _liff = liff;
        _logger = logger;
    }

    /// <summary>
    /// 列出所有 LIFF 應用程式。
    /// Lists every LIFF app.
    /// </summary>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>應用程式清單的 JSON。The apps as JSON.</returns>
    [McpServerTool(Name = "line_list_liff_apps")]
    [Description("列出這個 LINE Login 頻道下所有的 LIFF 應用程式,含 liffId、視窗大小(view.type)、網址(view.url)、說明與機器人提示設定。" +
                 "沒有任何應用程式時回傳空清單(count 為 0),那是正常狀態而非錯誤。" +
                 "宿主沒有啟用 LIFF 時回傳 line.not_configured。唯讀,直接查詢,不經待審佇列也不計每日額度。")]
    public async Task<string> ListLiffAppsAsync(CancellationToken cancellationToken = default)
    {
        if (_liff is null)
        {
            return LineMcpJson.Error(LineMcpLiff.NotEnabled());
        }

        var apps = await _liff.GetAllAsync(cancellationToken).ConfigureAwait(false);

        return apps.IsFailure
            ? LineMcpJson.Error(apps.Error)
            : LineMcpJson.Ok(new
            {
                ok = true,
                count = apps.GetValueOrThrow().Count,
                apps = apps.GetValueOrThrow(),
            });
    }

    /// <summary>
    /// 提出建立一個 LIFF 應用程式。
    /// Proposes creating a LIFF app.
    /// </summary>
    /// <param name="viewType">視窗大小:compact、tall 或 full。The window size: compact, tall or full.</param>
    /// <param name="url">應用程式網址,必須是 https。The app's address, which must be https.</param>
    /// <param name="description">應用程式說明。The app's description.</param>
    /// <param name="botPrompt">加好友提示:none、normal 或 aggressive。The add-friend prompt: none, normal or aggressive.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>結果的 JSON。The result as JSON.</returns>
    [McpServerTool(Name = "line_create_liff_app")]
    [Description("提出建立一個 LIFF 應用程式。" +
                 "viewType 為 compact(約 50%)、tall(約 80%)或 full(全螢幕);url 必須是 https 網址,指向你的網頁;" +
                 "description 是給管理者看的說明;botPrompt(選填)為 none、normal 或 aggressive,決定開啟時是否提示加官方帳號為好友。" +
                 "這是帳號設定的變更:預設為待審模式,本工具只把計畫排進待發佇列,需宿主人工核准後才會真的建立,回傳值的 mode 欄位會說明是哪一種;" +
                 "直接模式才會立即建立並回傳 liffId 與 LIFF 網址(https://liff.line.me/{liffId})。建立後不會有訊息送給任何使用者。" +
                 "參數不合規格時在本地失敗、不會排入佇列;宿主沒有啟用 LIFF 時回傳 line.not_configured。")]
    public async Task<string> CreateLiffAppAsync(
        [Description("視窗大小:compact、tall 或 full")] string viewType,
        [Description("LIFF 應用程式的網址,必須是 https")] string url,
        [Description("應用程式說明")] string description,
        [Description("加好友提示(選填):none、normal 或 aggressive;省略則沿用 LINE 預設")] string? botPrompt = null,
        CancellationToken cancellationToken = default)
    {
        if (_liff is null)
        {
            return LineMcpJson.Error(LineMcpLiff.NotEnabled());
        }

        var parsed = LineMcpLiff.ParseCreate(viewType, url, description, botPrompt);
        if (parsed.IsFailure)
        {
            return LineMcpJson.Error(parsed.Error);
        }

        var app = parsed.GetValueOrThrow();

        return await QueueOrExecuteAsync(
                LineMcpOutboxKind.LiffCreate,
                LineMcpLiff.CreatePayload(app),
                $"建立 LIFF 應用程式({app.View!.Type}):{app.Description},網址 {app.View.Url}",
                async () =>
                {
                    var created = await LineMcpLiff.CreateAsync(_liff, app, _logger, cancellationToken).ConfigureAwait(false);
                    return (created.ToResult(), created.IsSuccess ? created.GetValueOrThrow() : null);
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 提出刪除一個 LIFF 應用程式。
    /// Proposes deleting a LIFF app.
    /// </summary>
    /// <param name="liffId">要刪除的應用程式識別碼。The identifier of the app to delete.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>結果的 JSON。The result as JSON.</returns>
    [McpServerTool(Name = "line_delete_liff_app")]
    [Description("提出刪除一個 LIFF 應用程式。刪除後無法復原,而且所有指向這個 liffId 的連結、圖文選單按鈕與訊息裡的按鈕會立即失效," +
                 "所以提出之前請先用 line_list_liff_apps 確認 liffId 正確、且已沒有地方在使用它。" +
                 "這是帳號設定的變更:預設為待審模式,本工具只把計畫排進待發佇列,需宿主人工核准後才會真的刪除,回傳值的 mode 欄位會說明是哪一種;" +
                 "直接模式才會立即刪除。liffId 不存在時 LINE 會回傳錯誤;宿主沒有啟用 LIFF 時回傳 line.not_configured。")]
    public async Task<string> DeleteLiffAppAsync(
        [Description("要刪除的 LIFF 應用程式識別碼(來自 line_list_liff_apps)")] string liffId,
        CancellationToken cancellationToken = default)
    {
        if (_liff is null)
        {
            return LineMcpJson.Error(LineMcpLiff.NotEnabled());
        }

        if (string.IsNullOrWhiteSpace(liffId))
        {
            return LineMcpJson.Error("liffId 不可為空白。");
        }

        var id = liffId.Trim();

        return await QueueOrExecuteAsync(
                LineMcpOutboxKind.LiffDelete,
                LineMcpLiff.DeletePayload(id),
                $"刪除 LIFF 應用程式 {id}",
                async () =>
                {
                    var deleted = await LineMcpLiff.DeleteAsync(_liff, id, _logger, cancellationToken).ConfigureAwait(false);
                    return (deleted, (string?)null);
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 兩個設定類工具共用的流程:檢查每日上限,再依模式排入待審或直接執行。
    /// The flow the two configuration tools share: check the daily cap, then queue or execute according to the mode.
    /// </summary>
    /// <param name="kind">項目種類。The item's kind.</param>
    /// <param name="payload">待發內容。The payload.</param>
    /// <param name="summary">給審核者看的摘要。The summary for the reviewer.</param>
    /// <param name="execute">直接模式要執行的動作,回傳結果與(建立時的)liffId。What direct mode executes, returning the result and, for a creation, the liffId.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>結果的 JSON。The result as JSON.</returns>
    private async Task<string> QueueOrExecuteAsync(
        LineMcpOutboxKind kind,
        string payload,
        string summary,
        Func<Task<(Ozakboy.Core.Abstractions.Result Result, string? LiffId)>> execute,
        CancellationToken cancellationToken)
    {
        var options = _options.Value;
        var now = DateTimeOffset.UtcNow;

        var (used, hasAllowance) = await LineMcpDailyLimit
            .CheckAsync(_outbox, options, now, cancellationToken)
            .ConfigureAwait(false);

        if (!hasAllowance)
        {
            return LineMcpJson.Error(LineMcpDailyLimit.Message(used, options));
        }

        var item = new LineMcpOutboxItem
        {
            Kind = kind,
            PayloadJson = payload,
            Summary = summary,
            Status = LineMcpOutboxStatus.PendingReview,
            CreatedAt = now,
        };

        if (options.SendMode == LineMcpSendMode.Review)
        {
            var queued = await _outbox.AddAsync(item, cancellationToken).ConfigureAwait(false);

            return LineMcpJson.Ok(new
            {
                ok = true,
                mode = "review",
                outboxId = queued.Id,
                summary = queued.Summary,
                createdToday = used + 1,
                dailyLimit = options.MaxSendRequestsPerDay,
                notice = options.ReviewNotice,
            });
        }

        // 直接模式:先執行再記。反過來(先記 Sent 再執行)的話,失敗時佇列裡會留下一筆
        // 說「已完成」的假紀錄,而稽核紀錄說謊比沒有稽核紀錄更糟。
        // Direct mode executes first and records afterwards. The other order would leave a row claiming the
        // change happened when it did not, and an audit trail that lies is worse than none.
        var (executed, liffId) = await execute().ConfigureAwait(false);

        item.DecidedAt = now;
        item.SentAt = executed.IsSuccess ? now : null;
        item.Status = executed.IsSuccess ? LineMcpOutboxStatus.Sent : LineMcpOutboxStatus.Failed;
        item.Error = executed.IsSuccess ? null : executed.Error.Message;

        var recorded = await _outbox.AddAsync(item, cancellationToken).ConfigureAwait(false);

        if (executed.IsFailure)
        {
            return LineMcpJson.Error(executed.Error);
        }

        return LineMcpJson.Ok(new
        {
            ok = true,
            mode = "direct",
            outboxId = recorded.Id,
            summary = recorded.Summary,
            liffId,
            liffUrl = liffId is null ? null : $"https://liff.line.me/{liffId}",
            createdToday = used + 1,
            dailyLimit = options.MaxSendRequestsPerDay,
        });
    }
}
