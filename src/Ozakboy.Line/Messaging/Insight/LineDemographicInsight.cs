using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Insight;

/// <summary>
/// 好友的屬性分布:性別、年齡、地區、作業系統與加好友時間。
/// The friend demographics: gender, age, area, OS and subscription period.
/// </summary>
/// <remarks>
/// 對應 LINE 好友屬性端點的回應:<c>available</c>、<c>genders</c>、<c>ages</c>、<c>areas</c>、
/// <c>appTypes</c>、<c>subscriptionPeriods</c>。每一組都是「類別 + 百分比」的清單,類別字串
/// (<c>unknown</c>、<c>from50</c>、<c>over365days</c> 等)照 LINE 原樣保留,不轉列舉 ——
/// LINE 增加類別時才不會在反序列化那一步就被擋掉。<see cref="Available"/> 為 <see langword="false"/> 時
/// (好友不足 20 人)所有清單都是空的。
/// Maps to the response of LINE's demographic endpoint: <c>available</c>, <c>genders</c>, <c>ages</c>,
/// <c>areas</c>, <c>appTypes</c>, <c>subscriptionPeriods</c>. Each is a list of category plus percentage, with
/// the category strings (<c>unknown</c>, <c>from50</c>, <c>over365days</c> and so on) kept as LINE sends them
/// rather than turned into enums, so a category LINE adds is not refused at deserialisation. When
/// <see cref="Available"/> is <see langword="false"/> (fewer than 20 friends) every list is empty.
/// </remarks>
public sealed class LineDemographicInsight
{
    /// <summary>
    /// 是否有統計資料;好友不足 20 人時為 <see langword="false"/>。
    /// Whether figures are available; <see langword="false"/> with fewer than 20 friends.
    /// </summary>
    [JsonPropertyName("available")]
    public bool Available { get; init; }

    /// <summary>
    /// 性別分布。
    /// The gender breakdown.
    /// </summary>
    [JsonPropertyName("genders")]
    public IReadOnlyList<LineDemographicGender> Genders { get; init; } = [];

    /// <summary>
    /// 年齡分布。
    /// The age breakdown.
    /// </summary>
    [JsonPropertyName("ages")]
    public IReadOnlyList<LineDemographicAge> Ages { get; init; } = [];

    /// <summary>
    /// 地區分布。
    /// The area breakdown.
    /// </summary>
    [JsonPropertyName("areas")]
    public IReadOnlyList<LineDemographicArea> Areas { get; init; } = [];

    /// <summary>
    /// 作業系統分布。
    /// The OS breakdown.
    /// </summary>
    [JsonPropertyName("appTypes")]
    public IReadOnlyList<LineDemographicAppType> AppTypes { get; init; } = [];

    /// <summary>
    /// 加好友時間長短的分布。
    /// The subscription period breakdown.
    /// </summary>
    [JsonPropertyName("subscriptionPeriods")]
    public IReadOnlyList<LineDemographicSubscriptionPeriod> SubscriptionPeriods { get; init; } = [];
}
