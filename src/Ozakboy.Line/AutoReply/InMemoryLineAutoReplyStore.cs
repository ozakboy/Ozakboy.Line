using System.Collections.Concurrent;
using Ozakboy.Line.Storage;

namespace Ozakboy.Line.AutoReply;

/// <summary>
/// 存在記憶體裡的規則儲存體。
/// A rule store that lives in memory.
/// </summary>
/// <remarks>
/// 行程結束就沒了,而且多台機器各存各的。適合測試與範例;正式環境請用
/// <see cref="JsonFileLineAutoReplyStore"/> 或自己接資料庫。
/// It is gone when the process ends, and every machine keeps its own copy. It suits tests and samples;
/// production wants <see cref="JsonFileLineAutoReplyStore"/> or a database of your own.
/// </remarks>
public sealed class InMemoryLineAutoReplyStore : ILineAutoReplyStore
{
    private readonly ConcurrentDictionary<string, LineAutoReplyRule> _rules = new(StringComparer.Ordinal);
    private readonly TimeProvider _clock;

    /// <summary>
    /// 建立儲存體。
    /// Creates the store.
    /// </summary>
    /// <param name="clock">時間來源;為 <see langword="null"/> 時用系統時鐘。The time source, or <see langword="null"/> for the system clock.</param>
    public InMemoryLineAutoReplyStore(TimeProvider? clock = null) => _clock = clock ?? TimeProvider.System;

    /// <inheritdoc />
    public Task<IReadOnlyList<LineAutoReplyRule>> ListAsync(CancellationToken cancellationToken = default)
    {
        var rules = _rules.Values
            .OrderBy(rule => rule.Priority)
            .ThenBy(rule => rule.Name, StringComparer.Ordinal)
            .Select(rule => rule.Clone())
            .ToList();

        return Task.FromResult<IReadOnlyList<LineAutoReplyRule>>(rules);
    }

    /// <inheritdoc />
    public Task<LineAutoReplyRule?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return Task.FromResult(_rules.TryGetValue(id, out var rule) ? rule.Clone() : null);
    }

    /// <inheritdoc />
    public Task<LineAutoReplyRule> UpsertAsync(LineAutoReplyRule rule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var now = _clock.GetUtcNow();
        var stored = rule.Clone();

        if (string.IsNullOrWhiteSpace(stored.Id))
        {
            stored.Id = LineStoreIds.New();
            stored.CreatedAt = now;
        }
        else if (_rules.TryGetValue(stored.Id, out var existing))
        {
            stored.CreatedAt = existing.CreatedAt;
        }
        else
        {
            stored.CreatedAt = now;
        }

        stored.UpdatedAt = now;
        _rules[stored.Id] = stored;

        return Task.FromResult(stored.Clone());
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return Task.FromResult(_rules.TryRemove(id, out _));
    }
}
