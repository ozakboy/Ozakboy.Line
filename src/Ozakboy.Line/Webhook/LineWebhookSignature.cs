using System.Security.Cryptography;
using System.Text;

namespace Ozakboy.Line.Webhook;

/// <summary>
/// webhook 請求的簽章計算與驗證。
/// Computes and verifies the signature on a webhook request.
/// </summary>
/// <remarks>
/// <para>
/// LINE 以 channel secret 為金鑰,對<b>原始請求內容</b>算 HMAC-SHA256,base64 之後放進
/// <c>X-Line-Signature</c>。關鍵在「原始」兩個字:必須拿還沒被反序列化、也沒被重新序列化的那一份位元組。
/// 解析成物件再序列化回去的 JSON,欄位順序、空白與跳脫方式都可能改變,算出來的簽章就對不上 ——
/// 而症狀是所有請求一律驗不過,看起來像金鑰設錯。
/// LINE computes HMAC-SHA256 over the <b>raw request body</b>, keyed by the channel secret, and puts its base64
/// into <c>X-Line-Signature</c>. "Raw" is the operative word: it must be the bytes as they arrived, neither
/// deserialised nor re-serialised. JSON that has been parsed and written back out may differ in field order,
/// whitespace, and escaping, so the signature will not match — and the symptom is that every request fails
/// verification, which looks like a misconfigured secret.
/// </para>
/// <para>
/// 驗簽是<b>唯一</b>能確認請求來自 LINE 的方法。webhook 位址是公開的,任何人都可以往上面 POST 一份
/// 「使用者送了『請幫我兌換』」的假事件;沒有驗簽的端點等於對外開放的指令介面。
/// Signature verification is the <b>only</b> thing that establishes a request came from LINE. A webhook address
/// is public, and anyone can POST a fabricated "the user asked to redeem this" event at it; an endpoint that does
/// not verify is a command interface open to the world.
/// </para>
/// </remarks>
public static class LineWebhookSignature
{
    /// <summary>
    /// 計算請求內容的簽章。
    /// Computes the signature of a request body.
    /// </summary>
    /// <param name="channelSecret">頻道密鑰。The channel secret.</param>
    /// <param name="body">原始請求內容。The raw request body.</param>
    /// <returns>base64 編碼的簽章。The signature in base64.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="channelSecret"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="channelSecret"/> is <see langword="null"/> or blank.
    /// </exception>
    public static string Compute(string channelSecret, ReadOnlySpan<byte> body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelSecret);

        Span<byte> digest = stackalloc byte[32];
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(channelSecret), body, digest);
        return Convert.ToBase64String(digest);
    }

    /// <summary>
    /// 驗證請求內容的簽章。
    /// Verifies the signature of a request body.
    /// </summary>
    /// <param name="channelSecret">頻道密鑰。The channel secret.</param>
    /// <param name="body">原始請求內容。The raw request body.</param>
    /// <param name="signature"><c>X-Line-Signature</c> 標頭的值。The value of the <c>X-Line-Signature</c> header.</param>
    /// <returns>
    /// 相符時為 <see langword="true"/>。密鑰為空白、標頭缺漏、或標頭不是合法 base64 時一律為
    /// <see langword="false"/> —— 這些情況都不擲出例外,因為它們都只代表一件事:這個請求不可信。
    /// <see langword="true"/> when they match. A blank secret, a missing header, or a header that is not valid
    /// base64 all yield <see langword="false"/> rather than an exception, because they all mean the same thing:
    /// this request cannot be trusted.
    /// </returns>
    public static bool Verify(string channelSecret, ReadOnlySpan<byte> body, string? signature)
    {
        if (string.IsNullOrWhiteSpace(channelSecret) || string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        Span<byte> provided = stackalloc byte[32];
        if (!Convert.TryFromBase64String(signature, provided, out var written) || written != provided.Length)
        {
            return false;
        }

        Span<byte> expected = stackalloc byte[32];
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(channelSecret), body, expected);

        // 定時比較:比較所花的時間不隨「前幾個位元組對了」而改變,攻擊者因此無法逐位元組地試出正確簽章。
        // A fixed-time comparison: how long it takes does not vary with how many leading bytes matched, so an
        // attacker cannot work the signature out one byte at a time.
        return CryptographicOperations.FixedTimeEquals(expected, provided);
    }

    /// <summary>
    /// 驗證請求內容的簽章(字串多載)。
    /// Verifies the signature of a request body, taking the body as a string.
    /// </summary>
    /// <param name="channelSecret">頻道密鑰。The channel secret.</param>
    /// <param name="body">原始請求內容。The raw request body.</param>
    /// <param name="signature"><c>X-Line-Signature</c> 標頭的值。The value of the <c>X-Line-Signature</c> header.</param>
    /// <returns>相符時為 <see langword="true"/>。<see langword="true"/> when they match.</returns>
    /// <remarks>
    /// 字串會以 UTF-8 編碼回位元組。能拿到原始位元組時請用
    /// <see cref="Verify(string, ReadOnlySpan{byte}, string?)"/> —— 少一次編碼往返,也少一次編碼選錯的機會。
    /// The string is encoded back to bytes as UTF-8. Where the raw bytes are available, prefer
    /// <see cref="Verify(string, ReadOnlySpan{byte}, string?)"/>: one encoding round trip fewer, and one fewer
    /// chance to pick the wrong encoding.
    /// </remarks>
    public static bool Verify(string channelSecret, string body, string? signature) =>
        Verify(channelSecret, Encoding.UTF8.GetBytes(body ?? string.Empty), signature);
}
