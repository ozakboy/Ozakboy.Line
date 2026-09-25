using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// 好友清單端點的回應形狀,只用於反序列化。
/// The follower list endpoint's response shape, used only for deserialisation.
/// </summary>
internal sealed class LineFollowerIdsResponse
{
    /// <summary>
    /// 這一頁的使用者識別碼。
    /// The user identifiers on this page.
    /// </summary>
    [JsonPropertyName("userIds")]
    public IReadOnlyList<string>? UserIds { get; init; }

    /// <summary>
    /// 下一頁的游標。
    /// The next page's cursor.
    /// </summary>
    [JsonPropertyName("next")]
    public string? Next { get; init; }
}
