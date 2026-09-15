using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Mcp.Outbox;
using Ozakboy.Line.Messaging;

namespace Ozakboy.Line.Mcp.Tools;

/// <summary>
/// 送出類工具:推播、群發、廣播。
/// The sending tools: push, multicast, and broadcast.
/// </summary>
/// <remarks>
/// <para>
/// 預設的 <see cref="LineMcpSendMode.Review"/> 模式下,這三個工具<b>一則訊息都不會送出</b> ——
/// 它們把內容寫進待發佇列,等宿主的人核准。核准的 API 不在這個類別裡,也不是任何一個工具。
/// In the default <see cref="LineMcpSendMode.Review"/> mode these three tools <b>send nothing at all</b>: they
/// write the payload to the outbox for a person on the host's side to approve. The approval API is not in this
/// class, and is not a tool.
/// </para>
/// <para>
/// 兩種模式都會寫進佇列。稽核紀錄不是待審制度的副產品,而是它自己就該有的東西。
/// Both modes write to the queue. The audit trail is not a by-product of the review step; it is worth having on
/// its own.
/// </para>
/// </remarks>
[McpServerToolType]
public sealed class LineSendTools
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
    public LineSendTools(ILineMessagingClient messaging, ILineMcpOutboxStore outbox, IOptions<LineMcpOptions> options)
    {
        ArgumentNullException.ThrowIfNull(messaging);
        ArgumentNullException.ThrowIfNull(outbox);
        ArgumentNullException.ThrowIfNull(options);

        _messaging = messaging;
        _outbox = outbox;
        _options = options;
    }

    /// <summary>
    /// 推播給單一對象。
    /// Pushes to one recipient.
    /// </summary>
    /// <param name="to">使用者、群組或聊天室識別碼。A user, group, or room identifier.</param>
    /// <param name="text">純文字內容。The plain text.</param>
    /// <param name="messagesJson">訊息陣列 JSON。The message array's JSON.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>結果的 JSON。The result as JSON.</returns>
    [McpServerTool(Name = "line_send_push")]
    [Description("對單一對象推播訊息。to 為使用者、群組或聊天室識別碼。text 與 messagesJson 需恰好擇一:" +
                 "text 是純文字,會自動包成一則 LINE 文字訊息;messagesJson 是完整的 LINE 訊息陣列(1~5 則)。" +
                 "預設為待審模式:本工具只把內容排進待發佇列,需宿主人工核准後才會真的送出,回傳值的 mode 欄位會說明是哪一種。")]
    public Task<string> SendPushAsync(
        [Description("收件對象識別碼(使用者 U…、群組 C…、聊天室 R…)")] string to,
        [Description("純文字內容(與 messagesJson 擇一)")] string? text = null,
        [Description("LINE 訊息陣列 JSON,1~5 則(與 text 擇一)")] string? messagesJson = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(to))
        {
            return Task.FromResult(LineMcpJson.Error("to 不可為空白。"));
        }

        return QueueOrSendAsync(
            LineMcpOutboxKind.Push,
            text,
            messagesJson,
            writer => writer.WriteString("to", to),
            async body =>
            {
                var pushed = await _messaging.PushRawJsonAsync(to, body, options: null, cancellationToken).ConfigureAwait(false);
                return pushed.ToResult();
            },
            cancellationToken);
    }

    /// <summary>
    /// 推播給多位使用者。
    /// Pushes to several users.
    /// </summary>
    /// <param name="userIds">使用者識別碼。The user identifiers.</param>
    /// <param name="text">純文字內容。The plain text.</param>
    /// <param name="messagesJson">訊息陣列 JSON。The message array's JSON.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>結果的 JSON。The result as JSON.</returns>
    [McpServerTool(Name = "line_send_multicast")]
    [Description("對多位使用者推播同一份訊息,單次上限 500 人,且只接受使用者識別碼(不能放群組或聊天室)。" +
                 "text 與 messagesJson 需恰好擇一。預設為待審模式,本工具只排進待發佇列,需宿主人工核准後才會送出。" +
                 "注意:群發是逐人計算推播額度的,500 人就是 500 則。")]
    public Task<string> SendMulticastAsync(
        [Description("使用者識別碼陣列,1~500 個")] string[] userIds,
        [Description("純文字內容(與 messagesJson 擇一)")] string? text = null,
        [Description("LINE 訊息陣列 JSON,1~5 則(與 text 擇一)")] string? messagesJson = null,
        CancellationToken cancellationToken = default)
    {
        if (userIds is null || userIds.Length == 0)
        {
            return Task.FromResult(LineMcpJson.Error("userIds 至少需要一位使用者。"));
        }

        if (userIds.Length > LineMessagingLimits.MulticastRecipients)
        {
            return Task.FromResult(LineMcpJson.Error(
                $"群發單次上限 {LineMessagingLimits.MulticastRecipients} 人,這次是 {userIds.Length} 人。請分批送出。"));
        }

        return QueueOrSendAsync(
            LineMcpOutboxKind.Multicast,
            text,
            messagesJson,
            writer =>
            {
                writer.WriteStartArray("userIds");
                foreach (var userId in userIds)
                {
                    writer.WriteStringValue(userId);
                }

                writer.WriteEndArray();
            },
            async body =>
            {
                var messages = LineMcpMessages.Parse(body);
                return messages.IsFailure
                    ? messages.ToResult()
                    : await _messaging.MulticastAsync(userIds, messages.GetValueOrThrow(), options: null, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
    }

    /// <summary>
    /// 廣播給所有好友。
    /// Broadcasts to every friend.
    /// </summary>
    /// <param name="text">純文字內容。The plain text.</param>
    /// <param name="messagesJson">訊息陣列 JSON。The message array's JSON.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>結果的 JSON。The result as JSON.</returns>
    [McpServerTool(Name = "line_send_broadcast")]
    [Description("對這個官方帳號的所有好友廣播訊息。text 與 messagesJson 需恰好擇一。" +
                 "預設為待審模式,本工具只排進待發佇列,需宿主人工核准後才會送出。" +
                 "廣播送出後無法收回,而且每位好友各算一則推播額度,提出之前請先用 line_get_quota 確認額度。")]
    public Task<string> SendBroadcastAsync(
        [Description("純文字內容(與 messagesJson 擇一)")] string? text = null,
        [Description("LINE 訊息陣列 JSON,1~5 則(與 text 擇一)")] string? messagesJson = null,
        CancellationToken cancellationToken = default) =>
        QueueOrSendAsync(
            LineMcpOutboxKind.Broadcast,
            text,
            messagesJson,
            _ => { },
            body => _messaging.BroadcastRawJsonAsync(body, options: null, cancellationToken),
            cancellationToken);

    /// <summary>
    /// 三個送出工具共用的流程:驗內容、檢查每日上限、依模式決定排入待審或直接送出。
    /// The flow the three sending tools share: validate the body, check the daily cap, and either queue or send
    /// according to the mode.
    /// </summary>
    /// <param name="kind">項目種類。The item's kind.</param>
    /// <param name="text">純文字內容。The plain text.</param>
    /// <param name="messagesJson">訊息陣列 JSON。The message array's JSON.</param>
    /// <param name="writeTarget">寫出收件對象欄位的委派。The delegate writing the recipient fields.</param>
    /// <param name="send">直接送出時要呼叫的動作。What to call when sending directly.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>結果的 JSON。The result as JSON.</returns>
    private async Task<string> QueueOrSendAsync(
        LineMcpOutboxKind kind,
        string? text,
        string? messagesJson,
        Action<Utf8JsonWriter> writeTarget,
        Func<string, Task<Result>> send,
        CancellationToken cancellationToken)
    {
        var composed = LineMcpMessages.Compose(text, messagesJson);
        if (composed.IsFailure)
        {
            return LineMcpJson.Error(composed.Error);
        }

        var body = composed.GetValueOrThrow();
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
            PayloadJson = BuildPayload(body, writeTarget),
            Summary = LineMcpMessages.Summarize(body),
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

        // 直接模式:先送再記。反過來(先記 Sent 再送)的話,送出失敗時佇列裡會留下一筆
        // 說「已送出」的假紀錄,而稽核紀錄說謊比沒有稽核紀錄更糟。
        // Direct mode sends first and records afterwards. The other order — record as Sent, then send — leaves a
        // row claiming a message went out when it did not, and an audit trail that lies is worse than none.
        var sent = await send(body).ConfigureAwait(false);

        item.DecidedAt = now;
        item.SentAt = sent.IsSuccess ? now : null;
        item.Status = sent.IsSuccess ? LineMcpOutboxStatus.Sent : LineMcpOutboxStatus.Failed;
        item.Error = sent.IsSuccess ? null : sent.Error.Message;

        var recorded = await _outbox.AddAsync(item, cancellationToken).ConfigureAwait(false);

        return sent.IsFailure
            ? LineMcpJson.Error(sent.Error)
            : LineMcpJson.Ok(new
            {
                ok = true,
                mode = "direct",
                outboxId = recorded.Id,
                summary = recorded.Summary,
                createdToday = used + 1,
                dailyLimit = options.MaxSendRequestsPerDay,
            });
    }

    /// <summary>
    /// 組出待發項目的內容 JSON。
    /// Builds the outbox item's payload JSON.
    /// </summary>
    /// <param name="messagesJson">訊息陣列 JSON。The message array's JSON.</param>
    /// <param name="writeTarget">寫出收件對象欄位的委派。The delegate writing the recipient fields.</param>
    /// <returns>內容 JSON。The payload JSON.</returns>
    private static string BuildPayload(string messagesJson, Action<Utf8JsonWriter> writeTarget)
    {
        using var document = JsonDocument.Parse(messagesJson);
        using var buffer = new MemoryStream();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writeTarget(writer);
            writer.WritePropertyName("messages");
            document.RootElement.WriteTo(writer);
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }
}
