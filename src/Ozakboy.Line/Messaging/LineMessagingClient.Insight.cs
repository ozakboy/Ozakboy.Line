using System.Globalization;
using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Messaging.Insight;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// <see cref="LineMessagingClient"/> 的成效洞察端點。
/// The insight endpoints of <see cref="LineMessagingClient"/>.
/// </summary>
public sealed partial class LineMessagingClient
{
    /// <inheritdoc />
    public Task<Result<LineMessageDeliveryInsight>> GetMessageDeliveryInsightAsync(DateOnly date, CancellationToken cancellationToken = default) =>
        !Options.IsConfigured
            ? Task.FromResult(NotConfigured<LineMessageDeliveryInsight>())
            : LineHttp.SendForJsonAsync<LineMessageDeliveryInsight>(
                _http,
                Get(LineEndpoints.InsightMessageDelivery + "?date=" + FormatInsightDate(date)),
                cancellationToken);

    /// <inheritdoc />
    public Task<Result<LineFollowersInsight>> GetFollowersInsightAsync(DateOnly date, CancellationToken cancellationToken = default) =>
        !Options.IsConfigured
            ? Task.FromResult(NotConfigured<LineFollowersInsight>())
            : LineHttp.SendForJsonAsync<LineFollowersInsight>(
                _http,
                Get(LineEndpoints.InsightFollowers + "?date=" + FormatInsightDate(date)),
                cancellationToken);

    /// <inheritdoc />
    public Task<Result<LineDemographicInsight>> GetDemographicInsightAsync(CancellationToken cancellationToken = default) =>
        !Options.IsConfigured
            ? Task.FromResult(NotConfigured<LineDemographicInsight>())
            : LineHttp.SendForJsonAsync<LineDemographicInsight>(_http, Get(LineEndpoints.InsightDemographic), cancellationToken);

    /// <summary>
    /// 把日期寫成 LINE 洞察端點要的 <c>yyyyMMdd</c>。
    /// Formats a date as the <c>yyyyMMdd</c> LINE's insight endpoints ask for.
    /// </summary>
    /// <param name="date">日期。The date.</param>
    /// <returns>八位數字。Eight digits.</returns>
    /// <remarks>
    /// 固定用不變文化:預設文化在某些機器上會把日期寫成民國年或加上分隔符號,而 LINE 只認這八位數字。
    /// The invariant culture is used on purpose: on some machines the default culture writes the year in the ROC
    /// calendar or adds separators, and LINE accepts only these eight digits.
    /// </remarks>
    private static string FormatInsightDate(DateOnly date) => date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
}
