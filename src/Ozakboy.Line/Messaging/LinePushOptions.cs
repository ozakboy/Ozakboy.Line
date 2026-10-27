namespace Ozakboy.Line.Messaging;

/// <summary>
/// 推播、群發與廣播的可選參數。
/// The optional parameters for push, multicast, and broadcast.
/// </summary>
public sealed class LinePushOptions
{
    /// <summary>
    /// 是否不發出通知(訊息照送,只是使用者的裝置不響)。
    /// Whether to suppress the notification; the message is still delivered, the device just stays quiet.
    /// </summary>
    public bool NotificationDisabled { get; set; }

    /// <summary>
    /// 重試鍵。設定後這次請求會帶上 <c>X-Line-Retry-Key</c>,並允許管線重試。
    /// The retry key. When set, the request carries <c>X-Line-Retry-Key</c> and the pipeline may retry it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>沒有重試鍵的推播絕不重試。</b>推播是 POST,重送一次就是多發一則 —— 對使用者來說是同一則通知響兩次,
    /// 而第一次的「失敗」很可能只是回應在路上掉了,訊息其實已經送出去了。
    /// <b>A push without a retry key is never retried.</b> A push is a POST, and resending it sends a second
    /// message: the user's phone buzzes twice, and the first "failure" was quite possibly just a lost response to
    /// a message that did go out.
    /// </para>
    /// <para>
    /// 帶上重試鍵之後就安全了:LINE 以這個鍵去重,同一個鍵重送只會送達一次。鍵必須是同一次邏輯推播固定的值 ——
    /// 每次重試各產生一個新的 UUID,等於沒有去重。
    /// With a retry key it becomes safe: LINE deduplicates on the key, so resending with the same key still
    /// delivers once. The key must stay fixed for one logical push — a fresh UUID per attempt deduplicates
    /// nothing.
    /// </para>
    /// </remarks>
    public Guid? RetryKey { get; set; }

    /// <summary>
    /// 統計單位(<c>customAggregationUnits</c>):讓 LINE 把這次發送歸入指定單位,之後可用
    /// <see cref="ILineMessagingClient.GetMessageEventInsightByUnitAsync"/> 依單位彙總成效。
    /// The aggregation units (<c>customAggregationUnits</c>): LINE files this send under the named unit, so its
    /// performance can be rolled up later with <see cref="ILineMessagingClient.GetMessageEventInsightByUnitAsync"/>.
    /// </summary>
    /// <remarks>
    /// 目前 LINE 每次請求最多 <b>1</b> 個單位,名稱 ≤ 30 字且只能是英數與底線;違反時在送出前以
    /// <see cref="LineErrorCodes.InvalidAggregationUnit"/> 失敗,一個請求都不送。不設定或空清單時不寫這個欄位。
    /// LINE currently allows at most <b>1</b> unit per request, with a name of up to 30 characters made of
    /// letters, digits and underscores; a violation fails with <see cref="LineErrorCodes.InvalidAggregationUnit"/>
    /// before anything is sent. When unset or empty the field is not written.
    /// </remarks>
#pragma warning disable CA2227 // 與其他選項一樣讓呼叫端用物件初始設定式整個指定。Assigned whole through an object initialiser like the other options.
    public IList<string>? CustomAggregationUnits { get; set; }
#pragma warning restore CA2227
}
