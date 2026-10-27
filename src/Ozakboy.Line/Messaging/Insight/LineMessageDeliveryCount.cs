using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Insight;

/// <summary>
/// 某一天某個管道(回覆 / 推播 / 群發 / 廣播)的投遞數。
/// The number of messages delivered through one channel on one day (reply, push, multicast or broadcast).
/// </summary>
/// <remarks>
/// 對應 LINE 的 <c>status</c> 與 <c>success</c>。<see cref="Status"/> 不是
/// <see cref="LineMessageDeliveryCountStatus.Ready"/> 時 <see cref="Success"/> 為 <see langword="null"/> ——
/// 那不是零,是「還沒統計」或「查不到」。
/// Maps to LINE's <c>status</c> and <c>success</c>. When <see cref="Status"/> is not
/// <see cref="LineMessageDeliveryCountStatus.Ready"/>, <see cref="Success"/> is <see langword="null"/>: not zero,
/// but not counted yet or not available.
/// </remarks>
public sealed class LineMessageDeliveryCount
{
    /// <summary>
    /// 統計狀態。
    /// The status of the figure.
    /// </summary>
    [JsonPropertyName("status")]
    public LineMessageDeliveryCountStatus Status { get; init; }

    /// <summary>
    /// 成功送達的訊息數;狀態不是就緒時為 <see langword="null"/>。
    /// The number of messages delivered successfully, or <see langword="null"/> unless the status is ready.
    /// </summary>
    [JsonPropertyName("success")]
    public long? Success { get; init; }
}
