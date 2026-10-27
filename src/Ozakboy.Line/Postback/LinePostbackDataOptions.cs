namespace Ozakboy.Line.Postback;

/// <summary>
/// postback 超長資料暫存的設定:保存多久、能存多長,以及用哪一種儲存體。
/// Settings for long postback data staging: how long to keep it, how long it may be, and which store to use.
/// </summary>
/// <remarks>
/// 什麼儲存體都不選就是記憶體儲存體 —— 行程重啟後,已經發出去的按鈕會因代碼查不到而回
/// <see cref="LineErrorCodes.PostbackExpired"/>。要讓按鈕活過重啟,請 <see cref="UseJsonFile(string)"/>
/// 或自己實作 <see cref="ILinePostbackDataStore"/>。
/// Choosing no store means the in-memory one — after a restart, buttons already sent fail with
/// <see cref="LineErrorCodes.PostbackExpired"/> because their tokens are gone. To let buttons outlive a restart,
/// call <see cref="UseJsonFile(string)"/> or implement <see cref="ILinePostbackDataStore"/> yourself.
/// </remarks>
public sealed class LinePostbackDataOptions
{
    /// <summary>
    /// 呼叫端沒指定存活時間時的預設值;預設 1 天。
    /// The time to live when the caller does not give one; one day by default.
    /// </summary>
    public TimeSpan DefaultTimeToLive { get; set; } = TimeSpan.FromDays(1);

    /// <summary>
    /// 可暫存的資料最大字元數;超過回 <see cref="LineErrorCodes.PostbackDataTooLong"/>。預設
    /// <see cref="LineMessagingLimits.DefaultMaxStoredPostbackDataLength"/>。
    /// The most characters of data that may be staged; more fails with
    /// <see cref="LineErrorCodes.PostbackDataTooLong"/>. Defaults to
    /// <see cref="LineMessagingLimits.DefaultMaxStoredPostbackDataLength"/>.
    /// </summary>
    public int MaxDataLength { get; set; } = LineMessagingLimits.DefaultMaxStoredPostbackDataLength;

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
    public LinePostbackDataOptions UseJsonFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        JsonFilePath = filePath;
        return this;
    }

    /// <summary>
    /// 檢查設定本身是否合理,供儲存體建構時使用。
    /// Checks that the settings themselves make sense, for the stores to call on construction.
    /// </summary>
    internal void EnsureValid()
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(DefaultTimeToLive, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxDataLength, 1);
    }
}
