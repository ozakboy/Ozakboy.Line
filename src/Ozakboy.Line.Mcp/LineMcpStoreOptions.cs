namespace Ozakboy.Line.Mcp;

/// <summary>
/// 決定待發佇列用哪一種儲存體。
/// Chooses which outbox store gets registered.
/// </summary>
public sealed class LineMcpStoreOptions
{
    /// <summary>
    /// 待發佇列 JSON 檔的路徑;為 <see langword="null"/> 時用記憶體儲存體。
    /// The outbox JSON file's path, or <see langword="null"/> for the in-memory store.
    /// </summary>
    public string? OutboxJsonFilePath { get; private set; }

    /// <summary>
    /// 改用 JSON 檔存待發佇列。
    /// Switches the outbox to a JSON file.
    /// </summary>
    /// <param name="filePath">檔案路徑。The file path.</param>
    /// <returns>同一個設定物件,方便串接。The same options object, for chaining.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="filePath"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="filePath"/> is <see langword="null"/> or blank.
    /// </exception>
    /// <remarks>
    /// 正式環境請一定要設。記憶體版的佇列在重啟時會把待審項目與已送紀錄一起丟掉,
    /// 而稽核紀錄的價值正好建立在它不會消失上。
    /// Set this in production. The in-memory queue loses the pending items and the record of what was sent on
    /// every restart, and an audit trail is worth something precisely because it does not disappear.
    /// </remarks>
    public LineMcpStoreOptions UseJsonFileOutbox(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        OutboxJsonFilePath = filePath;
        return this;
    }
}
