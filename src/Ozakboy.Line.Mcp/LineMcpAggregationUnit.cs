using System.Text.RegularExpressions;
using Ozakboy.Line.Messaging;

namespace Ozakboy.Line.Mcp;

/// <summary>
/// 送出類工具的「統計單位」小工具:提出時先檢查名稱,送出時轉成 <see cref="LinePushOptions"/>。
/// Aggregation-unit helpers for the sending tools: the name is checked when proposed, and turned into
/// <see cref="LinePushOptions"/> when sent.
/// </summary>
/// <remarks>
/// 待審模式下,項目要等人核准才會真的送出;若名稱不合規格卻先排進佇列,審核的人核准了一個
/// 註定被核心擋下的項目。所以在提出的當下就用同一條規則(1 到 30 個英數字元與底線)檢查。
/// In review mode an item only goes out once someone approves it; queueing a bad name would have a reviewer
/// approve something the core is bound to refuse. The same rule — one to thirty letters, digits and underscores —
/// is therefore checked at proposal time. The core checks again at send time.
/// </remarks>
internal static partial class LineMcpAggregationUnit
{
    /// <summary>
    /// 待發內容裡存放統計單位的欄位名。
    /// The payload field that carries the aggregation unit.
    /// </summary>
    internal const string PayloadField = "customAggregationUnit";

    [GeneratedRegex("^[A-Za-z0-9_]{1,30}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex NamePattern();

    /// <summary>
    /// 檢查統計單位名稱。
    /// Checks an aggregation unit name.
    /// </summary>
    /// <param name="unit">名稱;<see langword="null"/> 或空白視為沒有設定。The name; <see langword="null"/> or blank means not set.</param>
    /// <returns>合規(或沒設定)時為 <see langword="null"/>,否則為錯誤訊息。<see langword="null"/> when it conforms (or is not set), otherwise the error message.</returns>
    internal static string? Validate(string? unit) =>
        string.IsNullOrWhiteSpace(unit) || NamePattern().IsMatch(unit)
            ? null
            : "customAggregationUnit 只能是 1 到 30 個英文字母、數字或底線(LINE 統計單位的規格)。";

    /// <summary>
    /// 轉成送出用的選項。
    /// Turns the name into send options.
    /// </summary>
    /// <param name="unit">名稱。The name.</param>
    /// <returns>沒有設定時為 <see langword="null"/>。<see langword="null"/> when not set.</returns>
    internal static LinePushOptions? ToOptions(string? unit) =>
        string.IsNullOrWhiteSpace(unit)
            ? null
            : new LinePushOptions { CustomAggregationUnits = [unit] };
}
