using System.Text.Json;

namespace Ozakboy.Line.Messaging.Messages;

/// <summary>
/// <see cref="TextV2Message"/> 裡一個具名替換項:@ 某人、@ 所有人,或一個 LINE emoji。
/// One named substitution in a <see cref="TextV2Message"/>: a mention of someone, a mention of everyone, or a
/// LINE emoji.
/// </summary>
/// <remarks>
/// 用 <see cref="Mention"/>、<see cref="MentionAll"/>、<see cref="Emoji"/> 建立。文字裡以 <c>{key}</c> 引用,
/// 其中 <c>key</c> 是 <see cref="TextV2Message.Substitution"/> 的鍵。
/// Create one with <see cref="Mention"/>, <see cref="MentionAll"/> or <see cref="Emoji"/>. The text refers to it
/// as <c>{key}</c>, where <c>key</c> is the key in <see cref="TextV2Message.Substitution"/>.
/// </remarks>
public sealed class LineTextSubstitution
{
    /// <summary>
    /// 替換項型別字串:@ 提及。
    /// The type string of a mention substitution.
    /// </summary>
    public const string TypeMention = "mention";

    /// <summary>
    /// 替換項型別字串:LINE emoji。
    /// The type string of an emoji substitution.
    /// </summary>
    public const string TypeEmoji = "emoji";

    /// <summary>
    /// 被提及者型別字串:指定使用者。
    /// The mentionee type string for a specific user.
    /// </summary>
    public const string MentioneeUser = "user";

    /// <summary>
    /// 被提及者型別字串:所有人。
    /// The mentionee type string for everyone.
    /// </summary>
    public const string MentioneeAll = "all";

    /// <summary>
    /// 只允許用靜態方法建立。
    /// Instances come only from the static factories.
    /// </summary>
    private LineTextSubstitution()
    {
    }

    /// <summary>
    /// 替換項型別,<see cref="TypeMention"/> 或 <see cref="TypeEmoji"/>。
    /// The substitution type, <see cref="TypeMention"/> or <see cref="TypeEmoji"/>.
    /// </summary>
    public string Type { get; private init; } = string.Empty;

    /// <summary>
    /// 被提及者型別(<see cref="MentioneeUser"/> 或 <see cref="MentioneeAll"/>);只有 mention 有。
    /// The mentionee type (<see cref="MentioneeUser"/> or <see cref="MentioneeAll"/>); mentions only.
    /// </summary>
    public string? MentioneeType { get; private init; }

    /// <summary>
    /// 被提及者的使用者識別碼;只有提及指定使用者時有。
    /// The mentioned user's identifier; present only when mentioning a specific user.
    /// </summary>
    public string? UserId { get; private init; }

    /// <summary>
    /// emoji 商品識別碼;只有 emoji 有。
    /// The emoji product identifier; emojis only.
    /// </summary>
    public string? ProductId { get; private init; }

    /// <summary>
    /// emoji 識別碼;只有 emoji 有。
    /// The emoji identifier; emojis only.
    /// </summary>
    public string? EmojiId { get; private init; }

    /// <summary>
    /// 提及指定的使用者。
    /// Mentions a specific user.
    /// </summary>
    /// <param name="userId">使用者識別碼。The user identifier.</param>
    /// <returns>替換項。The substitution.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="userId"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="userId"/> is <see langword="null"/> or blank.
    /// </exception>
    /// <remarks>
    /// 只有群組與聊天室裡才有意義,而且被提及者必須是該群組的成員。
    /// It only means anything in a group or room, and the mentionee must be a member of it.
    /// </remarks>
    public static LineTextSubstitution Mention(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return new LineTextSubstitution { Type = TypeMention, MentioneeType = MentioneeUser, UserId = userId };
    }

    /// <summary>
    /// 提及所有人(@All)。
    /// Mentions everyone (@All).
    /// </summary>
    /// <returns>替換項。The substitution.</returns>
    public static LineTextSubstitution MentionAll() =>
        new() { Type = TypeMention, MentioneeType = MentioneeAll };

    /// <summary>
    /// 放入一個 LINE emoji。
    /// Places a LINE emoji.
    /// </summary>
    /// <param name="productId">emoji 商品識別碼。The emoji product identifier.</param>
    /// <param name="emojiId">emoji 識別碼。The emoji identifier.</param>
    /// <returns>替換項。The substitution.</returns>
    /// <exception cref="ArgumentException">
    /// 任一識別碼為 <see langword="null"/> 或空白時擲出。Thrown when either identifier is <see langword="null"/> or blank.
    /// </exception>
    public static LineTextSubstitution Emoji(string productId, string emojiId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        ArgumentException.ThrowIfNullOrWhiteSpace(emojiId);
        return new LineTextSubstitution { Type = TypeEmoji, ProductId = productId, EmojiId = emojiId };
    }

    /// <summary>
    /// 把這個替換項寫成 JSON 物件。
    /// Writes this substitution as a JSON object.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    internal void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", Type);

        if (Type == TypeMention)
        {
            writer.WriteStartObject("mentionee");
            writer.WriteString("type", MentioneeType);
            if (UserId is not null)
            {
                writer.WriteString("userId", UserId);
            }

            writer.WriteEndObject();
        }
        else
        {
            writer.WriteString("productId", ProductId);
            writer.WriteString("emojiId", EmojiId);
        }

        writer.WriteEndObject();
    }
}
