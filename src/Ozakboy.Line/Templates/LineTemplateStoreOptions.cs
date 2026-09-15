namespace Ozakboy.Line.Templates;

/// <summary>
/// 決定 <see cref="ILineTemplateStore"/> 用哪一種儲存體。
/// Chooses which <see cref="ILineTemplateStore"/> gets registered.
/// </summary>
/// <remarks>
/// 什麼都不設定就是記憶體儲存體。這是刻意的預設:一個什麼都不用設定就跑得起來的東西,
/// 比一個「忘了設路徑就在啟動時爆掉」的東西好上手,而要上正式環境的人一定會看到這一段。
/// Setting nothing means the in-memory store. That default is deliberate: something that runs with no
/// configuration at all is easier to start with than something that fails at startup over a missing path, and
/// anyone taking it to production will have read this far.
/// </remarks>
public sealed class LineTemplateStoreOptions
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
    public LineTemplateStoreOptions UseJsonFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        JsonFilePath = filePath;
        return this;
    }
}
