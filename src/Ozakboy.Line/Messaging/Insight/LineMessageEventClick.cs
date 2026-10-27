using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Insight;

/// <summary>
/// 訊息內一個網址的點擊統計。
/// The click statistics of one URL in a message.
/// </summary>
/// <remarks>
/// 對應 LINE 的 <c>clicks[]</c>。數值欄位為 <see langword="null"/> 時表示不適用或被隱藏,不是零。
/// Maps to LINE's <c>clicks[]</c>. A <see langword="null"/> number means not applicable or hidden, not zero.
/// </remarks>
public sealed class LineMessageEventClick
{
    /// <summary>
    /// 網址所在訊息的序號(從 1 起算)。
    /// The sequence number of the message holding the URL, starting at 1.
    /// </summary>
    [JsonPropertyName("seq")]
    public int Seq { get; init; }

    /// <summary>
    /// 被點的網址。
    /// The clicked URL.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    /// <summary>
    /// 點擊次數。
    /// The click count.
    /// </summary>
    [JsonPropertyName("click")]
    public long? Click { get; init; }

    /// <summary>
    /// 點過這個網址的不重複人數。
    /// The number of unique people who clicked this URL.
    /// </summary>
    [JsonPropertyName("uniqueClick")]
    public long? UniqueClick { get; init; }

    /// <summary>
    /// 點過這次發送中任一網址的不重複人數。
    /// The number of unique people who clicked any URL of this send.
    /// </summary>
    [JsonPropertyName("uniqueClickOfRequest")]
    public long? UniqueClickOfRequest { get; init; }
}
