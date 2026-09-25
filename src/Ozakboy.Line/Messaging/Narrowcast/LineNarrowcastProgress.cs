using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Narrowcast;

/// <summary>
/// 一次分眾推播的進度。
/// The progress of one narrowcast.
/// </summary>
/// <remarks>
/// 對應 LINE 進度端點的回應:<c>phase</c>、<c>successCount</c>、<c>failureCount</c>、<c>targetCount</c>、
/// <c>failedDescription</c>、<c>errorCode</c>、<c>acceptedTime</c>、<c>completedTime</c>。
/// 分眾推播是<b>非同步</b>的:送出端點回 202 只代表收下了,實際有沒有送、送給幾個人,要用這個型別看。
/// Maps to the response of LINE's progress endpoint: <c>phase</c>, <c>successCount</c>, <c>failureCount</c>,
/// <c>targetCount</c>, <c>failedDescription</c>, <c>errorCode</c>, <c>acceptedTime</c>, <c>completedTime</c>.
/// A narrowcast is <b>asynchronous</b>: the send endpoint's 202 means only that it was accepted, and whether it
/// went out and to how many people is what this type reports.
/// </remarks>
public sealed class LineNarrowcastProgress
{
    /// <summary>尚未開始。Not started yet.</summary>
    public const string PhaseWaiting = "waiting";

    /// <summary>送出中。Sending.</summary>
    public const string PhaseSending = "sending";

    /// <summary>已完成。Finished.</summary>
    public const string PhaseSucceeded = "succeeded";

    /// <summary>失敗。Failed.</summary>
    public const string PhaseFailed = "failed";

    /// <summary>
    /// 目前階段:<see cref="PhaseWaiting"/>、<see cref="PhaseSending"/>、<see cref="PhaseSucceeded"/> 或
    /// <see cref="PhaseFailed"/>。
    /// The current phase: <see cref="PhaseWaiting"/>, <see cref="PhaseSending"/>, <see cref="PhaseSucceeded"/>
    /// or <see cref="PhaseFailed"/>.
    /// </summary>
    [JsonPropertyName("phase")]
    public string Phase { get; init; } = string.Empty;

    /// <summary>
    /// 已成功送達的人數;還沒開始送時為 <see langword="null"/>。
    /// The number delivered so far, or <see langword="null"/> before sending starts.
    /// </summary>
    [JsonPropertyName("successCount")]
    public long? SuccessCount { get; init; }

    /// <summary>
    /// 送達失敗的人數;還沒開始送時為 <see langword="null"/>。
    /// The number that failed, or <see langword="null"/> before sending starts.
    /// </summary>
    [JsonPropertyName("failureCount")]
    public long? FailureCount { get; init; }

    /// <summary>
    /// 目標人數;還沒算出來時為 <see langword="null"/>。
    /// The target count, or <see langword="null"/> before it is known.
    /// </summary>
    [JsonPropertyName("targetCount")]
    public long? TargetCount { get; init; }

    /// <summary>
    /// 失敗原因的說明;只在 <see cref="PhaseFailed"/> 時有值。
    /// A description of the failure, present only in <see cref="PhaseFailed"/>.
    /// </summary>
    [JsonPropertyName("failedDescription")]
    public string? FailedDescription { get; init; }

    /// <summary>
    /// 失敗代碼:1 為受眾不可用(狀態不是 READY 或人數不足),2 為內部錯誤;只在失敗時有值。
    /// The failure code: 1 when the audience was unusable (not READY, or too few members), 2 for an internal
    /// error; present only on failure.
    /// </summary>
    [JsonPropertyName("errorCode")]
    public int? ErrorCode { get; init; }

    /// <summary>
    /// LINE 收下請求的時間(ISO 8601)。
    /// When LINE accepted the request, as ISO 8601.
    /// </summary>
    [JsonPropertyName("acceptedTime")]
    public string AcceptedTime { get; init; } = string.Empty;

    /// <summary>
    /// 完成的時間(ISO 8601);未完成時為 <see langword="null"/>。
    /// When it completed, as ISO 8601, or <see langword="null"/> while unfinished.
    /// </summary>
    [JsonPropertyName("completedTime")]
    public string? CompletedTime { get; init; }
}
