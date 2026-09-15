using Ozakboy.Line.Storage;

namespace Ozakboy.Line.Templates;

/// <summary>
/// 把所有範本存成一份 JSON 陣列檔的範本儲存體。
/// A template store keeping every template in one JSON array file.
/// </summary>
/// <remarks>
/// 寫入是原子化的(先寫 <c>.tmp</c> 再換檔),讀寫共用一把鎖 —— 細節與理由見
/// <see cref="Storage.JsonFileStore{T}"/>。檔案不存在時當作空清單;檔案存在但不是合法 JSON 陣列時
/// 擲出 <see cref="InvalidDataException"/> 而不是回一個空清單,因為後者會在下一次寫入時把壞掉的檔案覆蓋掉。
/// Writes are atomic — a <c>.tmp</c> file and then a swap — and reads and writes share one lock; the details and
/// the reasoning are in <see cref="Storage.JsonFileStore{T}"/>. A missing file reads as an empty list, while a
/// file that exists but is not a valid JSON array throws <see cref="InvalidDataException"/> rather than reading
/// as empty, because reading as empty would overwrite the damaged file on the next write.
/// </remarks>
public sealed class JsonFileLineTemplateStore : ILineTemplateStore, IDisposable
{
    private readonly JsonFileStore<LineMessageTemplate> _store;
    private readonly TimeProvider _clock;

    /// <summary>
    /// 建立儲存體。
    /// Creates the store.
    /// </summary>
    /// <param name="filePath">JSON 檔的路徑;目錄不存在時會在第一次寫入時建立。The JSON file's path; its directory is created on the first write.</param>
    /// <param name="clock">時間來源;為 <see langword="null"/> 時用系統時鐘。The time source, or <see langword="null"/> for the system clock.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="filePath"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="filePath"/> is <see langword="null"/> or blank.
    /// </exception>
    public JsonFileLineTemplateStore(string filePath, TimeProvider? clock = null)
    {
        _store = new JsonFileStore<LineMessageTemplate>(filePath);
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
    public async Task<IReadOnlyList<LineMessageTemplate>> ListAsync(CancellationToken cancellationToken = default)
    {
        var templates = await _store.ReadAsync(cancellationToken).ConfigureAwait(false);
        return [.. templates.OrderBy(template => template.Name, StringComparer.Ordinal)];
    }

    /// <inheritdoc />
    public async Task<LineMessageTemplate?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var templates = await _store.ReadAsync(cancellationToken).ConfigureAwait(false);
        return templates.FirstOrDefault(template => string.Equals(template.Id, id, StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public Task<LineMessageTemplate> UpsertAsync(LineMessageTemplate template, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);

        var now = _clock.GetUtcNow();
        var stored = template.Clone();

        return _store.UpdateAsync<LineMessageTemplate>(
            templates =>
            {
                if (string.IsNullOrWhiteSpace(stored.Id))
                {
                    stored.Id = LineStoreIds.New();
                    stored.CreatedAt = now;
                    stored.UpdatedAt = now;
                    templates.Add(stored);
                    return (stored.Clone(), true);
                }

                var index = templates.FindIndex(item => string.Equals(item.Id, stored.Id, StringComparison.Ordinal));
                stored.CreatedAt = index >= 0 ? templates[index].CreatedAt : now;
                stored.UpdatedAt = now;

                if (index >= 0)
                {
                    templates[index] = stored;
                }
                else
                {
                    templates.Add(stored);
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
            templates =>
            {
                var removed = templates.RemoveAll(template => string.Equals(template.Id, id, StringComparison.Ordinal));

                // 沒刪到就不寫檔:刪一個不存在的識別碼不該讓檔案的修改時間變動,
                // 那會讓「這份設定上次是什麼時候改的」變成一個不可信的訊號。
                // Nothing removed means nothing written: deleting an identifier that is not there should not move
                // the file's modification time, or "when was this last changed" stops being a signal worth
                // trusting.
                return (removed > 0, removed > 0);
            },
            cancellationToken);
    }
}
