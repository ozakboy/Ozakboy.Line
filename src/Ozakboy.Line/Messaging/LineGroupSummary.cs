using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// 群組的摘要:識別碼、名稱與群組圖片。
/// A group's summary: its id, name and picture.
/// </summary>
/// <remarks>
/// 對應 LINE 群組摘要端點的回應(<c>groupId</c>、<c>groupName</c>、<c>pictureUrl</c>)。
/// 聊天室(room)沒有對應的端點:聊天室沒有名稱也沒有圖片。
/// Maps to the response of LINE's group summary endpoint (<c>groupId</c>, <c>groupName</c>,
/// <c>pictureUrl</c>). Rooms have no counterpart: a room has neither a name nor a picture.
/// </remarks>
public sealed class LineGroupSummary
{
    /// <summary>
    /// 群組識別碼。
    /// The group identifier.
    /// </summary>
    [JsonPropertyName("groupId")]
    public string GroupId { get; init; } = string.Empty;

    /// <summary>
    /// 群組名稱。
    /// The group name.
    /// </summary>
    [JsonPropertyName("groupName")]
    public string GroupName { get; init; } = string.Empty;

    /// <summary>
    /// 群組圖片位址;沒有設定時為 <see langword="null"/>。
    /// The group picture address, or <see langword="null"/> when none is set.
    /// </summary>
    [JsonPropertyName("pictureUrl")]
    public string? PictureUrl { get; init; }
}
