using System.Collections.Concurrent;
using Ozakboy.Line.Storage;

namespace Ozakboy.Line.Templates;

/// <summary>
/// 存在記憶體裡的範本儲存體。
/// A template store that lives in memory.
/// </summary>
/// <remarks>
/// 行程結束就沒了,而且多台機器各存各的。這是測試、範例與「先跑起來再說」用的預設實作;
/// 正式環境請用 <see cref="JsonFileLineTemplateStore"/> 或自己接資料庫。
/// It is gone when the process ends, and every machine keeps its own copy. This is the default for tests,
/// samples and getting something running; production wants <see cref="JsonFileLineTemplateStore"/> or a database
/// of your own.
/// </remarks>
public sealed class InMemoryLineTemplateStore : ILineTemplateStore
{
    private readonly ConcurrentDictionary<string, LineMessageTemplate> _templates = new(StringComparer.Ordinal);
    private readonly TimeProvider _clock;

    /// <summary>
    /// 建立儲存體。
    /// Creates the store.
    /// </summary>
    /// <param name="clock">時間來源;為 <see langword="null"/> 時用系統時鐘。The time source, or <see langword="null"/> for the system clock.</param>
    public InMemoryLineTemplateStore(TimeProvider? clock = null) => _clock = clock ?? TimeProvider.System;

    /// <inheritdoc />
    public Task<IReadOnlyList<LineMessageTemplate>> ListAsync(CancellationToken cancellationToken = default)
    {
        var templates = _templates.Values
            .OrderBy(template => template.Name, StringComparer.Ordinal)
            .Select(template => template.Clone())
            .ToList();

        return Task.FromResult<IReadOnlyList<LineMessageTemplate>>(templates);
    }

    /// <inheritdoc />
    public Task<LineMessageTemplate?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return Task.FromResult(_templates.TryGetValue(id, out var template) ? template.Clone() : null);
    }

    /// <inheritdoc />
    public Task<LineMessageTemplate> UpsertAsync(LineMessageTemplate template, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);

        var now = _clock.GetUtcNow();
        var stored = template.Clone();

        if (string.IsNullOrWhiteSpace(stored.Id))
        {
            stored.Id = LineStoreIds.New();
            stored.CreatedAt = now;
        }
        else if (_templates.TryGetValue(stored.Id, out var existing))
        {
            // 建立時間以既有的為準。讓呼叫端送來的值覆蓋它,等於每次編輯都把「這份範本什麼時候做的」洗掉。
            // The creation time comes from what is already stored: letting the caller's value overwrite it would
            // erase when the template was first made, once per edit.
            stored.CreatedAt = existing.CreatedAt;
        }
        else
        {
            stored.CreatedAt = now;
        }

        stored.UpdatedAt = now;
        _templates[stored.Id] = stored;

        return Task.FromResult(stored.Clone());
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return Task.FromResult(_templates.TryRemove(id, out _));
    }
}
