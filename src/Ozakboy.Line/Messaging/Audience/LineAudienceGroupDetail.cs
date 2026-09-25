using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Audience;

/// <summary>
/// 單一受眾的完整資料:受眾本身與對它做過的工作。
/// One audience in full: the audience itself and the jobs run against it.
/// </summary>
/// <remarks>
/// 對應 LINE 受眾查詢端點的回應:<c>audienceGroup</c> 與 <c>jobs</c>。
/// Maps to the response of LINE's audience read endpoint: <c>audienceGroup</c> and <c>jobs</c>.
/// </remarks>
public sealed class LineAudienceGroupDetail
{
    /// <summary>
    /// 受眾本身。
    /// The audience itself.
    /// </summary>
    [JsonPropertyName("audienceGroup")]
    public LineAudienceGroup AudienceGroup { get; init; } = new();

    /// <summary>
    /// 對它做過的成員變更工作;沒有時為空清單。
    /// The membership jobs run against it, or an empty list.
    /// </summary>
    [JsonPropertyName("jobs")]
    public IReadOnlyList<LineAudienceGroupJob> Jobs { get; init; } = [];
}
