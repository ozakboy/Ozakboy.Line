using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Insight;

/// <summary>
/// 某一天各種管道的訊息傳送數。
/// The number of messages delivered on one day, by channel.
/// </summary>
/// <remarks>
/// 對應 LINE 訊息傳送數端點的回應:<c>status</c>、<c>broadcast</c>、<c>targeting</c>、<c>autoResponse</c>、
/// <c>welcomeResponse</c>、<c>chat</c>、<c>apiBroadcast</c>、<c>apiPush</c>、<c>apiMulticast</c>、
/// <c>apiNarrowcast</c>、<c>apiReply</c>。<see cref="Status"/> 不是 <see cref="StatusReady"/> 時,
/// 數字欄位全部是 <see langword="null"/> —— 那不是零,是「還沒統計」或「查不到」。
/// Maps to the response of LINE's message delivery endpoint: <c>status</c>, <c>broadcast</c>,
/// <c>targeting</c>, <c>autoResponse</c>, <c>welcomeResponse</c>, <c>chat</c>, <c>apiBroadcast</c>,
/// <c>apiPush</c>, <c>apiMulticast</c>, <c>apiNarrowcast</c>, <c>apiReply</c>. When <see cref="Status"/> is
/// not <see cref="StatusReady"/> every number is <see langword="null"/>: not zero, but not counted yet or not
/// available.
/// </remarks>
public sealed class LineMessageDeliveryInsight
{
    /// <summary>統計已就緒。The figures are ready.</summary>
    public const string StatusReady = "ready";

    /// <summary>還沒統計完(當天或前一天)。Not counted yet (today or yesterday).</summary>
    public const string StatusUnready = "unready";

    /// <summary>查不到(太久以前,或帳號當時不存在)。Not available (too long ago, or the account did not exist).</summary>
    public const string StatusOutOfService = "out_of_service";

    /// <summary>
    /// 狀態:<see cref="StatusReady"/>、<see cref="StatusUnready"/> 或 <see cref="StatusOutOfService"/>。
    /// The status: <see cref="StatusReady"/>, <see cref="StatusUnready"/> or <see cref="StatusOutOfService"/>.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>從 LINE Official Account Manager 發的廣播。Broadcasts from LINE Official Account Manager.</summary>
    [JsonPropertyName("broadcast")]
    public long? Broadcast { get; init; }

    /// <summary>從 LINE Official Account Manager 發的分眾訊息。Targeted messages from LINE Official Account Manager.</summary>
    [JsonPropertyName("targeting")]
    public long? Targeting { get; init; }

    /// <summary>LINE Official Account Manager 的自動回應。Auto responses from LINE Official Account Manager.</summary>
    [JsonPropertyName("autoResponse")]
    public long? AutoResponse { get; init; }

    /// <summary>LINE Official Account Manager 的歡迎訊息。Greeting messages from LINE Official Account Manager.</summary>
    [JsonPropertyName("welcomeResponse")]
    public long? WelcomeResponse { get; init; }

    /// <summary>聊天模式下人工回的訊息。Messages sent by hand in chat mode.</summary>
    [JsonPropertyName("chat")]
    public long? Chat { get; init; }

    /// <summary>經 Messaging API 的廣播。Broadcasts through the Messaging API.</summary>
    [JsonPropertyName("apiBroadcast")]
    public long? ApiBroadcast { get; init; }

    /// <summary>經 Messaging API 的推播。Pushes through the Messaging API.</summary>
    [JsonPropertyName("apiPush")]
    public long? ApiPush { get; init; }

    /// <summary>經 Messaging API 的群發。Multicasts through the Messaging API.</summary>
    [JsonPropertyName("apiMulticast")]
    public long? ApiMulticast { get; init; }

    /// <summary>經 Messaging API 的分眾推播。Narrowcasts through the Messaging API.</summary>
    [JsonPropertyName("apiNarrowcast")]
    public long? ApiNarrowcast { get; init; }

    /// <summary>經 Messaging API 的回覆。Replies through the Messaging API.</summary>
    [JsonPropertyName("apiReply")]
    public long? ApiReply { get; init; }
}
