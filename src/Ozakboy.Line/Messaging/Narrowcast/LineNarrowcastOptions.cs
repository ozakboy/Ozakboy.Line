using System.Text.Json;

namespace Ozakboy.Line.Messaging.Narrowcast;

/// <summary>
/// 分眾推播的可選參數:收件對象、屬性篩選、人數上限、通知與重試鍵。
/// The optional parameters of a narrowcast: the recipients, the demographic filter, the cap, the notification
/// and the retry key.
/// </summary>
/// <remarks>
/// <para>
/// 對應 LINE 請求內容的 <c>recipient</c>、<c>filter.demographic</c>、<c>limit.max</c>、
/// <c>limit.upToRemainingQuota</c> 與 <c>notificationDisabled</c>。<c>recipient</c> 與 <c>demographic</c>
/// 以 <see cref="JsonElement"/> 接受而不完整建模:兩者都是可巢狀的運算樹(<c>and</c> / <c>or</c> /
/// <c>not</c>),而 LINE 還在為它們增加葉節點型別。收件對象常用的形狀由
/// <see cref="LineNarrowcastRecipient"/> 組。
/// Maps to <c>recipient</c>, <c>filter.demographic</c>, <c>limit.max</c>, <c>limit.upToRemainingQuota</c> and
/// <c>notificationDisabled</c> in LINE's request body. <c>recipient</c> and <c>demographic</c> are taken as a
/// <see cref="JsonElement"/> rather than modelled in full: both are nestable operator trees (<c>and</c> /
/// <c>or</c> / <c>not</c>) whose leaf types LINE keeps adding to. The common recipient shapes are built by
/// <see cref="LineNarrowcastRecipient"/>.
/// </para>
/// <para>
/// <see cref="Recipient"/> 與 <see cref="Demographic"/> 都不給時,LINE 會送給<b>所有好友</b> ——
/// 這與廣播的差別只在計費與進度查詢。
/// With neither <see cref="Recipient"/> nor <see cref="Demographic"/> set, LINE sends to <b>every friend</b>;
/// the difference from a broadcast is then only billing and progress tracking.
/// </para>
/// </remarks>
public sealed class LineNarrowcastOptions
{
    /// <summary>
    /// 收件對象的運算樹;不設定時不限對象。
    /// The recipient tree, or unset for no restriction.
    /// </summary>
    public JsonElement? Recipient { get; set; }

    /// <summary>
    /// 屬性篩選(性別、年齡、地區、作業系統、加好友時間)的運算樹;不設定時不篩選。
    /// The demographic filter tree (gender, age, area, OS, subscription period), or unset for no filter.
    /// </summary>
    /// <remarks>
    /// 形狀例如 <c>{"type":"operator","and":[{"type":"gender","oneOf":["male"]},{"type":"age","gte":"age_20","lt":"age_30"}]}</c>。
    /// A shape such as <c>{"type":"operator","and":[{"type":"gender","oneOf":["male"]},{"type":"age","gte":"age_20","lt":"age_30"}]}</c>.
    /// </remarks>
    public JsonElement? Demographic { get; set; }

    /// <summary>
    /// 最多送給幾個人;不設定時不限。
    /// The maximum number of recipients, or unset for no cap.
    /// </summary>
    public int? MaxRecipients { get; set; }

    /// <summary>
    /// 是否以本月剩餘額度為上限;不設定時沿用 LINE 的預設(不設上限)。
    /// Whether to cap at this month's remaining quota, or unset for LINE's default (no cap).
    /// </summary>
    public bool? UpToRemainingQuota { get; set; }

    /// <summary>
    /// 是否不發出通知。
    /// Whether to suppress the notification.
    /// </summary>
    public bool NotificationDisabled { get; set; }

    /// <summary>
    /// 重試鍵,語意與 <see cref="LinePushOptions.RetryKey"/> 相同:有值才重試。
    /// The retry key, with the same meaning as <see cref="LinePushOptions.RetryKey"/>: retried only when set.
    /// </summary>
    public Guid? RetryKey { get; set; }
}
