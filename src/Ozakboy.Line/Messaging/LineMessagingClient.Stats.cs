using System.Globalization;
using System.Text.Json;
using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Messaging.Insight;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// <see cref="LineMessagingClient"/> 的投遞統計、單則訊息互動統計與統計單位端點。
/// The delivery count, per-message engagement and aggregation unit endpoints of <see cref="LineMessagingClient"/>.
/// </summary>
public sealed partial class LineMessagingClient
{
    /// <inheritdoc />
    public Task<Result<LineMessageDeliveryCount>> GetMessageDeliveryCountAsync(
        LineMessageDeliveryType type,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var segment = type switch
        {
            LineMessageDeliveryType.Reply => "reply",
            LineMessageDeliveryType.Push => "push",
            LineMessageDeliveryType.Multicast => "multicast",
            LineMessageDeliveryType.Broadcast => "broadcast",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "未定義的投遞管道。An undefined delivery channel."),
        };

        return !Options.IsConfigured
            ? Task.FromResult(NotConfigured<LineMessageDeliveryCount>())
            : LineHttp.SendForJsonAsync<LineMessageDeliveryCount>(
                _http,
                Get(LineEndpoints.MessageDeliveryBase + segment + "?date=" + FormatInsightDate(date)),
                cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<LineMessageEventInsight>> GetMessageEventInsightAsync(string requestId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);

        return !Options.IsConfigured
            ? Task.FromResult(NotConfigured<LineMessageEventInsight>())
            : LineHttp.SendForJsonAsync<LineMessageEventInsight>(
                _http,
                Get(LineEndpoints.InsightMessageEvent + "?requestId=" + Uri.EscapeDataString(requestId)),
                cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<LineMessageEventInsight>> GetMessageEventInsightByUnitAsync(
        string customAggregationUnit,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customAggregationUnit);

        if (!Options.IsConfigured)
        {
            return Task.FromResult(NotConfigured<LineMessageEventInsight>());
        }

        var unit = ValidateAggregationUnitName(customAggregationUnit);
        if (unit.IsFailure)
        {
            return Task.FromResult(unit.ToFailure<LineMessageEventInsight>());
        }

        var days = to.DayNumber - from.DayNumber;
        if (days < 0 || days > LineMessagingLimits.MaxAggregationRangeDays)
        {
            return Task.FromResult(Result.Failure<LineMessageEventInsight>(Error.Validation(
                LineErrorCodes.InvalidDateRange,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"統計區間的迄日不得早於起日,且最多比起日晚 {LineMessagingLimits.MaxAggregationRangeDays} 天,這次是 {FormatInsightDate(from)} 到 {FormatInsightDate(to)}。The end date must not precede the start and may be at most {LineMessagingLimits.MaxAggregationRangeDays} days after it; {FormatInsightDate(from)} to {FormatInsightDate(to)} was supplied."))));
        }

        var uri = LineEndpoints.InsightMessageEventAggregation
            + "?customAggregationUnit=" + Uri.EscapeDataString(customAggregationUnit)
            + "&from=" + FormatInsightDate(from)
            + "&to=" + FormatInsightDate(to);

        return LineHttp.SendForJsonAsync<LineMessageEventInsight>(_http, Get(uri), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<int>> GetAggregationUnitUsageAsync(CancellationToken cancellationToken = default)
    {
        if (!Options.IsConfigured)
        {
            return NotConfigured<int>();
        }

        var usage = await LineHttp
            .SendForJsonAsync<LineAggregationUnitUsageResponse>(_http, Get(LineEndpoints.MessageAggregationInfo), cancellationToken)
            .ConfigureAwait(false);

        return usage.IsFailure ? usage.ToFailure<int>() : Result.Success(usage.GetValueOrThrow().NumOfCustomAggregationUnits);
    }

    /// <inheritdoc />
    public Task<Result<LineAggregationUnitNamesPage>> GetAggregationUnitNamesAsync(
        int? limit = null,
        string? start = null,
        CancellationToken cancellationToken = default)
    {
        if (!Options.IsConfigured)
        {
            return Task.FromResult(NotConfigured<LineAggregationUnitNamesPage>());
        }

        if (limit is < 1 or > LineMessagingLimits.MaxAggregationUnitNamesPerPage)
        {
            return Task.FromResult(Result.Failure<LineAggregationUnitNamesPage>(Error.Validation(
                LineErrorCodes.InvalidPageSize,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"統計單位清單每頁筆數需為 1 到 {LineMessagingLimits.MaxAggregationUnitNamesPerPage},這次是 {limit}。The aggregation unit list's page size must be between 1 and {LineMessagingLimits.MaxAggregationUnitNamesPerPage}; {limit} was supplied."))));
        }

        var query = new List<string>(2);
        if (limit is { } size)
        {
            query.Add("limit=" + size.ToString(CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(start))
        {
            query.Add("start=" + Uri.EscapeDataString(start));
        }

        var uri = query.Count == 0
            ? LineEndpoints.MessageAggregationList
            : LineEndpoints.MessageAggregationList + "?" + string.Join('&', query);

        return LineHttp.SendForJsonAsync<LineAggregationUnitNamesPage>(_http, Get(uri), cancellationToken);
    }

    /// <summary>
    /// 檢查請求要帶的統計單位清單:數量與每個名稱。
    /// Checks the aggregation unit list a request is to carry: the count and every name.
    /// </summary>
    /// <param name="units">統計單位,可為 <see langword="null"/> 或空。The units, possibly <see langword="null"/> or empty.</param>
    /// <returns>合法(含沒設定)時為成功。Success when valid, including when unset.</returns>
    private static Result ValidateAggregationUnits(IList<string>? units)
    {
        if (units is null || units.Count == 0)
        {
            return Result.Success();
        }

        if (units.Count > LineMessagingLimits.MaxAggregationUnitsPerRequest)
        {
            return Error.Validation(
                LineErrorCodes.InvalidAggregationUnit,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"一次請求最多帶 {LineMessagingLimits.MaxAggregationUnitsPerRequest} 個統計單位,這次是 {units.Count} 個。One request carries at most {LineMessagingLimits.MaxAggregationUnitsPerRequest} aggregation unit; {units.Count} were supplied."));
        }

        for (var index = 0; index < units.Count; index++)
        {
            var name = ValidateAggregationUnitName(units[index]);
            if (name.IsFailure)
            {
                return name;
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// 檢查一個統計單位名稱:非空、≤ 30 字、只含英數與底線。
    /// Checks one aggregation unit name: non-empty, at most 30 characters, only letters, digits and underscores.
    /// </summary>
    /// <param name="name">名稱。The name.</param>
    /// <returns>合法時為成功。Success when valid.</returns>
    /// <remarks>
    /// 字元比對刻意只收 ASCII:<see cref="char.IsLetterOrDigit(char)"/> 會放行中文與全形字,而 LINE 拒收它們。
    /// The character test accepts ASCII only on purpose: <see cref="char.IsLetterOrDigit(char)"/> would let
    /// Chinese and full-width characters through, which LINE rejects.
    /// </remarks>
    private static Result ValidateAggregationUnitName(string? name)
    {
        if (!string.IsNullOrEmpty(name)
            && name.Length <= LineMessagingLimits.MaxAggregationUnitNameLength
            && name.All(static c => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_'))
        {
            return Result.Success();
        }

        return Error.Validation(
            LineErrorCodes.InvalidAggregationUnit,
            string.Create(
                CultureInfo.InvariantCulture,
                $"統計單位名稱需為 1 到 {LineMessagingLimits.MaxAggregationUnitNameLength} 個英文字母、數字或底線。An aggregation unit name must be 1 to {LineMessagingLimits.MaxAggregationUnitNameLength} letters, digits or underscores."));
    }

    /// <summary>
    /// 寫出 <c>customAggregationUnits</c>,只在有值時寫。
    /// Writes <c>customAggregationUnits</c>, and only when there is one.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    /// <param name="units">統計單位。The units.</param>
    private static void WriteCustomAggregationUnits(Utf8JsonWriter writer, IList<string>? units)
    {
        if (units is null || units.Count == 0)
        {
            return;
        }

        writer.WriteStartArray("customAggregationUnits");
        for (var index = 0; index < units.Count; index++)
        {
            writer.WriteStringValue(units[index]);
        }

        writer.WriteEndArray();
    }
}
