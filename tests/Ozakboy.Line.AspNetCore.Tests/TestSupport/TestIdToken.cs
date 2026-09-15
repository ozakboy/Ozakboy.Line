using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Ozakboy.Line.AspNetCore.Tests.TestSupport;

/// <summary>
/// 在測試裡自己簽一個 id_token。
/// Signs an id_token inside the test.
/// </summary>
/// <remarks>
/// 一律使用佔位符憑證。測試不連 LINE,也不使用任何真實的 channel id 或 channel secret。
/// Placeholder credentials throughout: the tests never call LINE and never use a real channel id or secret.
/// </remarks>
internal static class TestIdToken
{
    /// <summary>
    /// 測試用的 channel id。
    /// The channel id used in tests.
    /// </summary>
    internal const string ChannelId = "test-channel-id";

    /// <summary>
    /// 測試用的 channel secret。
    /// The channel secret used in tests.
    /// </summary>
    internal const string ChannelSecret = "test-channel-secret";

    /// <summary>
    /// 簽一個 HS256 的 id_token。
    /// Signs an HS256 id_token.
    /// </summary>
    /// <param name="channelSecret">簽章金鑰。The signing key.</param>
    /// <param name="issuer">發行者。The issuer.</param>
    /// <param name="audience">對象。The audience.</param>
    /// <param name="expiresAt">到期時間;<see langword="null"/> 表示一小時後。The expiry; an hour from now when <see langword="null"/>.</param>
    /// <param name="nonce">一次性隨機值。The one-time value.</param>
    /// <param name="email">電子郵件。The email address.</param>
    /// <param name="algorithm">標頭裡宣告的演算法。The algorithm declared in the header.</param>
    /// <returns>簽好的 id_token。The signed id_token.</returns>
    internal static string Create(
        string? channelSecret = null,
        string issuer = LineEndpoints.IdTokenIssuer,
        string? audience = null,
        DateTimeOffset? expiresAt = null,
        string? nonce = null,
        string? email = null,
        string algorithm = "HS256")
    {
        var secret = channelSecret ?? ChannelSecret;
        var expiry = (expiresAt ?? DateTimeOffset.UtcNow.AddHours(1)).ToUnixTimeSeconds();
        var issuedAt = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds();

        var header = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(
            $"{{\"alg\":\"{algorithm}\",\"typ\":\"JWT\"}}"));

        var claims = new StringBuilder();
        claims.Append(CultureInfo.InvariantCulture, $"{{\"iss\":\"{issuer}\"");
        claims.Append(CultureInfo.InvariantCulture, $",\"sub\":\"U1234567890abcdef\"");
        claims.Append(CultureInfo.InvariantCulture, $",\"aud\":\"{audience ?? ChannelId}\"");
        claims.Append(CultureInfo.InvariantCulture, $",\"exp\":{expiry}");
        claims.Append(CultureInfo.InvariantCulture, $",\"iat\":{issuedAt}");
        claims.Append(CultureInfo.InvariantCulture, $",\"amr\":[\"pwd\"]");
        claims.Append(CultureInfo.InvariantCulture, $",\"name\":\"Test User\"");

        if (nonce is not null)
        {
            claims.Append(CultureInfo.InvariantCulture, $",\"nonce\":\"{nonce}\"");
        }

        if (email is not null)
        {
            claims.Append(CultureInfo.InvariantCulture, $",\"email\":\"{email}\"");
        }

        claims.Append('}');

        var payload = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(claims.ToString()));
        var signingInput = Encoding.ASCII.GetBytes($"{header}.{payload}");
        var signature = Base64Url.EncodeToString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), signingInput));

        return $"{header}.{payload}.{signature}";
    }

    /// <summary>
    /// 把一個合法的 id_token 的簽章改掉。
    /// Corrupts the signature of an otherwise valid id_token.
    /// </summary>
    /// <param name="idToken">原本的 id_token。The original id_token.</param>
    /// <returns>簽章被改過的 id_token。The id_token with a corrupted signature.</returns>
    internal static string WithBrokenSignature(string idToken)
    {
        var parts = idToken.Split('.');
        var signature = Base64Url.DecodeFromChars(parts[2]);
        signature[0] ^= 0xFF;
        return $"{parts[0]}.{parts[1]}.{Base64Url.EncodeToString(signature)}";
    }
}
