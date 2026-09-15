using System.Text.Json.Serialization;

namespace Ozakboy.Line.Login;

/// <summary>
/// 驗證 access token 的結果。
/// The result of verifying an access token.
/// </summary>
public sealed class LineAccessTokenInfo
{
    /// <summary>
    /// 這個權杖擁有的權限範圍,以空白分隔。
    /// The scopes this token holds, separated by spaces.
    /// </summary>
    [JsonPropertyName("scope")]
    public string Scope { get; init; } = string.Empty;

    /// <summary>
    /// 核發這個權杖的 channel id。
    /// The channel id that issued this token.
    /// </summary>
    /// <remarks>
    /// 驗證權杖時<b>必須</b>比對這個值等於自己的 channel id。LINE 的驗證端點只回答「這個權杖有效」,
    /// 不回答「這個權杖是發給你的」—— 別人 channel 的有效權杖在這裡也會過。
    /// Verification <b>must</b> compare this against one's own channel id. LINE's verify endpoint answers only
    /// that the token is valid, not that it was issued to you: a valid token from someone else's channel passes
    /// here too.
    /// </remarks>
    [JsonPropertyName("client_id")]
    public string ClientId { get; init; } = string.Empty;

    /// <summary>
    /// 權杖的剩餘有效秒數。
    /// The number of seconds the token remains valid.
    /// </summary>
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }
}
