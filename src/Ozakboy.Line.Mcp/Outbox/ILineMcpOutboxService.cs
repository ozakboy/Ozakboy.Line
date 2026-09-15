using Ozakboy.Core.Abstractions;

namespace Ozakboy.Line.Mcp.Outbox;

/// <summary>
/// 對待發項目做決定:核准並送出、退回、取消。
/// Decides what happens to a queued item: approve and send it, reject it, or cancel it.
/// </summary>
/// <remarks>
/// <b>這個介面不會被包成 MCP 工具</b>,而且不該被包。待審制度的整個價值就在於「提案的一方沒有核准權」——
/// 把核准也做成工具,等於讓 AI 自己核准自己的提案,那時佇列只是多了一道手續。
/// 這個介面是給宿主的後台、管理命令或審核頁呼叫的。
/// <b>This interface is not exposed as an MCP tool</b>, and should not be. The whole value of a review queue is
/// that whoever proposes cannot approve; a tool for approving would let the AI approve its own proposals, and the
/// queue would be one extra step and nothing more. This is for a host's admin pages, management commands, or
/// review screens to call.
/// </remarks>
public interface ILineMcpOutboxService
{
    /// <summary>
    /// 核准一筆待審項目並立刻送出。
    /// Approves a pending item and sends it straight away.
    /// </summary>
    /// <param name="id">項目識別碼。The item identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>
    /// 更新後的項目(<see cref="LineMcpOutboxStatus.Sent"/> 或 <see cref="LineMcpOutboxStatus.Failed"/>);
    /// 項目不存在為 <see cref="LineErrorCodes.McpOutboxNotFound"/>、狀態不對為
    /// <see cref="LineErrorCodes.McpOutboxInvalidStatus"/> 的失敗。
    /// The updated item, either <see cref="LineMcpOutboxStatus.Sent"/> or
    /// <see cref="LineMcpOutboxStatus.Failed"/>. A <see cref="LineErrorCodes.McpOutboxNotFound"/> failure when
    /// there is no such item, and a <see cref="LineErrorCodes.McpOutboxInvalidStatus"/> one when its status does
    /// not allow it.
    /// </returns>
    /// <remarks>
    /// 送出失敗時項目記為 <see cref="LineMcpOutboxStatus.Failed"/> 並留下原因,<b>而不是留在待審</b>:
    /// 留在待審的話,下一個看到它的人不知道它已經被試過一次,而重按核准可能是重複發送。
    /// A failed send marks the item <see cref="LineMcpOutboxStatus.Failed"/> with the reason recorded, <b>rather
    /// than leaving it pending</b>: pending, the next person to see it does not know it has already been tried,
    /// and pressing approve again may send it twice.
    /// </remarks>
    Task<Result<LineMcpOutboxItem>> ApproveAndSendAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 退回一筆待審項目。
    /// Rejects a pending item.
    /// </summary>
    /// <param name="id">項目識別碼。The item identifier.</param>
    /// <param name="reason">退回理由;可為 <see langword="null"/>。Why, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>更新後的項目。The updated item.</returns>
    Task<Result<LineMcpOutboxItem>> RejectAsync(string id, string? reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取消一筆待審項目。
    /// Cancels a pending item.
    /// </summary>
    /// <param name="id">項目識別碼。The item identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>更新後的項目。The updated item.</returns>
    /// <remarks>
    /// 只有 <see cref="LineMcpOutboxStatus.PendingReview"/> 的項目可以取消。已送出的訊息收不回來,
    /// 而讓 API 接受一個做不到的請求、再回一個含糊的成功,比明確拒絕更糟。
    /// Only a <see cref="LineMcpOutboxStatus.PendingReview"/> item can be cancelled. A sent message cannot be
    /// recalled, and an API that accepts a request it cannot honour and answers with a vague success is worse
    /// than one that refuses plainly.
    /// </remarks>
    Task<Result<LineMcpOutboxItem>> CancelAsync(string id, CancellationToken cancellationToken = default);
}
