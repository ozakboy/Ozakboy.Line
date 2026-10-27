using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// 分批發送時,從呼叫端的重試鍵推導每一批各自的重試鍵。
/// Derives each batch's own retry key from the caller's retry key during a bulk send.
/// </summary>
/// <remarks>
/// <para>
/// LINE 以重試鍵去重:同一個鍵只送達一次。分批後若每批共用同一個鍵,第二批會被當成第一批的重複而被吞掉;
/// 若每批各產生隨機鍵,重跑整個呼叫時就沒有任何東西能去重。因此各批的鍵必須「彼此不同、又可重現」。
/// LINE deduplicates on the retry key: one key delivers once. If every batch shared the caller's key, the second
/// batch would be swallowed as a duplicate of the first; if every batch drew a random key, re-running the whole
/// call would have nothing to deduplicate on. Each batch's key therefore has to be different from the others
/// yet reproducible.
/// </para>
/// <para>
/// 推導方式:<c>SHA-256(金鑰的 16 位元組(大端序) ‖ 批次索引(4 位元組大端序))</c> 取前 16 位元組,
/// 再設成 UUID 版本 5 / RFC 4122 變體的位元。同一個鍵與索引永遠得到同一個結果,與機器、平台無關。
/// The derivation is <c>SHA-256(the key's 16 bytes, big-endian ‖ the batch index as 4 big-endian bytes)</c>, the
/// first 16 bytes of which get the UUID version 5 / RFC 4122 variant bits set. The same key and index always give
/// the same result, regardless of machine or platform.
/// </para>
/// </remarks>
public static class LineBulkSendRetryKey
{
    /// <summary>
    /// 推導第 <paramref name="batchIndex"/> 批的重試鍵。
    /// Derives the retry key of batch <paramref name="batchIndex"/>.
    /// </summary>
    /// <param name="retryKey">呼叫端給的重試鍵。The caller's retry key.</param>
    /// <param name="batchIndex">批次索引(從 0 起算)。The zero-based batch index.</param>
    /// <returns>該批專用、可重現的重試鍵。The reproducible retry key for that batch.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="batchIndex"/> 為負數時擲出。
    /// Thrown when <paramref name="batchIndex"/> is negative.
    /// </exception>
    public static Guid Derive(Guid retryKey, int batchIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(batchIndex);

        Span<byte> input = stackalloc byte[20];
        retryKey.TryWriteBytes(input[..16], bigEndian: true, out _);
        BinaryPrimitives.WriteInt32BigEndian(input[16..], batchIndex);

        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input, hash);

        Span<byte> bytes = hash[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes, bigEndian: true);
    }
}
