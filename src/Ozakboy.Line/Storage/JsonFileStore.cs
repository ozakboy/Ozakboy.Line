using System.Text.Json;

namespace Ozakboy.Line.Storage;

/// <summary>
/// 把一份清單存成單一 JSON 陣列檔的小型儲存體,供本套件的檔案型 store 共用。
/// A small store keeping one list in a single JSON array file, shared by this package's file-backed stores.
/// </summary>
/// <typeparam name="T">清單元素的型別。The list element type.</typeparam>
/// <remarks>
/// <para>
/// 寫入一律<b>原子化</b>:先寫 <c>.tmp</c>,再以 <see cref="File.Move(string, string, bool)"/> 覆蓋過去。
/// 直接覆寫原檔的話,行程在寫到一半被中止(部署重啟、機器斷電)留下的就是一個半截 JSON ——
/// 那份檔案從此讀不回來,而下一次啟動才會發現。
/// Writes are <b>atomic</b>: to a <c>.tmp</c> file first, then over the original with
/// <see cref="File.Move(string, string, bool)"/>. Overwriting in place means a process stopped halfway — a
/// deployment restart, a power cut — leaves half a JSON document, unreadable from then on and only discovered at
/// the next start.
/// </para>
/// <para>
/// 讀寫都走同一個 <see cref="SemaphoreSlim"/>:平面檔沒有交易,兩個「讀出來、改一筆、寫回去」同時跑,
/// 後寫的那份會把先寫的整個蓋掉,而且不會有任何錯誤。
/// Reads and writes share one <see cref="SemaphoreSlim"/>: a flat file has no transactions, and two concurrent
/// read-modify-write cycles end with the later write discarding the earlier one entirely, silently.
/// </para>
/// <para>
/// 這個型別是 <c>internal</c>,並以 <c>InternalsVisibleTo</c> 開放給 <c>Ozakboy.Line.Mcp</c> 與測試專案。
/// 它是實作細節而不是公開契約 —— 檔案格式與鎖的策略都可能改,不該有外部程式碼綁在上面。
/// The type is <c>internal</c> and opened to <c>Ozakboy.Line.Mcp</c> and the test project through
/// <c>InternalsVisibleTo</c>. It is an implementation detail rather than a public contract: the file format and
/// the locking strategy may both change, and no outside code should be tied to them.
/// </para>
/// </remarks>
internal sealed class JsonFileStore<T> : IDisposable
    where T : class
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// 以檔案路徑建立。
    /// Creates one for a file path.
    /// </summary>
    /// <param name="filePath">JSON 檔的路徑。The JSON file's path.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="filePath"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="filePath"/> is <see langword="null"/> or blank.
    /// </exception>
    internal JsonFileStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    /// <summary>
    /// 讀出整份清單。
    /// Reads the whole list.
    /// </summary>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>檔案不存在時為空清單。An empty list when the file does not exist.</returns>
    /// <exception cref="InvalidDataException">
    /// 檔案存在但不是合法的 JSON 陣列時擲出。
    /// Thrown when the file exists but is not a valid JSON array.
    /// </exception>
    internal async Task<IReadOnlyList<T>> ReadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await LoadAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 讀出清單、交給呼叫端修改,需要時寫回。
    /// Reads the list, hands it to the caller to change, and writes it back when needed.
    /// </summary>
    /// <typeparam name="TResult">修改動作的回傳型別。The mutation's return type.</typeparam>
    /// <param name="mutate">
    /// 修改清單並回報「結果」與「是否真的改了」。<c>Changed</c> 為 <see langword="false"/> 時不寫檔。
    /// Changes the list and reports its result along with whether anything actually changed; nothing is written
    /// when <c>Changed</c> is <see langword="false"/>.
    /// </param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>修改動作的結果。The mutation's result.</returns>
    /// <exception cref="InvalidDataException">
    /// 既有檔案不是合法的 JSON 陣列時擲出。Thrown when the existing file is not a valid JSON array.
    /// </exception>
    internal async Task<TResult> UpdateAsync<TResult>(
        Func<List<T>, (TResult Result, bool Changed)> mutate,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var items = await LoadAsync(cancellationToken).ConfigureAwait(false);
            var (result, changed) = mutate(items);

            if (changed)
            {
                await SaveAsync(items, cancellationToken).ConfigureAwait(false);
            }

            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 釋放鎖。
    /// Releases the gate.
    /// </summary>
    /// <remarks>
    /// 這些 store 在容器裡是 singleton,實務上會活到行程結束才被釋放。實作 <see cref="IDisposable"/>
    /// 是為了讓「誰擁有這個 <see cref="SemaphoreSlim"/>」有明確答案,而不是靠終結器收尾。
    /// These stores are singletons in the container and in practice live until the process ends. Implementing
    /// <see cref="IDisposable"/> gives a clear answer to who owns the <see cref="SemaphoreSlim"/>, rather than
    /// leaving it to a finaliser.
    /// </remarks>
    public void Dispose() => _gate.Dispose();

    /// <summary>
    /// 讀檔;必須在持有鎖的情況下呼叫。
    /// Loads the file; must be called while holding the gate.
    /// </summary>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>清單。The list.</returns>
    /// <remarks>
    /// 整份讀進記憶體再解析,不走串流。這種 store 放的是規則、範本與待發項目 —— 數量以「人看得完」為上限,
    /// 而一次讀完換來的是簡單得多的錯誤處理與原子寫入。
    /// The whole file is read into memory and then parsed, rather than streamed. What these stores hold — rules,
    /// templates, outbox items — is bounded by what a person can read through, and reading it in one go buys much
    /// simpler error handling and atomic writes.
    /// </remarks>
    private async Task<List<T>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            // 檔案還沒有 = 還沒存過任何東西,不是錯誤。第一次寫入時才會建檔。
            // No file yet means nothing has been stored yet, which is not an error. It is created on first write.
            return [];
        }

        var bytes = await File.ReadAllBytesAsync(_filePath, cancellationToken).ConfigureAwait(false);
        if (bytes.Length == 0)
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<T>>(bytes, LineJson.Options) ?? [];
        }
        catch (JsonException exception)
        {
            // 這裡擲出例外而不是回 Result:檔案壞掉不是「預期之內的失敗」,而是資料毀損或有人手改壞了。
            // 靜靜地回一個空清單更糟 —— 下一次寫入就會把壞掉的檔案覆蓋掉,原本的資料再也救不回來。
            // An exception rather than a Result: a corrupted file is not an expected failure but damaged data, or
            // someone's hand edit gone wrong. Quietly returning an empty list would be worse — the next write
            // would overwrite the damaged file and the original data would be gone for good.
            throw new InvalidDataException(
                $"儲存檔 {_filePath} 不是合法的 JSON 陣列,拒絕以空清單覆蓋它。The store file {_filePath} is not a valid JSON array, and will not be overwritten with an empty list.",
                exception);
        }
    }

    /// <summary>
    /// 原子化寫檔;必須在持有鎖的情況下呼叫。
    /// Saves the file atomically; must be called while holding the gate.
    /// </summary>
    /// <param name="items">要寫出的清單。The list to write.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    private async Task SaveAsync(List<T> items, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = _filePath + ".tmp";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(items, LineJson.Options);

        // 先整份序列化成位元組再寫檔。序列化到一半才失敗(某個元素轉不出去)的話,
        // 這時 .tmp 還沒有被建出來,原檔也還沒被碰過 —— 失敗之後什麼都沒變。
        // Serialised to bytes in full before anything is written. A serialisation that fails partway — one
        // element that will not convert — happens before the .tmp file exists and before the original is
        // touched, so a failure changes nothing.
        await File.WriteAllBytesAsync(temporaryPath, bytes, cancellationToken).ConfigureAwait(false);
        File.Move(temporaryPath, _filePath, overwrite: true);
    }
}
