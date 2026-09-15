using System.Collections.Concurrent;
using Ozakboy.Line.Storage;

namespace Ozakboy.Line.Mcp.Outbox;

/// <summary>
/// 存在記憶體裡的待發佇列。
/// An outbox that lives in memory.
/// </summary>
/// <remarks>
/// 行程結束就沒了。這在<b>正式環境是個問題</b>而不只是不方便:重啟一次,待審的項目與已送的紀錄一起消失,
/// 而稽核紀錄的價值正好建立在它不會消失上。這個實作是給測試與試跑用的。
/// It is gone when the process ends, which in <b>production is a problem</b> rather than an inconvenience: one
/// restart takes the pending items and the record of what was sent with it, and an audit trail is worth
/// something precisely because it does not disappear. This implementation is for tests and trial runs.
/// </remarks>
public sealed class InMemoryLineMcpOutboxStore : ILineMcpOutboxStore
{
    private readonly ConcurrentDictionary<string, LineMcpOutboxItem> _items = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<IReadOnlyList<LineMcpOutboxItem>> ListAsync(
        LineMcpOutboxStatus? status = null,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var items = _items.Values
            .Where(item => status is null || item.Status == status)
            .OrderByDescending(item => item.CreatedAt)
            .Take(Math.Max(limit, 1))
            .Select(item => item.Clone())
            .ToList();

        return Task.FromResult<IReadOnlyList<LineMcpOutboxItem>>(items);
    }

    /// <inheritdoc />
    public Task<LineMcpOutboxItem?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return Task.FromResult(_items.TryGetValue(id, out var item) ? item.Clone() : null);
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

        _items[stored.Id] = stored;
        return Task.FromResult(stored.Clone());
    }

    /// <inheritdoc />
    public Task<bool> UpdateAsync(LineMcpOutboxItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(item.Id);

        if (!_items.ContainsKey(item.Id))
        {
            return Task.FromResult(false);
        }

        _items[item.Id] = item.Clone();
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<int> CountCreatedBetweenAsync(
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_items.Values.Count(item => item.CreatedAt >= from && item.CreatedAt < until));
}
