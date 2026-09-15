using Ozakboy.Line.Storage;

namespace Ozakboy.Line.AutoReply;

/// <summary>
/// 把所有規則存成一份 JSON 陣列檔的規則儲存體。
/// A rule store keeping every rule in one JSON array file.
/// </summary>
/// <remarks>
/// 寫入原子化、讀寫共用一把鎖,細節與理由見 <see cref="Storage.JsonFileStore{T}"/>。
/// Writes are atomic and reads and writes share one lock; the details and the reasoning are in
/// <see cref="Storage.JsonFileStore{T}"/>.
/// </remarks>
public sealed class JsonFileLineAutoReplyStore : ILineAutoReplyStore, IDisposable
{
    private readonly JsonFileStore<LineAutoReplyRule> _store;
    private readonly TimeProvider _clock;

    /// <summary>
    /// 建立儲存體。
    /// Creates the store.
    /// </summary>
    /// <param name="filePath">JSON 檔的路徑。The JSON file's path.</param>
    /// <param name="clock">時間來源;為 <see langword="null"/> 時用系統時鐘。The time source, or <see langword="null"/> for the system clock.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="filePath"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="filePath"/> is <see langword="null"/> or blank.
    /// </exception>
    public JsonFileLineAutoReplyStore(string filePath, TimeProvider? clock = null)
    {
        _store = new JsonFileStore<LineAutoReplyRule>(filePath);
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>
    /// 釋放內部的讀寫鎖。
    /// Releases the internal read-write lock.
    /// </summary>
    /// <remarks>
    /// 這個儲存體在容器裡是 singleton,由容器負責釋放。
    /// The store is a singleton in the container, and the container releases it.
    /// </remarks>
    public void Dispose() => _store.Dispose();

    /// <inheritdoc />
    public async Task<IReadOnlyList<LineAutoReplyRule>> ListAsync(CancellationToken cancellationToken = default)
    {
        var rules = await _store.ReadAsync(cancellationToken).ConfigureAwait(false);
        return [.. rules.OrderBy(rule => rule.Priority).ThenBy(rule => rule.Name, StringComparer.Ordinal)];
    }

    /// <inheritdoc />
    public async Task<LineAutoReplyRule?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var rules = await _store.ReadAsync(cancellationToken).ConfigureAwait(false);
        return rules.FirstOrDefault(rule => string.Equals(rule.Id, id, StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public Task<LineAutoReplyRule> UpsertAsync(LineAutoReplyRule rule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var now = _clock.GetUtcNow();
        var stored = rule.Clone();

        return _store.UpdateAsync<LineAutoReplyRule>(
            rules =>
            {
                if (string.IsNullOrWhiteSpace(stored.Id))
                {
                    stored.Id = LineStoreIds.New();
                    stored.CreatedAt = now;
                    stored.UpdatedAt = now;
                    rules.Add(stored);
                    return (stored.Clone(), true);
                }

                var index = rules.FindIndex(item => string.Equals(item.Id, stored.Id, StringComparison.Ordinal));
                stored.CreatedAt = index >= 0 ? rules[index].CreatedAt : now;
                stored.UpdatedAt = now;

                if (index >= 0)
                {
                    rules[index] = stored;
                }
                else
                {
                    rules.Add(stored);
                }

                return (stored.Clone(), true);
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return _store.UpdateAsync<bool>(
            rules =>
            {
                var removed = rules.RemoveAll(rule => string.Equals(rule.Id, id, StringComparison.Ordinal));
                return (removed > 0, removed > 0);
            },
            cancellationToken);
    }
}
