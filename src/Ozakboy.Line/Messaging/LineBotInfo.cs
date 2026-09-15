using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// 官方帳號自身的資訊。
/// Information about the official account itself.
/// </summary>
public sealed class LineBotInfo
{
    /// <summary>
    /// 官方帳號的使用者識別碼。
    /// The official account's user identifier.
    /// </summary>
    [JsonPropertyName("userId")]
    public string UserId { get; init; } = string.Empty;

    /// <summary>
    /// 帳號的基本 ID(以 <c>@</c> 開頭的那一個)。
    /// The account's basic id, the one starting with <c>@</c>.
    /// </summary>
    [JsonPropertyName("basicId")]
    public string BasicId { get; init; } = string.Empty;

    /// <summary>
    /// 付費取得的專屬 ID;沒有時為 <see langword="null"/>。
    /// The premium id, when one has been purchased; otherwise <see langword="null"/>.
    /// </summary>
    [JsonPropertyName("premiumId")]
    public string? PremiumId { get; init; }

    /// <summary>
    /// 帳號顯示名稱。
    /// The account's display name.
    /// </summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 帳號大頭貼位址;未設定時為 <see langword="null"/>。
    /// The account's avatar address, or <see langword="null"/> when none is set.
    /// </summary>
    [JsonPropertyName("pictureUrl")]
    public string? PictureUrl { get; init; }

    /// <summary>
    /// 聊天模式:<c>chat</c>(聊天)或 <c>bot</c>(機器人)。
    /// The chat mode: <c>chat</c> or <c>bot</c>.
    /// </summary>
    /// <remarks>
    /// 這個值是<b>設定</b>不是狀態。設成 <c>chat</c> 時,webhook 仍然會收到事件,但回覆的主導權在人工客服那邊;
    /// 「程式沒回話」的第一個檢查點通常就是這裡,而不是程式。
    /// This is a <b>setting</b> rather than a state. In <c>chat</c> mode the webhook still receives events, but
    /// replies are the human operator's to make; it is usually the first thing to check when "the bot is not
    /// answering", before the code.
    /// </remarks>
    [JsonPropertyName("chatMode")]
    public string ChatMode { get; init; } = string.Empty;

    /// <summary>
    /// 已讀標記模式:<c>auto</c> 或 <c>manual</c>。
    /// The read-receipt mode: <c>auto</c> or <c>manual</c>.
    /// </summary>
    [JsonPropertyName("markAsReadMode")]
    public string MarkAsReadMode { get; init; } = string.Empty;
}
