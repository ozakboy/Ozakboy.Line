using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Insight;

/// <summary>
/// 一次發送中某一則訊息的互動統計。
/// The engagement statistics of one message within a send.
/// </summary>
/// <remarks>
/// 對應 LINE 的 <c>messages[]</c>。數值欄位為 <see langword="null"/> 時表示不適用或被隱藏,不是零。
/// Maps to LINE's <c>messages[]</c>. A <see langword="null"/> number means not applicable or hidden, not zero.
/// </remarks>
public sealed class LineMessageEventMessage
{
    /// <summary>
    /// 訊息在這次發送中的序號(從 1 起算)。
    /// The message's sequence number within the send, starting at 1.
    /// </summary>
    [JsonPropertyName("seq")]
    public int Seq { get; init; }

    /// <summary>
    /// 曝光數(看過的次數)。
    /// The impression count.
    /// </summary>
    [JsonPropertyName("impression")]
    public long? Impression { get; init; }

    /// <summary>
    /// 開始播放數。
    /// The number of plays started.
    /// </summary>
    [JsonPropertyName("mediaPlayed")]
    public long? MediaPlayed { get; init; }

    /// <summary>
    /// 播放到 25% 的次數。
    /// The number of plays that reached 25%.
    /// </summary>
    [JsonPropertyName("mediaPlayed25Percent")]
    public long? MediaPlayed25Percent { get; init; }

    /// <summary>
    /// 播放到 50% 的次數。
    /// The number of plays that reached 50%.
    /// </summary>
    [JsonPropertyName("mediaPlayed50Percent")]
    public long? MediaPlayed50Percent { get; init; }

    /// <summary>
    /// 播放到 75% 的次數。
    /// The number of plays that reached 75%.
    /// </summary>
    [JsonPropertyName("mediaPlayed75Percent")]
    public long? MediaPlayed75Percent { get; init; }

    /// <summary>
    /// 播放到 100% 的次數。
    /// The number of plays that reached 100%.
    /// </summary>
    [JsonPropertyName("mediaPlayed100Percent")]
    public long? MediaPlayed100Percent { get; init; }

    /// <summary>
    /// 開始播放的不重複人數。
    /// The number of unique people who started playing.
    /// </summary>
    [JsonPropertyName("uniqueMediaPlayed")]
    public long? UniqueMediaPlayed { get; init; }

    /// <summary>
    /// 播放到 25% 的不重複人數。
    /// The number of unique people who reached 25%.
    /// </summary>
    [JsonPropertyName("uniqueMediaPlayed25Percent")]
    public long? UniqueMediaPlayed25Percent { get; init; }

    /// <summary>
    /// 播放到 50% 的不重複人數。
    /// The number of unique people who reached 50%.
    /// </summary>
    [JsonPropertyName("uniqueMediaPlayed50Percent")]
    public long? UniqueMediaPlayed50Percent { get; init; }

    /// <summary>
    /// 播放到 75% 的不重複人數。
    /// The number of unique people who reached 75%.
    /// </summary>
    [JsonPropertyName("uniqueMediaPlayed75Percent")]
    public long? UniqueMediaPlayed75Percent { get; init; }

    /// <summary>
    /// 播放到 100% 的不重複人數。
    /// The number of unique people who reached 100%.
    /// </summary>
    [JsonPropertyName("uniqueMediaPlayed100Percent")]
    public long? UniqueMediaPlayed100Percent { get; init; }
}
