using Ozakboy.Core.Abstractions;

namespace Ozakboy.Line.Postback;

/// <summary>
/// 兩個內建儲存體共用的檢查與錯誤。
/// The checks and errors shared by the two built-in stores.
/// </summary>
internal static class LinePostbackDataStoreSupport
{
    /// <summary>
    /// 檢查要暫存的資料與存活時間,算出到期時間。
    /// Checks the data and time to live to stage, and works out the expiry.
    /// </summary>
    /// <param name="data">資料。The data.</param>
    /// <param name="timeToLive">存活時間或 <see langword="null"/>。The time to live, or <see langword="null"/>.</param>
    /// <param name="options">設定。The options.</param>
    /// <param name="now">現在。Now.</param>
    /// <returns>到期時間,或 <see cref="LineErrorCodes.PostbackDataTooLong"/> 失敗。The expiry, or a <see cref="LineErrorCodes.PostbackDataTooLong"/> failure.</returns>
    /// <exception cref="ArgumentException"><paramref name="data"/> 為 null 或空白。<paramref name="data"/> is null or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeToLive"/> 不是正數。<paramref name="timeToLive"/> is not positive.</exception>
    internal static Result<DateTimeOffset> Prepare(string data, TimeSpan? timeToLive, LinePostbackDataOptions options, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(data);

        var lifetime = timeToLive ?? options.DefaultTimeToLive;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);

        if (data.Length > options.MaxDataLength)
        {
            return Result.Failure<DateTimeOffset>(Error.Validation(
                LineErrorCodes.PostbackDataTooLong,
                $"要暫存的 postback 資料長 {data.Length} 字,超過上限 {options.MaxDataLength}。The postback data to stage is {data.Length} characters long, over the limit of {options.MaxDataLength}."));
        }

        return Result.Success(now + lifetime);
    }

    /// <summary>
    /// 找不到或過期的失敗。
    /// The failure for missing or expired.
    /// </summary>
    /// <returns>錯誤。The error.</returns>
    internal static Error Expired() =>
        Error.NotFound(
            LineErrorCodes.PostbackExpired,
            "暫存的 postback 資料找不到或已過期,請使用者重新操作。The staged postback data is missing or has expired; ask the user to repeat the action.");
}
