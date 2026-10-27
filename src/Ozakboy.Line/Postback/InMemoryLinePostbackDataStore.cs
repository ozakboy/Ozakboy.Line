using System.Collections.Concurrent;
using Ozakboy.Core.Abstractions;

namespace Ozakboy.Line.Postback;

/// <summary>
/// 存在記憶體裡的 postback 資料暫存。
/// A postback data staging store that lives in memory.
/// </summary>
/// <remarks>
/// 行程結束就沒了,而且多台機器各存各的(A 機發出去的按鈕被 B 機收到會查不到)。取用時順便剔除該筆的過期項目;
/// 其餘過期項目留在記憶體裡直到 <see cref="PurgeExpiredAsync"/>,長時間執行的宿主請定期呼叫。
/// It is gone when the process ends, and every machine keeps its own copy — a button sent by machine A and
/// received by machine B finds nothing. An expired entry is dropped when it is looked up; the rest stay in memory
/// until <see cref="PurgeExpiredAsync"/>, which a long-running host should call periodically.
/// </remarks>
public sealed class InMemoryLinePostbackDataStore : ILinePostbackDataStore
{
    private readonly ConcurrentDictionary<string, LinePostbackDataEntry> _entries = new(StringComparer.Ordinal);
    private readonly LinePostbackDataOptions _options;
    private readonly TimeProvider _clock;

    /// <summary>
    /// 建立儲存體。
    /// Creates the store.
    /// </summary>
    /// <param name="options">設定;<see langword="null"/> 時用預設值(<see cref="LinePostbackDataOptions.JsonFilePath"/> 在此被忽略)。The options, or <see langword="null"/> for the defaults; <see cref="LinePostbackDataOptions.JsonFilePath"/> is ignored here.</param>
    /// <param name="clock">時間來源;<see langword="null"/> 時用系統時鐘。The time source, or <see langword="null"/> for the system clock.</param>
    public InMemoryLinePostbackDataStore(LinePostbackDataOptions? options = null, TimeProvider? clock = null)
    {
        _options = options ?? new LinePostbackDataOptions();
        _options.EnsureValid();
        _clock = clock ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public Task<Result<string>> StoreAsync(string data, TimeSpan? timeToLive = null, CancellationToken cancellationToken = default)
    {
        var expiresAt = LinePostbackDataStoreSupport.Prepare(data, timeToLive, _options, _clock.GetUtcNow());
        if (expiresAt.IsFailure)
        {
            return Task.FromResult(Result.Failure<string>(expiresAt.Error!));
        }

        var token = LinePostbackDataToken.Create();
        _entries[token] = new LinePostbackDataEntry { Token = token, Data = data, ExpiresAt = expiresAt.GetValueOrThrow() };

        return Task.FromResult(Result.Success(token));
    }

    /// <inheritdoc />
    public Task<Result<string>> GetAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        if (_entries.TryGetValue(token, out var entry))
        {
            if (entry.ExpiresAt > _clock.GetUtcNow())
            {
                return Task.FromResult(Result.Success(entry.Data));
            }

            // 只在「仍是同一個項目」時才移除,避免與並行的覆寫互相踩到。
            // Removed only if it is still that very entry, so a concurrent overwrite is not trampled.
            _entries.TryRemove(new KeyValuePair<string, LinePostbackDataEntry>(token, entry));
        }

        return Task.FromResult(Result.Failure<string>(LinePostbackDataStoreSupport.Expired()));
    }

    /// <inheritdoc />
    public Task<Result<int>> PurgeExpiredAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow();
        var removed = 0;

        foreach (var pair in _entries)
        {
            if (pair.Value.ExpiresAt <= now && _entries.TryRemove(pair))
            {
                removed++;
            }
        }

        return Task.FromResult(Result.Success(removed));
    }
}
