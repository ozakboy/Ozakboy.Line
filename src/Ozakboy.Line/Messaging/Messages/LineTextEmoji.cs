namespace Ozakboy.Line.Messaging.Messages;

/// <summary>
/// 文字訊息(<see cref="TextMessage"/>)裡的一個 LINE emoji。
/// One LINE emoji inside a <see cref="TextMessage"/>.
/// </summary>
/// <remarks>
/// 文字裡對應的位置必須放一個 <c>$</c> 當佔位字元,LINE 會在顯示時把它換成 emoji 圖。
/// 位置以 UTF-16 字元(也就是 .NET 的 <see cref="string"/> 索引)計算,所以前面若有代理對
/// (例如一般的 Unicode emoji)要算成兩格。
/// The text must hold a <c>$</c> placeholder at the matching position, which LINE swaps for the emoji image when
/// displaying. Positions count UTF-16 characters — the same as a .NET <see cref="string"/> index — so a
/// surrogate pair before it, as in an ordinary Unicode emoji, takes two places.
/// </remarks>
public sealed class LineTextEmoji
{
    /// <summary>
    /// 建立一個 emoji。
    /// Creates an emoji.
    /// </summary>
    /// <param name="index">在文字中的位置(從 0 起算),該處必須是 <c>$</c>。The position in the text, from 0, where a <c>$</c> must stand.</param>
    /// <param name="productId">emoji 商品識別碼。The emoji product identifier.</param>
    /// <param name="emojiId">emoji 識別碼。The emoji identifier.</param>
    /// <exception cref="ArgumentException">
    /// 任一識別碼為 <see langword="null"/> 或空白時擲出。Thrown when either identifier is <see langword="null"/> or blank.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> 為負數時擲出。Thrown when <paramref name="index"/> is negative.
    /// </exception>
    public LineTextEmoji(int index, string productId, string emojiId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        ArgumentException.ThrowIfNullOrWhiteSpace(emojiId);

        Index = index;
        ProductId = productId;
        EmojiId = emojiId;
    }

    /// <summary>
    /// 在文字中的位置(從 0 起算)。
    /// The position in the text, counted from 0.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// emoji 商品識別碼。
    /// The emoji product identifier.
    /// </summary>
    public string ProductId { get; }

    /// <summary>
    /// emoji 識別碼。
    /// The emoji identifier.
    /// </summary>
    public string EmojiId { get; }
}
