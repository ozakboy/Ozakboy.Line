using System.ComponentModel;
using ModelContextProtocol.Server;
using Ozakboy.Line.Mcp.Outbox;

namespace Ozakboy.Line.Mcp.Tools;

/// <summary>
/// 待發佇列的查詢與取消工具。
/// The tools for reading the outbox and cancelling from it.
/// </summary>
/// <remarks>
/// 這裡<b>沒有核准工具</b>,而且不會有。待審制度的價值就在於提案的一方沒有核准權 ——
/// 核准由宿主呼叫 <see cref="ILineMcpOutboxService.ApproveAndSendAsync"/>,不經過 MCP。
/// There is <b>no approval tool</b> here, and there will not be one. A review queue is worth something because
/// whoever proposes cannot approve; approval is the host calling
/// <see cref="ILineMcpOutboxService.ApproveAndSendAsync"/>, never MCP.
/// </remarks>
[McpServerToolType]
public sealed class LineOutboxTools
{
    private readonly ILineMcpOutboxStore _outbox;
    private readonly ILineMcpOutboxService _service;

    /// <summary>
    /// 建立工具組。
    /// Creates the tool set.
    /// </summary>
    /// <param name="outbox">待發佇列。The outbox.</param>
    /// <param name="service">待發項目服務。The outbox service.</param>
    /// <exception cref="ArgumentNullException">
    /// 任一參數為 <see langword="null"/> 時擲出。Thrown when either argument is <see langword="null"/>.
    /// </exception>
    public LineOutboxTools(ILineMcpOutboxStore outbox, ILineMcpOutboxService service)
    {
        ArgumentNullException.ThrowIfNull(outbox);
        ArgumentNullException.ThrowIfNull(service);

        _outbox = outbox;
        _service = service;
    }

    /// <summary>
    /// 列出待發項目。
    /// Lists the outbox items.
    /// </summary>
    /// <param name="status">狀態篩選。The status filter.</param>
    /// <param name="limit">最多幾筆。The most to return.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>項目清單的 JSON。The items as JSON.</returns>
    [McpServerTool(Name = "line_list_outbox")]
    [Description("列出待發佇列裡的項目,新的在前。status 可填 PendingReview(待審)、Approved(已核准)、Sent(已送出)、" +
                 "Failed(送出失敗)、Rejected(已退回)、Canceled(已取消);省略則不分狀態。" +
                 "這是確認「先前提出的內容後來怎麼了」的地方。")]
    public async Task<string> ListOutboxAsync(
        [Description("狀態篩選;省略代表不分狀態")] string? status = null,
        [Description("最多回傳幾筆,預設 20")] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        LineMcpOutboxStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<LineMcpOutboxStatus>(status, ignoreCase: true, out var parsed))
            {
                return LineMcpJson.Error(
                    $"status「{status}」不是有效的狀態。可用值:{string.Join("、", Enum.GetNames<LineMcpOutboxStatus>())}。");
            }

            filter = parsed;
        }

        var items = await _outbox.ListAsync(filter, limit, cancellationToken).ConfigureAwait(false);

        return LineMcpJson.Ok(new
        {
            ok = true,
            count = items.Count,
            items = items.Select(Describe),
        });
    }

    /// <summary>
    /// 取得單一待發項目,含完整內容。
    /// Gets one outbox item, payload included.
    /// </summary>
    /// <param name="id">項目識別碼。The item identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>項目的 JSON。The item as JSON.</returns>
    [McpServerTool(Name = "line_get_outbox")]
    [Description("取得單一待發項目的完整內容,包含要送出的訊息 JSON、目前狀態、建立與決定時間,以及失敗時的原因。")]
    public async Task<string> GetOutboxAsync(
        [Description("待發項目識別碼")] string id,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return LineMcpJson.Error("id 不可為空白。");
        }

        var item = await _outbox.GetAsync(id, cancellationToken).ConfigureAwait(false);

        return item is null
            ? LineMcpJson.Error($"找不到待發項目 {id}。")
            : LineMcpJson.Ok(new
            {
                ok = true,
                item = Describe(item),
                payloadJson = item.PayloadJson,
            });
    }

    /// <summary>
    /// 取消一筆待審項目。
    /// Cancels a pending item.
    /// </summary>
    /// <param name="id">項目識別碼。The item identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>結果的 JSON。The result as JSON.</returns>
    [McpServerTool(Name = "line_cancel_outbox")]
    [Description("取消一筆還在待審(PendingReview)的待發項目,例如發現內容寫錯了想重提一筆。" +
                 "只有待審中的項目可以取消:已核准或已送出的訊息收不回來,那時請直接聯繫宿主。")]
    public async Task<string> CancelOutboxAsync(
        [Description("待發項目識別碼")] string id,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return LineMcpJson.Error("id 不可為空白。");
        }

        var canceled = await _service.CancelAsync(id, cancellationToken).ConfigureAwait(false);

        return canceled.IsFailure
            ? LineMcpJson.Error(canceled.Error)
            : LineMcpJson.Ok(new
            {
                ok = true,
                item = Describe(canceled.GetValueOrThrow()),
            });
    }

    /// <summary>
    /// 把項目整理成回傳用的形狀(不含完整內容)。
    /// Shapes an item for the response, without its full payload.
    /// </summary>
    /// <param name="item">項目。The item.</param>
    /// <returns>回傳用的物件。The object to return.</returns>
    /// <remarks>
    /// 列表不帶 <see cref="LineMcpOutboxItem.PayloadJson"/>:二十筆訊息陣列的原文會把回應撐得很大,
    /// 而列表的用途是「看一眼有哪些」。要看內容請用 <c>line_get_outbox</c>。
    /// A listing leaves <see cref="LineMcpOutboxItem.PayloadJson"/> out: twenty message arrays in full make for a
    /// very large response, and a listing exists to show what is there. <c>line_get_outbox</c> is for the
    /// contents.
    /// </remarks>
    private static object Describe(LineMcpOutboxItem item) => new
    {
        id = item.Id,
        kind = item.Kind.ToString(),
        status = item.Status.ToString(),
        summary = item.Summary,
        createdAt = item.CreatedAt,
        decidedAt = item.DecidedAt,
        sentAt = item.SentAt,
        error = item.Error,
        source = item.Source,
    };
}
