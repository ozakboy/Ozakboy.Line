using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Insight;

/// <summary>
/// 一次發送的互動總覽。
/// The engagement overview of one send.
/// </summary>
/// <remarks>
/// 對應 LINE 的 <c>overview</c>。每個數值欄位都可能是 <see langword="null"/>:LINE 對不適用的欄位(例如沒有影片的訊息
/// 的播放數、或不足 20 人時為保護隱私而隱藏的數字)回 null,那不是零。以統計單位彙總的回應沒有
/// <see cref="RequestId"/>、<see cref="Timestamp"/> 與 <see cref="Delivered"/>。
/// Maps to LINE's <c>overview</c>. Every numeric field may be <see langword="null"/>: LINE answers null for a
/// field that does not apply (media plays of a message without media, or a figure hidden for privacy), and that
/// is not zero. The response rolled up by aggregation unit carries no <see cref="RequestId"/>,
/// <see cref="Timestamp"/> or <see cref="Delivered"/>.
/// </remarks>
public sealed class LineMessageEventOverview
{
    /// <summary>
    /// 請求識別碼;以統計單位彙總時為 <see langword="null"/>。
    /// The request id, or <see langword="null"/> when rolled up by aggregation unit.
    /// </summary>
    [JsonPropertyName("requestId")]
    public string? RequestId { get; init; }

    /// <summary>
    /// 發送時間(Unix 時間,秒);以統計單位彙總時為 <see langword="null"/>。
    /// When it was sent, as Unix time in seconds, or <see langword="null"/> when rolled up by aggregation unit.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long? Timestamp { get; init; }

    /// <summary>
    /// 送達的人數。
    /// The number of people it was delivered to.
    /// </summary>
    [JsonPropertyName("delivered")]
    public long? Delivered { get; init; }

    /// <summary>
    /// 看過訊息的不重複人數。
    /// The number of unique people who saw the message.
    /// </summary>
    [JsonPropertyName("uniqueImpression")]
    public long? UniqueImpression { get; init; }

    /// <summary>
    /// 點過訊息內網址的不重複人數。
    /// The number of unique people who clicked a URL in the message.
    /// </summary>
    [JsonPropertyName("uniqueClick")]
    public long? UniqueClick { get; init; }

    /// <summary>
    /// 開始播放影片或音訊的不重複人數。
    /// The number of unique people who started playing the video or audio.
    /// </summary>
    [JsonPropertyName("uniqueMediaPlayed")]
    public long? UniqueMediaPlayed { get; init; }

    /// <summary>
    /// 播放到 100% 的不重複人數。
    /// The number of unique people who played it through to 100%.
    /// </summary>
    [JsonPropertyName("uniqueMediaPlayed100Percent")]
    public long? UniqueMediaPlayed100Percent { get; init; }
}
