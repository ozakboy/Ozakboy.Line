using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Audience;

/// <summary>
/// 建立上傳型受眾的回應。
/// The response to creating an upload audience.
/// </summary>
/// <remarks>
/// 對應 LINE 的回應欄位:<c>audienceGroupId</c>、<c>createRoute</c>、<c>type</c>、<c>description</c>、
/// <c>created</c>、<c>permission</c>、<c>expireTimestamp</c>、<c>isIfaAudience</c>。
/// 剛建立的受眾狀態是 <c>IN_PROGRESS</c>,要等 LINE 處理完(用 <c>GetAudienceGroupAsync</c> 看)才能推播。
/// Maps to LINE's response fields: <c>audienceGroupId</c>, <c>createRoute</c>, <c>type</c>,
/// <c>description</c>, <c>created</c>, <c>permission</c>, <c>expireTimestamp</c>, <c>isIfaAudience</c>. A
/// freshly created audience is <c>IN_PROGRESS</c>, and can be narrowcast to only once LINE has finished with it
/// (which <c>GetAudienceGroupAsync</c> reports).
/// </remarks>
public sealed class LineAudienceGroupCreated
{
    /// <summary>
    /// 受眾識別碼。
    /// The audience identifier.
    /// </summary>
    [JsonPropertyName("audienceGroupId")]
    public long AudienceGroupId { get; init; }

    /// <summary>
    /// 建立來源,這裡永遠是 <c>MESSAGING_API</c>。
    /// Where it was created, always <c>MESSAGING_API</c> here.
    /// </summary>
    [JsonPropertyName("createRoute")]
    public string? CreateRoute { get; init; }

    /// <summary>
    /// 受眾型別,這裡永遠是 <c>UPLOAD</c>。
    /// The audience type, always <c>UPLOAD</c> here.
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
    /// 建立時間(Unix 秒)。
    /// When it was created, in Unix seconds.
    /// </summary>
    [JsonPropertyName("created")]
    public long Created { get; init; }

    /// <summary>
    /// 權限。
    /// The permission.
    /// </summary>
    [JsonPropertyName("permission")]
    public string? Permission { get; init; }

    /// <summary>
    /// 到期時間(Unix 秒)。
    /// When it expires, in Unix seconds.
    /// </summary>
    [JsonPropertyName("expireTimestamp")]
    public long? ExpireTimestamp { get; init; }

    /// <summary>
    /// 成員是否以廣告識別碼指定;本套件只送使用者識別碼,所以是 <see langword="false"/>。
    /// Whether members are named by advertising id; this package sends user ids only, so this is
    /// <see langword="false"/>.
    /// </summary>
    [JsonPropertyName("isIfaAudience")]
    public bool IsIfaAudience { get; init; }
}
