using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ozakboy.Core.Abstractions;

namespace Ozakboy.Line.Login;

/// <summary>
/// 在本地驗證 LINE 的 id_token。
/// Verifies a LINE id_token locally.
/// </summary>
/// <remarks>
/// <para>
/// LINE 以 HS256 簽 id_token,金鑰就是 channel secret —— 驗證方與簽章方握有同一個祕密,
/// 因此不需要下載公鑰、不需要 JWKS 快取,也就<b>不需要任何 JWT 函式庫</b>:
/// BCL 的 <see cref="HMACSHA256"/> 與 <see cref="Base64Url"/> 已經足夠。
/// LINE signs id_tokens with HS256 keyed by the channel secret, so the verifier and the signer hold the same
/// secret: no public key to fetch, no JWKS cache, and therefore <b>no JWT library at all</b> — the BCL's
/// <see cref="HMACSHA256"/> and <see cref="Base64Url"/> cover it.
/// </para>
/// <para>
/// 本地驗證檢查五件事:簽章、發行者、對象、到期、以及(有指定時)nonce。任何一項不過就是失敗,
/// 而且失敗代碼各自不同 —— 「驗不過」這三個字對除錯毫無幫助,是簽章不符還是時鐘差太多,處理方式完全不一樣。
/// Local validation checks five things: the signature, the issuer, the audience, the expiry, and the nonce when
/// one is expected. Any one of them failing is a failure, each with its own code — "did not verify" tells a
/// debugger nothing, and a signature mismatch and a clock too far out call for entirely different responses.
/// </para>
/// </remarks>
public static class LineIdTokenValidator
{
    /// <summary>
    /// 預設允許的時鐘誤差。
    /// The clock skew allowed by default.
    /// </summary>
    public static TimeSpan DefaultClockSkew { get; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 驗證 id_token 並取出內容。
    /// Verifies an id_token and returns its contents.
    /// </summary>
    /// <param name="idToken">要驗證的 id_token。The id_token to verify.</param>
    /// <param name="channelId">自己的 Login channel id,必須等於 token 的 <c>aud</c>。This Login channel's id, which must equal the token's <c>aud</c>.</param>
    /// <param name="channelSecret">Login channel secret,也就是簽章金鑰。The Login channel secret, which is the signing key.</param>
    /// <param name="expectedNonce">
    /// 授權時送出的 nonce;為 <see langword="null"/> 時不檢查。
    /// The nonce sent at authorization, or <see langword="null"/> to skip the check.
    /// </param>
    /// <param name="timeProvider">
    /// 時間來源,為 <see langword="null"/> 時使用 <see cref="TimeProvider.System"/>。
    /// The time source; <see cref="TimeProvider.System"/> when <see langword="null"/>.
    /// </param>
    /// <param name="clockSkew">
    /// 允許的時鐘誤差,為 <see langword="null"/> 時使用 <see cref="DefaultClockSkew"/>。
    /// The clock skew allowed; <see cref="DefaultClockSkew"/> when <see langword="null"/>.
    /// </param>
    /// <returns>
    /// 驗證通過時為內容;否則為 <c>line.id_token.*</c> 的失敗。
    /// The contents when it verifies, otherwise a <c>line.id_token.*</c> failure.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="idToken"/>、<paramref name="channelId"/> 或 <paramref name="channelSecret"/> 為
    /// <see langword="null"/> 或空白時擲出 —— 這是呼叫端的程式缺陷,不是預期之內的失敗。
    /// Thrown when <paramref name="idToken"/>, <paramref name="channelId"/>, or
    /// <paramref name="channelSecret"/> is <see langword="null"/> or blank, which is a defect in the caller
    /// rather than an expected failure.
    /// </exception>
    public static Result<LineIdTokenPayload> Validate(
        string idToken,
        string channelId,
        string channelSecret,
        string? expectedNonce = null,
        TimeProvider? timeProvider = null,
        TimeSpan? clockSkew = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(channelSecret);

        var parts = idToken.Split('.');
        if (parts.Length != 3)
        {
            return InvalidFormat("id_token 不是三段式的 JWT。The id_token is not a three-part JWT.");
        }

        if (!TryDecode(parts[0], out var headerBytes)
            || !TryDecode(parts[1], out var payloadBytes)
            || !TryDecode(parts[2], out var signature))
        {
            return InvalidFormat("id_token 的某一段不是合法的 base64url。A part of the id_token is not valid base64url.");
        }

        var algorithm = ReadAlgorithm(headerBytes);
        if (algorithm is null)
        {
            return InvalidFormat("id_token 的標頭無法解析。The id_token header could not be read.");
        }

        if (!string.Equals(algorithm, "HS256", StringComparison.Ordinal))
        {
            return Error.Validation(
                LineErrorCodes.IdTokenUnsupportedAlgorithm,
                $"本地驗證只支援 HS256,這個 id_token 是 {algorithm};請改用 VerifyIdTokenAsync 交給 LINE 遠端驗證。Local validation supports HS256 only and this id_token uses {algorithm}; use VerifyIdTokenAsync to have LINE verify it instead.");
        }

        // 簽章比對用定時比較:一般的位元組比較會在第一個不同的位元組就返回,
        // 執行時間因此洩漏「猜對了前幾個位元組」,足以讓攻擊者一個位元組一個位元組地湊出正確簽章。
        // The signature is compared in fixed time: an ordinary byte comparison returns at the first difference,
        // so its duration leaks how many leading bytes were guessed correctly — enough to assemble a valid
        // signature one byte at a time.
        var signingInput = Encoding.ASCII.GetBytes(string.Concat(parts[0], ".", parts[1]));
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(channelSecret), signingInput);
        if (!CryptographicOperations.FixedTimeEquals(expected, signature))
        {
            return Error.Validation(
                LineErrorCodes.IdTokenInvalidSignature,
                "id_token 的簽章與 channel secret 算出來的不符。The id_token's signature does not match the one computed from the channel secret.");
        }

        LineIdTokenClaims? claims;
        try
        {
            claims = JsonSerializer.Deserialize<LineIdTokenClaims>(payloadBytes, LineJson.Options);
        }
        catch (JsonException)
        {
            return InvalidFormat("id_token 的內容不是合法的 JSON。The id_token payload is not valid JSON.");
        }

        if (claims is null)
        {
            return InvalidFormat("id_token 的內容為空。The id_token payload was empty.");
        }

        if (!string.Equals(claims.Issuer, LineEndpoints.IdTokenIssuer, StringComparison.Ordinal))
        {
            return Error.Validation(
                LineErrorCodes.IdTokenInvalidIssuer,
                $"id_token 的發行者應為 {LineEndpoints.IdTokenIssuer}。The id_token issuer should be {LineEndpoints.IdTokenIssuer}.");
        }

        if (!string.Equals(claims.Audience, channelId, StringComparison.Ordinal))
        {
            // 訊息不回述兩邊的值:aud 是自己的 channel id,寫進日誌沒有必要。
            // The message echoes neither value: the audience is one's own channel id and has no business in a log.
            return Error.Validation(
                LineErrorCodes.IdTokenInvalidAudience,
                "id_token 不是發給這個 channel id 的。The id_token was not issued for this channel id.");
        }

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(claims.ExpiresAt);
        if (expiresAt + (clockSkew ?? DefaultClockSkew) < now)
        {
            return Error.Validation(
                LineErrorCodes.IdTokenExpired,
                string.Create(CultureInfo.InvariantCulture, $"id_token 已於 {expiresAt:O} 過期。The id_token expired at {expiresAt:O}."));
        }

        if (expectedNonce is not null && !string.Equals(claims.Nonce, expectedNonce, StringComparison.Ordinal))
        {
            return Error.Validation(
                LineErrorCodes.IdTokenNonceMismatch,
                "id_token 的 nonce 與這次登入送出的不符。The id_token's nonce does not match the one sent with this sign-in.");
        }

        return Result.Success(claims.ToPayload());
    }

    /// <summary>
    /// 產生格式錯誤的失敗。
    /// Builds an invalid-format failure.
    /// </summary>
    /// <param name="message">說明訊息。The message.</param>
    /// <returns>失敗。The failure.</returns>
    private static Error InvalidFormat(string message) => Error.Validation(LineErrorCodes.IdTokenInvalidFormat, message);

    /// <summary>
    /// 解開一段 base64url。
    /// Decodes one base64url segment.
    /// </summary>
    /// <param name="segment">要解的段落。The segment to decode.</param>
    /// <param name="bytes">解出來的位元組。The decoded bytes.</param>
    /// <returns>可以解開時為 <see langword="true"/>。<see langword="true"/> when it decodes.</returns>
    private static bool TryDecode(string segment, out byte[] bytes)
    {
        try
        {
            bytes = Base64Url.DecodeFromChars(segment);
            return true;
        }
        catch (FormatException)
        {
            bytes = [];
            return false;
        }
    }

    /// <summary>
    /// 從 JWT 標頭讀出簽章演算法。
    /// Reads the signing algorithm from the JWT header.
    /// </summary>
    /// <param name="headerBytes">標頭的位元組。The header bytes.</param>
    /// <returns>讀不到時為 <see langword="null"/>。<see langword="null"/> when it cannot be read.</returns>
    private static string? ReadAlgorithm(byte[] headerBytes)
    {
        try
        {
            using var document = JsonDocument.Parse(headerBytes);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("alg", out var algorithm)
                && algorithm.ValueKind == JsonValueKind.String
                    ? algorithm.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
