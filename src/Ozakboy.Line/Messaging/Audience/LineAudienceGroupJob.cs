using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Audience;

/// <summary>
/// 對受眾做過的一次成員變更工作。
/// One membership job run against an audience.
/// </summary>
/// <remarks>
/// 對應 LINE 受眾查詢回應 <c>jobs</c> 陣列裡的每一項:<c>audienceGroupJobId</c>、<c>audienceGroupId</c>、
/// <c>description</c>、<c>type</c>、<c>status</c>、<c>failedType</c>、<c>audienceCount</c>、
/// <c>created</c>、<c>jobStatus</c>。
/// Maps to each item of the <c>jobs</c> array in LINE's audience read response: <c>audienceGroupJobId</c>,
/// <c>audienceGroupId</c>, <c>description</c>, <c>type</c>, <c>status</c>, <c>failedType</c>,
/// <c>audienceCount</c>, <c>created</c>, <c>jobStatus</c>.
/// </remarks>
public sealed class LineAudienceGroupJob
{
    /// <summary>
    /// 工作識別碼。
    /// The job identifier.
    /// </summary>
    [JsonPropertyName("audienceGroupJobId")]
    public long AudienceGroupJobId { get; init; }

    /// <summary>
    /// 所屬受眾的識別碼。
    /// The audience it belongs to.
    /// </summary>
    [JsonPropertyName("audienceGroupId")]
    public long AudienceGroupId { get; init; }

    /// <summary>
    /// 工作說明(建立或加成員時填的 <c>uploadDescription</c>)。
    /// The job description, the <c>uploadDescription</c> given when creating or adding.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// 工作型別,目前只有 <c>DIFF_ADD</c>。
    /// The job type, currently only <c>DIFF_ADD</c>.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// 工作狀態:<c>QUEUED</c>、<c>WORKING</c>、<c>FINISHED</c> 或 <c>FAILED</c>。
    /// The job status: <c>QUEUED</c>, <c>WORKING</c>, <c>FINISHED</c> or <c>FAILED</c>.
    /// </summary>
    [JsonPropertyName("jobStatus")]
    public string JobStatus { get; init; } = string.Empty;

    /// <summary>
    /// 舊欄位,內容與 <see cref="JobStatus"/> 相同;LINE 仍然回傳。
    /// The older field carrying the same value as <see cref="JobStatus"/>, which LINE still returns.
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    /// <summary>
    /// 失敗原因;沒失敗時為 <see langword="null"/>。
    /// Why it failed, or <see langword="null"/> when it did not.
    /// </summary>
    [JsonPropertyName("failedType")]
    public string? FailedType { get; init; }

    /// <summary>
    /// 這次工作處理的人數。
    /// The number of members this job handled.
    /// </summary>
    [JsonPropertyName("audienceCount")]
    public long? AudienceCount { get; init; }

    /// <summary>
    /// 建立時間(Unix 秒)。
    /// When it was created, in Unix seconds.
    /// </summary>
    [JsonPropertyName("created")]
    public long Created { get; init; }
}
