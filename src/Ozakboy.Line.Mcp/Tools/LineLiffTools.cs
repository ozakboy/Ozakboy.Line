using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Liff;

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
/// 建立與刪除是<b>帳號設定的變更,不是訊息送出</b>:不進待發佇列、不計每日額度,每一次都寫一行日誌備查。
/// Creating and deleting change the account's configuration rather than send a message: they do not enter the
/// outbox and do not count against the daily cap, and each one writes a log line for the record.
/// </para>
/// </remarks>
[McpServerToolType]
public sealed partial class LineLiffTools
{
    private readonly ILineLiffClient? _liff;
    private readonly ILogger<LineLiffTools>? _logger;

    /// <summary>
    /// 建立工具組。
    /// Creates the tool set.
    /// </summary>
    /// <param name="liff">
    /// LIFF 用戶端;宿主沒呼叫 <c>AddLineLiff</c> 時為 <see langword="null"/>。
    /// The LIFF client; <see langword="null"/> when the host did not call <c>AddLineLiff</c>.
    /// </param>
    /// <param name="logger">日誌記錄器,可為 <see langword="null"/>。The logger; may be <see langword="null"/>.</param>
    public LineLiffTools(ILineLiffClient? liff = null, ILogger<LineLiffTools>? logger = null)
    {
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
                 "宿主沒有啟用 LIFF 時回傳 line.not_configured。唯讀,不計每日額度。")]
    public async Task<string> ListLiffAppsAsync(CancellationToken cancellationToken = default)
    {
        if (_liff is null)
        {
            return NotEnabled();
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
    /// 建立一個 LIFF 應用程式。
    /// Creates a LIFF app.
    /// </summary>
    /// <param name="viewType">視窗大小:compact、tall 或 full。The window size: compact, tall or full.</param>
    /// <param name="url">應用程式網址,必須是 https。The app's address, which must be https.</param>
    /// <param name="description">應用程式說明。The app's description.</param>
    /// <param name="botPrompt">加好友提示:none、normal 或 aggressive。The add-friend prompt: none, normal or aggressive.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>新應用程式識別碼的 JSON。The new app's identifier as JSON.</returns>
    [McpServerTool(Name = "line_create_liff_app")]
    [Description("建立一個 LIFF 應用程式,成功時回傳 liffId 與 LIFF 網址(https://liff.line.me/{liffId})。" +
                 "viewType 為 compact(約 50%)、tall(約 80%)或 full(全螢幕);url 必須是 https 網址,指向你的網頁;" +
                 "description 是給管理者看的說明;botPrompt(選填)為 none、normal 或 aggressive,決定開啟時是否提示加官方帳號為好友。" +
                 "這是帳號設定的變更而不是訊息送出,不經待審佇列,會立即生效並寫入日誌;建立後不會有訊息送給任何使用者。" +
                 "網址或視窗大小不合規格時在本地失敗、不會送出;宿主沒有啟用 LIFF 時回傳 line.not_configured。")]
    public async Task<string> CreateLiffAppAsync(
        [Description("視窗大小:compact、tall 或 full")] string viewType,
        [Description("LIFF 應用程式的網址,必須是 https")] string url,
        [Description("應用程式說明")] string description,
        [Description("加好友提示(選填):none、normal 或 aggressive;省略則沿用 LINE 預設")] string? botPrompt = null,
        CancellationToken cancellationToken = default)
    {
        if (_liff is null)
        {
            return NotEnabled();
        }

        if (!TryParseViewType(viewType, out var parsedViewType))
        {
            return LineMcpJson.Error("viewType 必須是 compact、tall 或 full 其中之一。");
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            return LineMcpJson.Error("url 不可為空白,且必須是 https 網址。");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return LineMcpJson.Error("description 不可為空白:沒有說明的應用程式,日後沒有人知道它是做什麼的。");
        }

        string? prompt = null;
        if (!string.IsNullOrWhiteSpace(botPrompt))
        {
            prompt = botPrompt.Trim().ToLowerInvariant();
            if (prompt is not ("none" or "normal" or "aggressive"))
            {
                return LineMcpJson.Error("botPrompt 必須是 none、normal 或 aggressive 其中之一。");
            }
        }

        var address = url.Trim();
        var app = new LineLiffApp
        {
            View = new LineLiffView { Type = parsedViewType, Url = address },
            Description = description.Trim(),
            BotPrompt = prompt,
        };

        var added = await _liff.AddAsync(app, cancellationToken).ConfigureAwait(false);
        if (added.IsFailure)
        {
            return LineMcpJson.Error(added.Error);
        }

        var liffId = added.GetValueOrThrow();
        if (_logger is not null)
        {
            Log.LiffAppCreated(_logger, liffId, address);
        }

        return LineMcpJson.Ok(new
        {
            ok = true,
            liffId,
            liffUrl = $"https://liff.line.me/{liffId}",
        });
    }

    /// <summary>
    /// 刪除一個 LIFF 應用程式。
    /// Deletes a LIFF app.
    /// </summary>
    /// <param name="liffId">要刪除的應用程式識別碼。The identifier of the app to delete.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>結果的 JSON。The result as JSON.</returns>
    [McpServerTool(Name = "line_delete_liff_app")]
    [Description("刪除一個 LIFF 應用程式。刪除後無法復原,而且所有指向這個 liffId 的連結、圖文選單按鈕與訊息裡的按鈕會立即失效," +
                 "所以刪之前請先用 line_list_liff_apps 確認 liffId 正確、且已沒有地方在使用它。" +
                 "這是帳號設定的變更而不是訊息送出,不經待審佇列,會立即生效並寫入日誌。" +
                 "liffId 不存在時 LINE 會回傳錯誤;宿主沒有啟用 LIFF 時回傳 line.not_configured。")]
    public async Task<string> DeleteLiffAppAsync(
        [Description("要刪除的 LIFF 應用程式識別碼(來自 line_list_liff_apps)")] string liffId,
        CancellationToken cancellationToken = default)
    {
        if (_liff is null)
        {
            return NotEnabled();
        }

        if (string.IsNullOrWhiteSpace(liffId))
        {
            return LineMcpJson.Error("liffId 不可為空白。");
        }

        var id = liffId.Trim();
        var deleted = await _liff.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        if (deleted.IsFailure)
        {
            return LineMcpJson.Error(deleted.Error);
        }

        if (_logger is not null)
        {
            Log.LiffAppDeleted(_logger, id);
        }

        return LineMcpJson.Ok(new { ok = true, deleted = id });
    }

    /// <summary>
    /// 沒有註冊 <see cref="ILineLiffClient"/> 時的回應。
    /// The response when no <see cref="ILineLiffClient"/> is registered.
    /// </summary>
    /// <returns>失敗的 JSON。The failure as JSON.</returns>
    private static string NotEnabled() =>
        LineMcpJson.Error(Error.Validation(
            LineErrorCodes.NotConfigured,
            "這個站台沒有啟用 LIFF(宿主未呼叫 AddLineLiff)。LIFF 使用 LINE Login 頻道的權杖,需由宿主設定後才能使用。"));

    /// <summary>
    /// 讀取視窗大小,不分大小寫。
    /// Reads the window size, case-insensitively.
    /// </summary>
    /// <param name="value">字串。The string.</param>
    /// <param name="viewType">解析出的值。The parsed value.</param>
    /// <returns>讀得出來時為 <see langword="true"/>。<see langword="true"/> when it reads.</returns>
    private static bool TryParseViewType(string? value, out LineLiffViewType viewType)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "compact":
                viewType = LineLiffViewType.Compact;
                return true;
            case "tall":
                viewType = LineLiffViewType.Tall;
                return true;
            case "full":
                viewType = LineLiffViewType.Full;
                return true;
            default:
                viewType = default;
                return false;
        }
    }

    /// <summary>
    /// 日誌訊息的定義。
    /// The log message definitions.
    /// </summary>
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 5101,
            Level = LogLevel.Information,
            Message = "MCP 建立了 LIFF 應用程式 {LiffId},網址 {Url}。An MCP tool created LIFF app {LiffId} pointing at {Url}.")]
        internal static partial void LiffAppCreated(ILogger logger, string liffId, string url);

        [LoggerMessage(
            EventId = 5102,
            Level = LogLevel.Warning,
            Message = "MCP 刪除了 LIFF 應用程式 {LiffId}。An MCP tool deleted LIFF app {LiffId}.")]
        internal static partial void LiffAppDeleted(ILogger logger, string liffId);
    }
}
