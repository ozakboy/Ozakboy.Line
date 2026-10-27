using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Storage;

namespace Ozakboy.Line.Postback;

/// <summary>
/// 把暫存項目存成一份 JSON 陣列檔的 postback 資料暫存。
/// A postback data staging store keeping its entries in one JSON array file.
/// </summary>
/// <remarks>
/// <para>
/// 寫入是原子化的、讀寫共用一把鎖 —— 細節見 <see cref="Storage.JsonFileStore{T}"/>;檔案壞掉時擲出
/// <see cref="InvalidDataException"/>,不回空清單。能活過重啟,但仍是單機:多台機器要共用請自己實作
/// <see cref="ILinePostbackDataStore"/>。
/// Writes are atomic and reads and writes share one lock — see <see cref="Storage.JsonFileStore{T}"/>; a damaged
/// file throws <see cref="InvalidDataException"/> rather than reading as empty. It survives a restart but is
/// still single-machine: to share across machines, implement <see cref="ILinePostbackDataStore"/> yourself.
/// </para>
/// <para>
/// 過期項目在 <see cref="GetAsync"/> 時只是「讀不到」,不會改檔;只有 <see cref="StoreAsync"/> 與
/// <see cref="PurgeExpiredAsync"/> 會寫檔,其中 <see cref="PurgeExpiredAsync"/> 真的把過期項目從檔案裡清掉。
/// On <see cref="GetAsync"/> an expired entry is merely not found and the file is left alone; only
/// <see cref="StoreAsync"/> and <see cref="PurgeExpiredAsync"/> write, and <see cref="PurgeExpiredAsync"/>
/// really removes the expired entries from the file.
/// </para>
/// </remarks>
public sealed class JsonFileLinePostbackDataStore : ILinePostbackDataStore, IDisposable
{
    private readonly JsonFileStore<LinePostbackDataEntry> _store;
    private readonly LinePostbackDataOptions _options;
    private readonly TimeProvider _clock;

    /// <summary>
    /// 建立儲存體。
    /// Creates the store.
    /// </summary>
    /// <param name="filePath">JSON 檔的路徑;目錄不存在時會在第一次寫入時建立。The JSON file's path; its directory is created on the first write.</param>
    /// <param name="options">設定;<see langword="null"/> 時用預設值。The options, or <see langword="null"/> for the defaults.</param>
    /// <param name="clock">時間來源;<see langword="null"/> 時用系統時鐘。The time source, or <see langword="null"/> for the system clock.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="filePath"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="filePath"/> is <see langword="null"/> or blank.
    /// </exception>
    public JsonFileLinePostbackDataStore(string filePath, LinePostbackDataOptions? options = null, TimeProvider? clock = null)
    {
        _store = new JsonFileStore<LinePostbackDataEntry>(filePath);
        _options = options ?? new LinePostbackDataOptions();
        _options.EnsureValid();
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
    public async Task<Result<string>> StoreAsync(string data, TimeSpan? timeToLive = null, CancellationToken cancellationToken = default)
    {
        var expiresAt = LinePostbackDataStoreSupport.Prepare(data, timeToLive, _options, _clock.GetUtcNow());
        if (expiresAt.IsFailure)
        {
            return Result.Failure<string>(expiresAt.Error!);
        }

        var entry = new LinePostbackDataEntry
        {
            Token = LinePostbackDataToken.Create(),
            Data = data,
            ExpiresAt = expiresAt.GetValueOrThrow(),
        };

        await _store.UpdateAsync<bool>(
            entries =>
            {
                entries.Add(entry);
                return (true, true);
            },
            cancellationToken).ConfigureAwait(false);

        return Result.Success(entry.Token);
    }

    /// <inheritdoc />
    public async Task<Result<string>> GetAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var entries = await _store.ReadAsync(cancellationToken).ConfigureAwait(false);
        var now = _clock.GetUtcNow();
        var entry = entries.FirstOrDefault(item => string.Equals(item.Token, token, StringComparison.Ordinal));

        return entry is not null && entry.ExpiresAt > now
            ? Result.Success(entry.Data)
            : Result.Failure<string>(LinePostbackDataStoreSupport.Expired());
    }

    /// <inheritdoc />
    public async Task<Result<int>> PurgeExpiredAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow();

        // 沒有過期項目就不寫檔,理由同範本 store 的刪除:無事不動檔案的修改時間。
        // Nothing expired means nothing written, for the same reason as the template store's delete.
        var removed = await _store.UpdateAsync<int>(
            entries =>
            {
                var count = entries.RemoveAll(entry => entry.ExpiresAt <= now);
                return (count, count > 0);
            },
            cancellationToken).ConfigureAwait(false);

        return Result.Success(removed);
    }
}
