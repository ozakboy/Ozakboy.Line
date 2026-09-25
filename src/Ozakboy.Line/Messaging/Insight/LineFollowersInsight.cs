using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Insight;

/// <summary>
/// 某一天的好友數。
/// The number of followers on one day.
/// </summary>
/// <remarks>
/// 對應 LINE 好友數端點的回應:<c>status</c>、<c>followers</c>、<c>targetedReaches</c>、<c>blocks</c>。
/// 狀態值與 <see cref="LineMessageDeliveryInsight"/> 相同;不是 ready 時三個數字都是 <see langword="null"/>。
/// Maps to the response of LINE's followers endpoint: <c>status</c>, <c>followers</c>,
/// <c>targetedReaches</c>, <c>blocks</c>. The status values are those of
/// <see cref="LineMessageDeliveryInsight"/>; when not ready all three numbers are <see langword="null"/>.
/// </remarks>
public sealed class LineFollowersInsight
{
    /// <summary>
    /// 狀態,值同 <see cref="LineMessageDeliveryInsight.Status"/>。
    /// The status, with the same values as <see cref="LineMessageDeliveryInsight.Status"/>.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// 累計加好友的人數(含已封鎖)。
    /// The cumulative number of people who added the account, blocks included.
    /// </summary>
    [JsonPropertyName("followers")]
    public long? Followers { get; init; }

    /// <summary>
    /// 可觸及的人數:有好友且屬性可辨識的那些。
    /// The reachable count: friends whose attributes can be identified.
    /// </summary>
    [JsonPropertyName("targetedReaches")]
    public long? TargetedReaches { get; init; }

    /// <summary>
    /// 封鎖的人數。
    /// The number who blocked the account.
    /// </summary>
    [JsonPropertyName("blocks")]
    public long? Blocks { get; init; }
}
