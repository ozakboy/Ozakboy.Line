using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Audience;

/// <summary>
/// 受眾清單的一頁。
/// One page of the audience list.
/// </summary>
/// <remarks>
/// 對應 LINE 受眾清單端點的回應:<c>audienceGroups</c>、<c>hasNextPage</c>、<c>totalCount</c>、
/// <c>readWriteAudienceGroupTotalCount</c>、<c>page</c>、<c>size</c>。這個端點以<b>頁碼</b>分頁,
/// 與好友清單的游標分頁不同。
/// Maps to the response of LINE's audience list endpoint: <c>audienceGroups</c>, <c>hasNextPage</c>,
/// <c>totalCount</c>, <c>readWriteAudienceGroupTotalCount</c>, <c>page</c>, <c>size</c>. It pages by
/// <b>page number</b>, unlike the follower list's cursor.
/// </remarks>
public sealed class LineAudienceGroupPage
{
    /// <summary>
    /// 這一頁的受眾;沒有時為空清單。
    /// The audiences on this page, or an empty list.
    /// </summary>
    [JsonPropertyName("audienceGroups")]
    public IReadOnlyList<LineAudienceGroup> AudienceGroups { get; init; } = [];

    /// <summary>
    /// 還有下一頁。
    /// There is a next page.
    /// </summary>
    [JsonPropertyName("hasNextPage")]
    public bool HasNextPage { get; init; }

    /// <summary>
    /// 受眾總數。
    /// The total number of audiences.
    /// </summary>
    [JsonPropertyName("totalCount")]
    public long TotalCount { get; init; }

    /// <summary>
    /// 權限為 <c>READ_WRITE</c> 的受眾總數。
    /// The number of audiences with <c>READ_WRITE</c> permission.
    /// </summary>
    [JsonPropertyName("readWriteAudienceGroupTotalCount")]
    public long ReadWriteAudienceGroupTotalCount { get; init; }

    /// <summary>
    /// 這是第幾頁(從 1 起算)。
    /// This page's number, counting from 1.
    /// </summary>
    [JsonPropertyName("page")]
    public int Page { get; init; }

    /// <summary>
    /// 每頁筆數。
    /// The page size.
    /// </summary>
    [JsonPropertyName("size")]
    public int Size { get; init; }
}
