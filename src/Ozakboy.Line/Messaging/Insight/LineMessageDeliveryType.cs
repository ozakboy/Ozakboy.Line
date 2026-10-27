namespace Ozakboy.Line.Messaging.Insight;

/// <summary>
/// 投遞數統計的管道。
/// The channel of a delivery count.
/// </summary>
public enum LineMessageDeliveryType
{
    /// <summary>回覆訊息。Reply messages.</summary>
    Reply = 0,

    /// <summary>推播訊息。Push messages.</summary>
    Push = 1,

    /// <summary>群發訊息。Multicast messages.</summary>
    Multicast = 2,

    /// <summary>廣播訊息。Broadcast messages.</summary>
    Broadcast = 3,
}
