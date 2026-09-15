using Ozakboy.Line.Storage;

namespace Ozakboy.Line.Mcp.Outbox;

/// <summary>
/// 把待發佇列存成一份 JSON 陣列檔的儲存體。
/// An outbox keeping every item in one JSON array file.
/// </summary>
/// <remarks>
/// <para>
/// 寫入原子化、讀寫共用一把鎖,細節與理由見核心套件的 <c>JsonFileStore</c>。
/// Writes are atomic and reads and writes share one lock; the details and the reasoning are in the core
/// package's <c>JsonFileStore</c>.
/// </para>
/// <para>
/// 這份檔案<b>只長不消</b>:已送、已退回、已取消的項目都留著,因為它同時是稽核紀錄。
/// 真的長到難以處理時,請由宿主自己搬歷史資料 —— 本套件不自動刪任何一筆,
/// 自動清掉的稽核紀錄在需要它的那一天就是不存在。
/// The file <b>only grows</b>: sent, rejected and cancelled items all stay, because this is also the audit
/// trail. When it grows past what is comfortable, archiving is the host's to do — nothing here deletes a row,
/// since an audit trail that tidies itself is simply absent on the day it is wanted.
/// </para>
/// </remarks>
public sealed class JsonFileLineMcpOutboxStore : ILineMcpOutboxStore, IDisposable
{
    private readonly JsonFileStore<LineMcpOutboxItem> _store;

    /// <summary>
    /// 建立儲存體。
    /// Creates the store.
    /// </summary>
    /// <param name="filePath">JSON 檔的路徑。The JSON file's path.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="filePath"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="filePath"/> is <see langword="null"/> or blank.
    /// </exception>
    public JsonFileLineMcpOutboxStore(string filePath) => _store = new JsonFileStore<LineMcpOutboxItem>(filePath);

    /// <summary>
    /// 釋放內部的讀寫鎖。
    /// Releases the internal read-write lock.
    /// </summary>
    public void Dispose() => _store.Dispose();

    /// <inheritdoc />
    public async Task<IReadOnlyList<LineMcpOutboxItem>> ListAsync(
        LineMcpOutboxStatus? status = null,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var items = await _store.ReadAsync(cancellationToken).ConfigureAwait(false);

        return
        [
            .. items
                .Where(item => status is null || item.Status == status)
                .OrderByDescending(item => item.CreatedAt)
                .Take(Math.Max(limit, 1)),
        ];
    }

    /// <inheritdoc />
    public async Task<LineMcpOutboxItem?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var items = await _store.ReadAsync(cancellationToken).ConfigureAwait(false);
        return items.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public Task<LineMcpOutboxItem> AddAsync(LineMcpOutboxItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        var stored = item.Clone();
        if (string.IsNullOrWhiteSpace(stored.Id))
        {
            stored.Id = LineStoreIds.New();
        }

        return _store.UpdateAsync<LineMcpOutboxItem>(
            items =>
            {
                items.Add(stored);
                return (stored.Clone(), true);
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> UpdateAsync(LineMcpOutboxItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(item.Id);

        var stored = item.Clone();

        return _store.UpdateAsync<bool>(
            items =>
            {
                var index = items.FindIndex(existing => string.Equals(existing.Id, stored.Id, StringComparison.Ordinal));
                if (index < 0)
                {
                    return (false, false);
                }

                items[index] = stored;
                return (true, true);
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> CountCreatedBetweenAsync(
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken = default)
    {
        var items = await _store.ReadAsync(cancellationToken).ConfigureAwait(false);
        return items.Count(item => item.CreatedAt >= from && item.CreatedAt < until);
    }
}
