namespace Ozakboy.Line.Mcp.Outbox;

/// <summary>
/// 待發佇列的儲存體。
/// The store holding the outbox.
/// </summary>
/// <remarks>
/// 宿主已經有後台與資料庫時,自己實作這個介面,待審項目就會出現在既有的審核頁裡,
/// 而不是另外開一個只有這個套件看得到的佇列 —— 兩個待審佇列的結果是其中一個沒人看。
/// A host that already has an admin section and a database implements this interface, and the items show up in
/// the review pages that already exist rather than in a second queue only this package knows about. Two review
/// queues end with one of them unread.
/// </remarks>
public interface ILineMcpOutboxStore
{
    /// <summary>
    /// 列出待發項目,新的在前。
    /// Lists the items, newest first.
    /// </summary>
    /// <param name="status">只列這個狀態;為 <see langword="null"/> 時全列。Only this status, or <see langword="null"/> for all of them.</param>
    /// <param name="limit">最多幾筆。The most to return.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>項目清單。The items.</returns>
    Task<IReadOnlyList<LineMcpOutboxItem>> ListAsync(
        LineMcpOutboxStatus? status = null,
        int limit = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得單一項目。
    /// Gets one item.
    /// </summary>
    /// <param name="id">項目識別碼。The item identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>找不到時為 <see langword="null"/>。<see langword="null"/> when there is no such item.</returns>
    Task<LineMcpOutboxItem?> GetAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增一筆項目。
    /// Adds an item.
    /// </summary>
    /// <param name="item">項目;<see cref="LineMcpOutboxItem.Id"/> 留空時由 store 產生。The item; a blank <see cref="LineMcpOutboxItem.Id"/> is filled in by the store.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>存好的項目。The stored item.</returns>
    Task<LineMcpOutboxItem> AddAsync(LineMcpOutboxItem item, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新一筆既有項目。
    /// Updates an existing item.
    /// </summary>
    /// <param name="item">項目。The item.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>
    /// 更新成功時為 <see langword="true"/>;項目不存在時為 <see langword="false"/>。
    /// <see langword="true"/> when it was updated, <see langword="false"/> when there was no such item.
    /// </returns>
    Task<bool> UpdateAsync(LineMcpOutboxItem item, CancellationToken cancellationToken = default);

    /// <summary>
    /// 數出某個時間區間內建立了幾筆(每日上限用)。
    /// Counts the items created within a time range, for the daily cap.
    /// </summary>
    /// <param name="from">起(含)。The start, inclusive.</param>
    /// <param name="until">迄(不含)。The end, exclusive.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>筆數。The count.</returns>
    /// <remarks>
    /// 數的是<b>建立</b>而不是送出:上限要擋的是 AI 灌爆佇列,而不是宿主核准了幾筆。
    /// 以送出計數的話,一個從不核准的佇列可以被無限灌下去。
    /// It counts what was <b>created</b> rather than what was sent: the cap exists to stop an AI flooding the
    /// queue, not to limit how much the host approves. Counting sends would let a queue nobody approves be filled
    /// without limit.
    /// </remarks>
    Task<int> CountCreatedBetweenAsync(DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken = default);
}
