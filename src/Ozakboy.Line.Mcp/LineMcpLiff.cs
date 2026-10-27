using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Liff;

namespace Ozakboy.Line.Mcp;

/// <summary>
/// LIFF 工具與待發項目核准共用的邏輯:讀參數、組內容、執行並寫日誌。
/// The logic the LIFF tools and the outbox approval share: reading arguments, building the payload, executing,
/// and logging.
/// </summary>
/// <remarks>
/// 提出(工具)與執行(直接模式的工具、或核准時的服務)是同一份實作 —— 兩份實作遲早會在其中一條路上
/// 漏掉一個檢查,而待審的項目被核准時才發現參數不對,就是審核白做了。
/// Proposing (the tool) and executing (the tool in direct mode, or the service on approval) share one
/// implementation: two of them eventually drop a check on one path, and an item found to carry bad arguments only
/// at approval means the review was wasted.
/// </remarks>
internal static partial class LineMcpLiff
{
    /// <summary>
    /// 讀取並檢查建立 LIFF 應用程式的參數。
    /// Reads and checks the arguments for creating a LIFF app.
    /// </summary>
    /// <param name="viewType">視窗大小。The window size.</param>
    /// <param name="url">網址。The address.</param>
    /// <param name="description">說明。The description.</param>
    /// <param name="botPrompt">加好友提示。The add-friend prompt.</param>
    /// <returns>應用程式定義;參數不合規格時為 <see cref="LineErrorCodes.InvalidLiffApp"/> 失敗。The app definition, or an <see cref="LineErrorCodes.InvalidLiffApp"/> failure for bad arguments.</returns>
    internal static Result<LineLiffApp> ParseCreate(string? viewType, string? url, string? description, string? botPrompt)
    {
        if (!TryParseViewType(viewType, out var parsedViewType))
        {
            return Invalid("viewType 必須是 compact、tall 或 full 其中之一。");
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            return Invalid("url 不可為空白,且必須是 https 網址。");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return Invalid("description 不可為空白:沒有說明的應用程式,日後沒有人知道它是做什麼的。");
        }

        string? prompt = null;
        if (!string.IsNullOrWhiteSpace(botPrompt))
        {
            prompt = botPrompt.Trim().ToLowerInvariant();
            if (prompt is not ("none" or "normal" or "aggressive"))
            {
                return Invalid("botPrompt 必須是 none、normal 或 aggressive 其中之一。");
            }
        }

        return Result.Success(new LineLiffApp
        {
            View = new LineLiffView { Type = parsedViewType, Url = url.Trim() },
            Description = description.Trim(),
            BotPrompt = prompt,
        });
    }

    /// <summary>
    /// 把建立參數寫成待發內容。
    /// Writes the create arguments as an outbox payload.
    /// </summary>
    /// <param name="app">已檢查過的應用程式定義。The checked app definition.</param>
    /// <returns>內容 JSON。The payload JSON.</returns>
    internal static string CreatePayload(LineLiffApp app)
    {
        ArgumentNullException.ThrowIfNull(app);

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("viewType", app.View!.Type!.Value.ToString().ToLowerInvariant());
            writer.WriteString("url", app.View.Url);
            writer.WriteString("description", app.Description);
            if (app.BotPrompt is not null)
            {
                writer.WriteString("botPrompt", app.BotPrompt);
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>
    /// 把刪除參數寫成待發內容。
    /// Writes the delete arguments as an outbox payload.
    /// </summary>
    /// <param name="liffId">識別碼。The identifier.</param>
    /// <returns>內容 JSON。The payload JSON.</returns>
    internal static string DeletePayload(string liffId) =>
        JsonSerializer.Serialize(new { liffId });

    /// <summary>
    /// 沒有 <see cref="ILineLiffClient"/> 時的失敗。
    /// The failure when there is no <see cref="ILineLiffClient"/>.
    /// </summary>
    /// <returns>失敗。The failure.</returns>
    internal static Error NotEnabled() =>
        Error.Validation(
            LineErrorCodes.NotConfigured,
            "這個站台沒有啟用 LIFF(宿主未呼叫 AddLineLiff)。LIFF 使用 LINE Login 頻道的權杖,需由宿主設定後才能使用。");

    /// <summary>
    /// 執行建立並寫日誌。
    /// Executes a create and logs it.
    /// </summary>
    /// <param name="liff">用戶端;<see langword="null"/> 時失敗。The client; a failure when <see langword="null"/>.</param>
    /// <param name="app">應用程式定義。The app definition.</param>
    /// <param name="logger">日誌記錄器。The logger.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>新應用程式的識別碼。The new app's identifier.</returns>
    internal static async Task<Result<string>> CreateAsync(
        ILineLiffClient? liff,
        LineLiffApp app,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        if (liff is null)
        {
            return Result.Failure<string>(NotEnabled());
        }

        var added = await liff.AddAsync(app, cancellationToken).ConfigureAwait(false);
        if (added.IsSuccess && logger is not null)
        {
            var liffId = added.GetValueOrThrow();
            var address = app.View!.Url!;
            Log.LiffAppCreated(logger, liffId, address);
        }

        return added;
    }

    /// <summary>
    /// 執行刪除並寫日誌。
    /// Executes a delete and logs it.
    /// </summary>
    /// <param name="liff">用戶端;<see langword="null"/> 時失敗。The client; a failure when <see langword="null"/>.</param>
    /// <param name="liffId">識別碼。The identifier.</param>
    /// <param name="logger">日誌記錄器。The logger.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>成功或失敗。Success or failure.</returns>
    internal static async Task<Result> DeleteAsync(
        ILineLiffClient? liff,
        string liffId,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        if (liff is null)
        {
            return Result.Failure(NotEnabled());
        }

        var deleted = await liff.DeleteAsync(liffId, cancellationToken).ConfigureAwait(false);
        if (deleted.IsSuccess && logger is not null)
        {
            Log.LiffAppDeleted(logger, liffId);
        }

        return deleted;
    }

    private static Error Invalid(string message) => Error.Validation(LineErrorCodes.InvalidLiffApp, message);

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
            Message = "MCP 建立了 LIFF 應用程式 {LiffId},網址 {Url}。An MCP-originated change created LIFF app {LiffId} pointing at {Url}.")]
        internal static partial void LiffAppCreated(ILogger logger, string liffId, string url);

        [LoggerMessage(
            EventId = 5102,
            Level = LogLevel.Warning,
            Message = "MCP 刪除了 LIFF 應用程式 {LiffId}。An MCP-originated change deleted LIFF app {LiffId}.")]
        internal static partial void LiffAppDeleted(ILogger logger, string liffId);
    }
}
