namespace Ozakboy.Line.AutoReply;

/// <summary>
/// 決定 <see cref="ILineAutoReplyStore"/> 用哪一種儲存體。
/// Chooses which <see cref="ILineAutoReplyStore"/> gets registered.
/// </summary>
/// <remarks>
/// 什麼都不設定就是記憶體儲存體,理由與 <see cref="Templates.LineTemplateStoreOptions"/> 相同。
/// Setting nothing means the in-memory store, for the same reason as
/// <see cref="Templates.LineTemplateStoreOptions"/>.
/// </remarks>
public sealed class LineAutoReplyStoreOptions
{
    /// <summary>
    /// JSON 檔的路徑;為 <see langword="null"/> 時用記憶體儲存體。
    /// The JSON file's path, or <see langword="null"/> for the in-memory store.
    /// </summary>
    public string? JsonFilePath { get; private set; }

    /// <summary>
    /// 改用 JSON 檔儲存。
    /// Switches to the JSON file store.
    /// </summary>
    /// <param name="filePath">檔案路徑。The file path.</param>
    /// <returns>同一個設定物件,方便串接。The same options object, for chaining.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="filePath"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="filePath"/> is <see langword="null"/> or blank.
    /// </exception>
    public LineAutoReplyStoreOptions UseJsonFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        JsonFilePath = filePath;
        return this;
    }
}
