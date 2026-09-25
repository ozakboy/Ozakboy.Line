using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// 群組與聊天室成員清單端點的回應形狀,只用於反序列化。
/// The group and room member list endpoints' response shape, used only for deserialisation.
/// </summary>
/// <remarks>
/// 欄位名是 <c>memberIds</c>,與好友清單的 <c>userIds</c> 不同;靠同一個模型讀兩邊會有一邊永遠是空的。
/// The field is <c>memberIds</c>, unlike the follower list's <c>userIds</c>; reading both through one model
/// leaves one of them always empty.
/// </remarks>
internal sealed class LineMemberIdsResponse
{
    /// <summary>
    /// 這一頁的成員識別碼。
    /// The member identifiers on this page.
    /// </summary>
    [JsonPropertyName("memberIds")]
    public IReadOnlyList<string>? MemberIds { get; init; }

    /// <summary>
    /// 下一頁的游標。
    /// The next page's cursor.
    /// </summary>
    [JsonPropertyName("next")]
    public string? Next { get; init; }
}
