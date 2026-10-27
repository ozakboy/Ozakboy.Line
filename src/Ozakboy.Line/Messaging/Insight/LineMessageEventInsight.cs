using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Insight;

/// <summary>
/// 一次發送(或一個統計單位)的互動統計。
/// The engagement statistics of one send, or of one aggregation unit.
/// </summary>
/// <remarks>
/// 對應 LINE 的 <c>overview</c>、<c>messages[]</c> 與 <c>clicks[]</c>,依請求識別碼查與依統計單位查共用這個型別。
/// 發送後要等一陣子才有數字,尚未就緒時 <see cref="Messages"/> 與 <see cref="Clicks"/> 為空。
/// Maps to LINE's <c>overview</c>, <c>messages[]</c> and <c>clicks[]</c>; the lookup by request id and the one by
/// aggregation unit share this type. The figures take a while to appear after a send, and until then
/// <see cref="Messages"/> and <see cref="Clicks"/> are empty.
/// </remarks>
public sealed class LineMessageEventInsight
{
    /// <summary>
    /// 總覽;LINE 沒有回時為 <see langword="null"/>。
    /// The overview, or <see langword="null"/> when LINE did not send one.
    /// </summary>
    [JsonPropertyName("overview")]
    public LineMessageEventOverview? Overview { get; init; }

    /// <summary>
    /// 各則訊息的統計。
    /// The per-message statistics.
    /// </summary>
    [JsonPropertyName("messages")]
    public IReadOnlyList<LineMessageEventMessage> Messages { get; init; } = [];

    /// <summary>
    /// 各個網址的點擊統計。
    /// The per-URL click statistics.
    /// </summary>
    [JsonPropertyName("clicks")]
    public IReadOnlyList<LineMessageEventClick> Clicks { get; init; } = [];
}
