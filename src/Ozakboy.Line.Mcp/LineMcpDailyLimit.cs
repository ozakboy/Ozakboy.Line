using System.Globalization;
using Ozakboy.Line.Mcp.Outbox;

namespace Ozakboy.Line.Mcp;

/// <summary>
/// 每日建立上限的計數。
/// Counting against the daily creation cap.
/// </summary>
/// <remarks>
/// 「今日」依 <see cref="LineMcpOptions.TimeZoneId"/> 的當地日期算,不是 UTC。台灣時間的話,
/// 用 UTC 會在早上八點換日 —— 而「昨天的額度為什麼還沒回來」不是任何人想在早上查的問題。
/// "Today" follows the local date in <see cref="LineMcpOptions.TimeZoneId"/> rather than UTC. In Taiwan, UTC
/// would turn the day over at eight in the morning, and "why has yesterday's allowance not come back" is not a
/// question anyone wants to investigate before lunch.
/// </remarks>
internal static class LineMcpDailyLimit
{
    /// <summary>
    /// 算出「今日」在絕對時間上的區間。
    /// Works out today's range in absolute time.
    /// </summary>
    /// <param name="options">設定。The settings.</param>
    /// <param name="now">目前時間。The current time.</param>
    /// <returns>起(含)與迄(不含)。The start, inclusive, and the end, exclusive.</returns>
    /// <remarks>
    /// 時區識別碼找不到時退回 UTC 而不是擲例外。一個打錯的時區不該讓所有工具都失效,
    /// 而退回 UTC 的後果只是換日時間不合預期 —— 比整組工具不能用小得多。
    /// An unknown time zone identifier falls back to UTC rather than throwing. One mistyped zone should not
    /// disable every tool, and the consequence of the fallback is a day boundary in the wrong place — far smaller
    /// than the whole tool set being unusable.
    /// </remarks>
    internal static (DateTimeOffset From, DateTimeOffset To) Today(LineMcpOptions options, DateTimeOffset now)
    {
        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            zone = TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            zone = TimeZoneInfo.Utc;
        }

        var local = TimeZoneInfo.ConvertTime(now, zone);
        var startOfDay = new DateTimeOffset(local.Year, local.Month, local.Day, 0, 0, 0, local.Offset);

        return (startOfDay, startOfDay.AddDays(1));
    }

    /// <summary>
    /// 檢查今日還有沒有額度。
    /// Checks whether there is any allowance left today.
    /// </summary>
    /// <param name="store">待發佇列。The outbox.</param>
    /// <param name="options">設定。The settings.</param>
    /// <param name="now">目前時間。The current time.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>
    /// 今日已建立的筆數,以及還有沒有額度。
    /// How many were created today, and whether there is allowance left.
    /// </returns>
    internal static async Task<(int Used, bool HasAllowance)> CheckAsync(
        ILineMcpOutboxStore store,
        LineMcpOptions options,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var (from, to) = Today(options, now);
        var used = await store.CountCreatedBetweenAsync(from, to, cancellationToken).ConfigureAwait(false);

        return (used, used < options.MaxSendRequestsPerDay);
    }

    /// <summary>
    /// 「今日額度用完」的說明文字。
    /// The sentence explaining that today's allowance is spent.
    /// </summary>
    /// <param name="used">今日已建立的筆數。How many were created today.</param>
    /// <param name="options">設定。The settings.</param>
    /// <returns>說明文字。The sentence.</returns>
    /// <remarks>
    /// 訊息裡寫清楚「已經幾筆、上限幾筆、下一步該做什麼」。只說「超過上限」的話,
    /// AI 多半會原樣再試一次,而下一次一樣會被擋 —— 那是一個不會自己停下來的迴圈。
    /// The message says how many, what the cap is, and what to do next. Told only that a limit was exceeded, an
    /// AI generally tries the same call again and is refused again — a loop that does not stop on its own.
    /// </remarks>
    internal static string Message(int used, LineMcpOptions options) => string.Create(
        CultureInfo.InvariantCulture,
        $"今日({options.TimeZoneId})已建立 {used} 筆待發項目,已達每日上限 {options.MaxSendRequestsPerDay} 筆。請明日再試,或先請宿主處理既有的待審項目;重試同一個呼叫不會有不同結果。");
}
