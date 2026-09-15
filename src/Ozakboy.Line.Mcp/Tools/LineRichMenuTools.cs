using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Ozakboy.Line.Mcp.Outbox;
using Ozakboy.Line.Messaging;

namespace Ozakboy.Line.Mcp.Tools;

/// <summary>
/// 圖文選單的查詢與替換工具。
/// The tools for reading and replacing rich menus.
/// </summary>
[McpServerToolType]
public sealed class LineRichMenuTools
{
    private readonly ILineMessagingClient _messaging;
    private readonly ILineMcpOutboxStore _outbox;
    private readonly ILineMcpOutboxService _service;
    private readonly IOptions<LineMcpOptions> _options;

    /// <summary>
    /// 建立工具組。
    /// Creates the tool set.
    /// </summary>
    /// <param name="messaging">Messaging API 用戶端。The Messaging API client.</param>
    /// <param name="outbox">待發佇列。The outbox.</param>
    /// <param name="service">待發項目服務。The outbox service.</param>
    /// <param name="options">MCP 設定。The MCP settings.</param>
    /// <exception cref="ArgumentNullException">
    /// 任一參數為 <see langword="null"/> 時擲出。Thrown when any argument is <see langword="null"/>.
    /// </exception>
    public LineRichMenuTools(
        ILineMessagingClient messaging,
        ILineMcpOutboxStore outbox,
        ILineMcpOutboxService service,
        IOptions<LineMcpOptions> options)
    {
        ArgumentNullException.ThrowIfNull(messaging);
        ArgumentNullException.ThrowIfNull(outbox);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(options);

        _messaging = messaging;
        _outbox = outbox;
        _service = service;
        _options = options;
    }

    /// <summary>
    /// 列出所有圖文選單。
    /// Lists every rich menu.
    /// </summary>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>選單清單的 JSON。The menus as JSON.</returns>
    [McpServerTool(Name = "line_list_rich_menus")]
    [Description("列出這個官方帳號目前所有的圖文選單,含識別碼、名稱、尺寸、聊天列文字與各區塊的動作。")]
    public async Task<string> ListRichMenusAsync(CancellationToken cancellationToken = default)
    {
        var menus = await _messaging.GetRichMenuListAsync(cancellationToken).ConfigureAwait(false);

        return menus.IsFailure
            ? LineMcpJson.Error(menus.Error)
            : LineMcpJson.Ok(new
            {
                ok = true,
                count = menus.GetValueOrThrow().Count,
                richMenus = menus.GetValueOrThrow(),
            });
    }

    /// <summary>
    /// 查詢目前的預設圖文選單。
    /// Reads the current default rich menu.
    /// </summary>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>預設選單識別碼的 JSON。The default menu's identifier as JSON.</returns>
    [McpServerTool(Name = "line_get_default_rich_menu")]
    [Description("查詢目前設為所有使用者預設的圖文選單識別碼。沒有設定預設選單時回傳 null,那是正常狀態而非錯誤。" +
                 "注意:個別使用者身上的連結優先於預設選單,所以有人看到的可能不是這一個。")]
    public async Task<string> GetDefaultRichMenuAsync(CancellationToken cancellationToken = default)
    {
        var id = await _messaging.GetDefaultRichMenuIdAsync(cancellationToken).ConfigureAwait(false);

        return id.IsFailure
            ? LineMcpJson.Error(id.Error)
            : LineMcpJson.Ok(new
            {
                ok = true,
                richMenuId = id.GetValueOrThrow(),
            });
    }

    /// <summary>
    /// 列出所有圖文選單別名。
    /// Lists every rich menu alias.
    /// </summary>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>別名清單的 JSON。The aliases as JSON.</returns>
    [McpServerTool(Name = "line_list_rich_menu_aliases")]
    [Description("列出所有圖文選單別名,以及各自目前指向哪一個選單。分頁式選單的切換動作(richmenuswitch)指的就是別名," +
                 "所以換選單時要一併確認別名有沒有改指到新的那一個。")]
    public async Task<string> ListRichMenuAliasesAsync(CancellationToken cancellationToken = default)
    {
        var aliases = await _messaging.GetRichMenuAliasListAsync(cancellationToken).ConfigureAwait(false);

        return aliases.IsFailure
            ? LineMcpJson.Error(aliases.Error)
            : LineMcpJson.Ok(new
            {
                ok = true,
                count = aliases.GetValueOrThrow().Count,
                aliases = aliases.GetValueOrThrow(),
            });
    }

    /// <summary>
    /// 驗證圖文選單定義,但不建立。
    /// Validates a rich menu definition without creating it.
    /// </summary>
    /// <param name="menuJson">選單定義 JSON。The menu definition's JSON.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>驗證結果的 JSON。The validation result as JSON.</returns>
    [McpServerTool(Name = "line_validate_rich_menu")]
    [Description("把一份圖文選單定義送給 LINE 驗證,但不建立任何東西。區塊超出邊界、尺寸不合規這類問題在這裡就看得到。" +
                 "提出 line_replace_rich_menu 之前先跑這個,可以避免排進待審佇列的內容根本送不出去。")]
    public async Task<string> ValidateRichMenuAsync(
        [Description("LINE 圖文選單定義 JSON,含 size、selected、name、chatBarText、areas")] string menuJson,
        CancellationToken cancellationToken = default)
    {
        var parsed = LineMcpRichMenu.Parse(menuJson);
        if (parsed.IsFailure)
        {
            return LineMcpJson.Error(parsed.Error);
        }

        var validated = await _messaging.ValidateRichMenuAsync(parsed.GetValueOrThrow(), cancellationToken).ConfigureAwait(false);

        return validated.IsFailure
            ? LineMcpJson.Error(validated.Error)
            : LineMcpJson.Ok(new { ok = true, valid = true });
    }

    /// <summary>
    /// 以新選單換掉舊選單。
    /// Replaces a rich menu with a new one.
    /// </summary>
    /// <param name="menuJson">選單定義 JSON。The menu definition's JSON.</param>
    /// <param name="imageUrl">選單圖片位址。The menu image's address.</param>
    /// <param name="setAsDefault">是否設為預設選單。Whether to make it the default.</param>
    /// <param name="oldRichMenuId">要刪掉的舊選單。The old menu to delete.</param>
    /// <param name="aliasId">要指向新選單的別名。The alias to point at the new menu.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>結果的 JSON。The result as JSON.</returns>
    [McpServerTool(Name = "line_replace_rich_menu")]
    [Description("以一份新的圖文選單換掉現行的選單:建立新選單、上傳圖片,再視參數設為預設、把別名指到新選單、刪掉舊選單。" +
                 "imageUrl 必須是公開可讀的 http/https 網址,型別 image/jpeg 或 image/png,大小 1MB 以內,圖片在核准的當下才抓取。" +
                 "預設為待審模式:本工具只把整個替換計畫排進待發佇列,需宿主人工核准後才會執行。")]
    public async Task<string> ReplaceRichMenuAsync(
        [Description("LINE 圖文選單定義 JSON,含 size、selected、name、chatBarText、areas")] string menuJson,
        [Description("選單圖片網址,http/https,image/jpeg 或 image/png,1MB 以內")] string imageUrl,
        [Description("是否把新選單設為所有使用者的預設選單")] bool setAsDefault = false,
        [Description("換好之後要刪掉的舊選單識別碼;省略則不刪")] string? oldRichMenuId = null,
        [Description("要改指向新選單的別名;不存在會自動建立")] string? aliasId = null,
        CancellationToken cancellationToken = default)
    {
        var options = _options.Value;

        if (!options.AllowRichMenuChanges)
        {
            return LineMcpJson.Error("這個站台不允許經由 MCP 變更圖文選單(AllowRichMenuChanges 為 false)。請改由宿主的後台操作。");
        }

        var parsed = LineMcpRichMenu.Parse(menuJson);
        if (parsed.IsFailure)
        {
            return LineMcpJson.Error(parsed.Error);
        }

        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return LineMcpJson.Error("imageUrl 不可為空白:沒有圖片的圖文選單在使用者眼裡是一片空白。");
        }

        var now = DateTimeOffset.UtcNow;
        var (used, hasAllowance) = await LineMcpDailyLimit
            .CheckAsync(_outbox, options, now, cancellationToken)
            .ConfigureAwait(false);

        if (!hasAllowance)
        {
            return LineMcpJson.Error(LineMcpDailyLimit.Message(used, options));
        }

        var menu = parsed.GetValueOrThrow();
        var item = new LineMcpOutboxItem
        {
            Kind = LineMcpOutboxKind.RichMenuReplace,
            PayloadJson = BuildPayload(menuJson, imageUrl, setAsDefault, oldRichMenuId, aliasId),
            Summary = $"替換圖文選單為「{menu.Name}」({menu.Areas.Count} 個區塊)",
            Status = LineMcpOutboxStatus.PendingReview,
            CreatedAt = now,
        };

        var queued = await _outbox.AddAsync(item, cancellationToken).ConfigureAwait(false);

        if (options.SendMode == LineMcpSendMode.Review)
        {
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

        // 直接模式下走的仍然是同一條核准路徑,只是核准的人變成程式自己。
        // 這樣抓圖、替換、記錄的流程只有一份實作 —— 兩份實作遲早會在其中一條路上漏掉一個步驟。
        // Direct mode still goes down the same approval path, with the program itself as the approver, so
        // fetching, replacing and recording have exactly one implementation. Two of them eventually drop a step
        // on one of the paths.
        var executed = await _service.ApproveAndSendAsync(queued.Id, cancellationToken).ConfigureAwait(false);
        if (executed.IsFailure)
        {
            return LineMcpJson.Error(executed.Error);
        }

        var result = executed.GetValueOrThrow();

        return result.Status == LineMcpOutboxStatus.Sent
            ? LineMcpJson.Ok(new
            {
                ok = true,
                mode = "direct",
                outboxId = result.Id,
                summary = result.Summary,
                createdToday = used + 1,
                dailyLimit = options.MaxSendRequestsPerDay,
            })
            : LineMcpJson.Error(result.Error ?? "替換圖文選單失敗。");
    }

    /// <summary>
    /// 組出替換選單的待發內容。
    /// Builds the payload for a menu replacement.
    /// </summary>
    /// <param name="menuJson">選單定義 JSON。The menu definition's JSON.</param>
    /// <param name="imageUrl">圖片位址。The image's address.</param>
    /// <param name="setAsDefault">是否設為預設。Whether to make it the default.</param>
    /// <param name="oldRichMenuId">舊選單識別碼。The old menu's identifier.</param>
    /// <param name="aliasId">別名。The alias.</param>
    /// <returns>內容 JSON。The payload JSON.</returns>
    private static string BuildPayload(
        string menuJson,
        string imageUrl,
        bool setAsDefault,
        string? oldRichMenuId,
        string? aliasId)
    {
        using var document = JsonDocument.Parse(menuJson);
        using var buffer = new MemoryStream();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("menu");

            // 存的是呼叫端給的原始選單 JSON,不是解析後再寫回去的版本:本套件沒建模的欄位
            // (LINE 之後新增的東西)因此不會在這一趟往返裡被洗掉。
            // The caller's original menu JSON is stored rather than a reserialised version, so fields this
            // package has not modelled — whatever LINE adds later — are not washed out by the round trip.
            document.RootElement.WriteTo(writer);

            writer.WriteString("imageUrl", imageUrl);
            writer.WriteBoolean("setAsDefault", setAsDefault);

            if (!string.IsNullOrWhiteSpace(oldRichMenuId))
            {
                writer.WriteString("oldRichMenuId", oldRichMenuId);
            }

            if (!string.IsNullOrWhiteSpace(aliasId))
            {
                writer.WriteString("aliasId", aliasId);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
