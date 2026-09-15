using System.Text.Json.Serialization;

namespace Ozakboy.Line.Login;

/// <summary>
/// 權杖端點的回應。
/// The token endpoint's response.
/// </summary>
public sealed class LineTokenResponse
{
    /// <summary>
    /// 存取權杖,用於呼叫 LINE Login 的各項 API。
    /// The access token used to call LINE Login's APIs.
    /// </summary>
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>
    /// 存取權杖的剩餘有效秒數。
    /// The number of seconds the access token remains valid.
    /// </summary>
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }

    /// <summary>
    /// OpenID Connect 的 id_token;只有在權限範圍包含 <c>openid</c> 時才有值。
    /// The OpenID Connect id_token, present only when the scopes include <c>openid</c>.
    /// </summary>
    [JsonPropertyName("id_token")]
    public string? IdToken { get; init; }

    /// <summary>
    /// 續期用的 refresh token。
    /// The refresh token.
    /// </summary>
    /// <remarks>
    /// LINE 的 refresh token 有它自己的效期(預設 90 天),而且只在使用者仍有授權時有效;
    /// 續期成功時會回一個新的,務必覆蓋舊的保存。
    /// LINE's refresh token has its own lifetime (90 days by default) and only works while the user's
    /// authorization stands. A successful refresh returns a new one, which must replace the stored value.
    /// </remarks>
    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }

    /// <summary>
    /// 實際獲准的權限範圍,以空白分隔。
    /// The scopes actually granted, separated by spaces.
    /// </summary>
    /// <remarks>
    /// 這裡回的是<b>獲准</b>的範圍,不一定等於請求的範圍 —— 例如 <c>email</c> 沒有通過 LINE 審核時,
    /// 請求照送,回來的 scope 就是少了它。要判斷「拿不拿得到 email」請看這個值,不要看自己送出去的。
    /// These are the scopes that were <b>granted</b>, which need not match those requested: when <c>email</c> has
    /// not been approved by LINE the request still goes through and simply comes back without it. Read this value
    /// rather than the request to know whether an email is coming.
    /// </remarks>
    [JsonPropertyName("scope")]
    public string Scope { get; init; } = string.Empty;

    /// <summary>
    /// 權杖類型,LINE 一律回 <c>Bearer</c>。
    /// The token type, always <c>Bearer</c> from LINE.
    /// </summary>
    [JsonPropertyName("token_type")]
    public string TokenType { get; init; } = string.Empty;
}
