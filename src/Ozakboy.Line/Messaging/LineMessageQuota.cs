using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// 本月的推播額度。
/// This month's push quota.
/// </summary>
public sealed class LineMessageQuota
{
    /// <summary>
    /// 額度類型:<c>none</c> 表示不限量,<c>limited</c> 表示有上限。
    /// The quota type: <c>none</c> for unlimited, <c>limited</c> when there is a cap.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// 上限則數;<see cref="Type"/> 為 <c>none</c> 時為 <see langword="null"/>。
    /// The cap, or <see langword="null"/> when <see cref="Type"/> is <c>none</c>.
    /// </summary>
    [JsonPropertyName("value")]
    public long? Value { get; init; }
}
