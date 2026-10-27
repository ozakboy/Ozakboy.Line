namespace Ozakboy.Line.Messaging.Narrowcast;

/// <summary>
/// 分眾推播的統計單位欄位(<see cref="LineNarrowcastOptions"/> 的一部分)。
/// The aggregation unit field of a narrowcast (part of <see cref="LineNarrowcastOptions"/>).
/// </summary>
public sealed partial class LineNarrowcastOptions
{
    /// <summary>
    /// 統計單位(<c>customAggregationUnits</c>),語意與限制同 <see cref="LinePushOptions.CustomAggregationUnits"/>。
    /// The aggregation units (<c>customAggregationUnits</c>), with the same meaning and limits as
    /// <see cref="LinePushOptions.CustomAggregationUnits"/>.
    /// </summary>
#pragma warning disable CA2227 // 讓呼叫端用物件初始設定式整個指定。Assigned whole through an object initialiser.
    public IList<string>? CustomAggregationUnits { get; set; }
#pragma warning restore CA2227
}
