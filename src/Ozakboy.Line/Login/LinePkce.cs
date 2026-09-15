using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Ozakboy.Line.Login;

/// <summary>
/// PKCE(RFC 7636)的 code verifier 與 code challenge 產生器。
/// Produces the PKCE code verifier and code challenge of RFC 7636.
/// </summary>
/// <remarks>
/// PKCE 擋的是「授權碼被攔截後被別人拿去換權杖」:challenge 先隨授權請求送出,verifier 留在自己手上,
/// 換權杖時才出示。攔到授權碼的人沒有 verifier,換不到東西。LINE Login 支援 S256,本套件只做 S256 ——
/// <c>plain</c> 方法等於沒有保護,不提供。
/// PKCE defends against an intercepted authorization code being redeemed by someone else: the challenge travels
/// with the authorization request while the verifier stays behind, and is presented only at the token exchange.
/// Whoever intercepts the code has no verifier and gets nothing. LINE Login supports S256, and this package
/// implements only S256 — the <c>plain</c> method offers no protection and is not offered.
/// </remarks>
public static class LinePkce
{
    /// <summary>
    /// code verifier 允許的最短長度(RFC 7636 規定)。
    /// The shortest code verifier RFC 7636 allows.
    /// </summary>
    public const int MinimumVerifierLength = 43;

    /// <summary>
    /// code verifier 允許的最長長度(RFC 7636 規定)。
    /// The longest code verifier RFC 7636 allows.
    /// </summary>
    public const int MaximumVerifierLength = 128;

    /// <summary>
    /// RFC 7636 允許的 unreserved 字元集。
    /// The unreserved character set RFC 7636 allows.
    /// </summary>
    private const string UnreservedCharacters =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";

    /// <summary>
    /// 產生一個隨機的 code verifier。
    /// Creates a random code verifier.
    /// </summary>
    /// <param name="length">
    /// 長度,必須介於 <see cref="MinimumVerifierLength"/> 與 <see cref="MaximumVerifierLength"/> 之間,預設 64。
    /// The length, between <see cref="MinimumVerifierLength"/> and <see cref="MaximumVerifierLength"/>; 64 by
    /// default.
    /// </param>
    /// <returns>由 unreserved 字元組成的 verifier。A verifier made of unreserved characters.</returns>
    /// <remarks>
    /// 亂數來自 <see cref="RandomNumberGenerator"/> 而非 <see cref="Random"/>:verifier 是安全機制的一部分,
    /// 可預測的 verifier 讓 PKCE 完全失效,而 <see cref="Random"/> 的序列是可預測的。
    /// The randomness comes from <see cref="RandomNumberGenerator"/> rather than <see cref="Random"/>: the
    /// verifier is part of a security mechanism, a predictable one defeats PKCE entirely, and
    /// <see cref="Random"/>'s sequence is predictable.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// 長度超出允許範圍時擲出。Thrown when the length is outside the allowed range.
    /// </exception>
    public static string CreateCodeVerifier(int length = 64)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(length, MinimumVerifierLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, MaximumVerifierLength);

        return RandomNumberGenerator.GetString(UnreservedCharacters, length);
    }

    /// <summary>
    /// 由 code verifier 算出 S256 的 code challenge。
    /// Derives the S256 code challenge from a code verifier.
    /// </summary>
    /// <param name="verifier">code verifier。The code verifier.</param>
    /// <returns>base64url 編碼、不帶補位字元的 challenge。The challenge in base64url with no padding.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="verifier"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="verifier"/> is <see langword="null"/> or blank.
    /// </exception>
    public static string ComputeCodeChallenge(string verifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(verifier);

        var digest = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64Url.EncodeToString(digest);
    }
}
