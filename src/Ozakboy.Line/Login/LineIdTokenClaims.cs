using System.Text.Json.Serialization;

namespace Ozakboy.Line.Login;

/// <summary>
/// id_token 內容的原始形狀,只用於反序列化。
/// The raw shape of an id_token's contents, used only for deserialisation.
/// </summary>
/// <remarks>
/// 公開型別 <see cref="LineIdTokenPayload"/> 把 <c>exp</c> 與 <c>iat</c> 表達成
/// <see cref="DateTimeOffset"/>,而 JWT 裡它們是 Unix 秒數。轉換放在這一層,公開型別就不必為了
/// 「配合 JSON 的形狀」而把時間留成 <see cref="long"/> —— 那會讓每個呼叫端各自轉一次,也各自有一次
/// 忘記是秒不是毫秒的機會。
/// The public <see cref="LineIdTokenPayload"/> expresses <c>exp</c> and <c>iat</c> as
/// <see cref="DateTimeOffset"/>, whereas a JWT carries them as Unix seconds. Converting at this layer keeps the
/// public type from having to leave times as <see cref="long"/> to match the JSON, which would make every caller
/// convert once and give every caller one chance to forget that these are seconds rather than milliseconds.
/// </remarks>
internal sealed class LineIdTokenClaims
{
    /// <summary>發行者。The issuer.</summary>
    [JsonPropertyName("iss")]
    public string? Issuer { get; init; }

    /// <summary>使用者識別碼。The user identifier.</summary>
    [JsonPropertyName("sub")]
    public string? Subject { get; init; }

    /// <summary>對象 channel id。The audience channel id.</summary>
    [JsonPropertyName("aud")]
    public string? Audience { get; init; }

    /// <summary>到期時間的 Unix 秒數。The expiry as Unix seconds.</summary>
    [JsonPropertyName("exp")]
    public long ExpiresAt { get; init; }

    /// <summary>核發時間的 Unix 秒數。The issue time as Unix seconds.</summary>
    [JsonPropertyName("iat")]
    public long IssuedAt { get; init; }

    /// <summary>一次性隨機值。The one-time value.</summary>
    [JsonPropertyName("nonce")]
    public string? Nonce { get; init; }

    /// <summary>認證方式。The authentication methods.</summary>
    [JsonPropertyName("amr")]
    public IReadOnlyList<string>? Amr { get; init; }

    /// <summary>顯示名稱。The display name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>大頭貼位址。The avatar address.</summary>
    [JsonPropertyName("picture")]
    public string? Picture { get; init; }

    /// <summary>電子郵件。The email address.</summary>
    [JsonPropertyName("email")]
    public string? Email { get; init; }

    /// <summary>
    /// 轉成公開型別。
    /// Converts to the public type.
    /// </summary>
    /// <returns>公開型別的內容。The contents as the public type.</returns>
    internal LineIdTokenPayload ToPayload() => new()
    {
        Issuer = Issuer ?? string.Empty,
        Subject = Subject ?? string.Empty,
        Audience = Audience ?? string.Empty,
        ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(ExpiresAt),
        IssuedAt = DateTimeOffset.FromUnixTimeSeconds(IssuedAt),
        Nonce = Nonce,
        Amr = Amr ?? [],
        Name = Name,
        Picture = Picture,
        Email = Email,
    };
}
