using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Messaging.Insight;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// <see cref="ILineMessagingClient"/> 的投遞統計、單則訊息互動統計與統計單位部分。
/// The delivery count, per-message engagement and aggregation unit part of <see cref="ILineMessagingClient"/>.
/// </summary>
public partial interface ILineMessagingClient
{
    /// <summary>
    /// 查某一天某個管道(回覆 / 推播 / 群發 / 廣播)送達的訊息數。
    /// Gets the number of messages delivered through one channel (reply, push, multicast or broadcast) on one day.
    /// </summary>
    /// <param name="type">管道。The channel.</param>
    /// <param name="date">日期。LINE 以日本標準時間(JST,UTC+9)計日,所以台灣時間凌晨 0 到 1 點算的是前一天。The date; LINE counts days in Japan Standard Time (JST, UTC+9), so the hour between midnight and 1 a.m. in Taiwan belongs to the previous day.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>
    /// 投遞數;<see cref="LineMessageDeliveryCount.Status"/> 不是 Ready 時 <see cref="LineMessageDeliveryCount.Success"/> 為 <see langword="null"/>。
    /// The delivery count; <see cref="LineMessageDeliveryCount.Success"/> is <see langword="null"/> unless the status is Ready.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="type"/> 不是已定義的值時擲出。
    /// Thrown when <paramref name="type"/> is not a defined value.
    /// </exception>
    Task<Result<LineMessageDeliveryCount>> GetMessageDeliveryCountAsync(
        LineMessageDeliveryType type,
        DateOnly date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 查某一次發送(以請求識別碼指定)的互動統計。
    /// Gets the engagement statistics of one send, selected by its request id.
    /// </summary>
    /// <param name="requestId">發送時 LINE 回的請求識別碼(<c>X-Line-Request-Id</c>)。The request id LINE returned at send time (<c>X-Line-Request-Id</c>).</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>
    /// 互動統計;LINE 對不適用的欄位回 <see langword="null"/>,那不是零。
    /// The statistics; LINE answers <see langword="null"/> for fields that do not apply, which is not zero.
    /// </returns>
    Task<Result<LineMessageEventInsight>> GetMessageEventInsightAsync(string requestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查某個統計單位在一段期間內的互動統計。
    /// Gets the engagement statistics of one aggregation unit over a period.
    /// </summary>
    /// <param name="customAggregationUnit">統計單位名稱(發送時放在 <see cref="LinePushOptions.CustomAggregationUnits"/>)。The aggregation unit's name, as set in <see cref="LinePushOptions.CustomAggregationUnits"/> at send time.</param>
    /// <param name="from">起日(含)。LINE 以 JST 計日。The first day, inclusive; LINE counts days in JST.</param>
    /// <param name="to">迄日(含)。須不早於起日,且最多比起日晚 30 天。The last day, inclusive; not before <paramref name="from"/> and at most 30 days after it.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>
    /// 互動統計;區間不合法時以 <see cref="LineErrorCodes.InvalidDateRange"/> 失敗且不送出,統計單位名稱不合法時以
    /// <see cref="LineErrorCodes.InvalidAggregationUnit"/> 失敗且不送出。
    /// The statistics; an invalid range fails with <see cref="LineErrorCodes.InvalidDateRange"/> and an invalid
    /// unit name with <see cref="LineErrorCodes.InvalidAggregationUnit"/>, both without sending anything.
    /// </returns>
    Task<Result<LineMessageEventInsight>> GetMessageEventInsightByUnitAsync(
        string customAggregationUnit,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 查本月已使用的統計單位數。
    /// Gets the number of aggregation units used this month.
    /// </summary>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>已使用的單位數。The number of units used.</returns>
    Task<Result<int>> GetAggregationUnitUsageAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出本月使用過的統計單位名稱。
    /// Lists the aggregation unit names used this month.
    /// </summary>
    /// <param name="limit">每頁筆數,1 到 100;不給時由 LINE 決定。The page size, 1 to 100, or LINE's default when omitted.</param>
    /// <param name="start">上一頁回的 <see cref="LineAggregationUnitNamesPage.Next"/>。The <see cref="LineAggregationUnitNamesPage.Next"/> of the previous page.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>
    /// 一頁名稱;<paramref name="limit"/> 超出範圍時以 <see cref="LineErrorCodes.InvalidPageSize"/> 失敗且不送出。
    /// One page of names; a <paramref name="limit"/> out of range fails with <see cref="LineErrorCodes.InvalidPageSize"/>
    /// without sending anything.
    /// </returns>
    Task<Result<LineAggregationUnitNamesPage>> GetAggregationUnitNamesAsync(
        int? limit = null,
        string? start = null,
        CancellationToken cancellationToken = default);
}
