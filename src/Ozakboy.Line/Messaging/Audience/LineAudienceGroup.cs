using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Audience;

/// <summary>
/// 一個受眾(audience group)。
/// One audience group.
/// </summary>
/// <remarks>
/// 對應 LINE 受眾端點回的 <c>audienceGroup</c> 物件與清單裡的每一項:<c>audienceGroupId</c>、
/// <c>type</c>、<c>description</c>、<c>status</c>、<c>audienceCount</c>、<c>created</c>、
/// <c>permission</c>、<c>createRoute</c>、<c>isIfaAudience</c>、<c>expireTimestamp</c>、<c>failedType</c>。
/// 識別碼是<b>數字</b>而不是字串,與 LINE 其他識別碼不同。
/// Maps to the <c>audienceGroup</c> object of LINE's audience endpoints and to each item of the list:
/// <c>audienceGroupId</c>, <c>type</c>, <c>description</c>, <c>status</c>, <c>audienceCount</c>,
/// <c>created</c>, <c>permission</c>, <c>createRoute</c>, <c>isIfaAudience</c>, <c>expireTimestamp</c>,
/// <c>failedType</c>. The id is a <b>number</b>, unlike LINE's other identifiers.
/// </remarks>
public sealed class LineAudienceGroup
{
    /// <summary>
    /// 受眾識別碼。
    /// The audience identifier.
    /// </summary>
    [JsonPropertyName("audienceGroupId")]
    public long AudienceGroupId { get; init; }

    /// <summary>
    /// 受眾型別:<c>UPLOAD</c>、<c>CLICK</c>、<c>IMP</c>、<c>CHAT_TAG</c>、<c>FRIEND_PATH</c> 等。
    /// The audience type: <c>UPLOAD</c>, <c>CLICK</c>, <c>IMP</c>, <c>CHAT_TAG</c>, <c>FRIEND_PATH</c> and so on.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// 受眾名稱。
    /// The audience name.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 狀態:<c>IN_PROGRESS</c>、<c>READY</c>、<c>FAILED</c> 或 <c>EXPIRED</c>。只有 <c>READY</c> 能用於分眾推播。
    /// The status: <c>IN_PROGRESS</c>, <c>READY</c>, <c>FAILED</c> or <c>EXPIRED</c>. Only <c>READY</c> can be
    /// narrowcast to.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// 成員人數;還在處理時為 <see langword="null"/>。
    /// The member count, or <see langword="null"/> while still processing.
    /// </summary>
    [JsonPropertyName("audienceCount")]
    public long? AudienceCount { get; init; }

    /// <summary>
    /// 建立時間(Unix 秒)。
    /// When it was created, in Unix seconds.
    /// </summary>
    [JsonPropertyName("created")]
    public long Created { get; init; }

    /// <summary>
    /// 權限:<c>READ</c> 或 <c>READ_WRITE</c>。
    /// The permission: <c>READ</c> or <c>READ_WRITE</c>.
    /// </summary>
    [JsonPropertyName("permission")]
    public string? Permission { get; init; }

    /// <summary>
    /// 建立來源:<c>MESSAGING_API</c>、<c>OA_MANAGER</c>、<c>ADS_MANAGER</c> 等。
    /// Where it was created: <c>MESSAGING_API</c>, <c>OA_MANAGER</c>, <c>ADS_MANAGER</c> and so on.
    /// </summary>
    [JsonPropertyName("createRoute")]
    public string? CreateRoute { get; init; }

    /// <summary>
    /// 成員是否以廣告識別碼(IFA)而非使用者識別碼指定。
    /// Whether members are named by advertising id (IFA) rather than user id.
    /// </summary>
    [JsonPropertyName("isIfaAudience")]
    public bool IsIfaAudience { get; init; }

    /// <summary>
    /// 到期時間(Unix 秒);不會到期時為 <see langword="null"/>。
    /// When it expires, in Unix seconds, or <see langword="null"/> when it never does.
    /// </summary>
    [JsonPropertyName("expireTimestamp")]
    public long? ExpireTimestamp { get; init; }

    /// <summary>
    /// 失敗原因:<c>AUDIENCE_GROUP_AUDIENCE_INSUFFICIENT</c> 或 <c>INTERNAL_ERROR</c>;沒失敗時為 <see langword="null"/>。
    /// Why it failed: <c>AUDIENCE_GROUP_AUDIENCE_INSUFFICIENT</c> or <c>INTERNAL_ERROR</c>; <see langword="null"/>
    /// when it did not.
    /// </summary>
    [JsonPropertyName("failedType")]
    public string? FailedType { get; init; }
}
