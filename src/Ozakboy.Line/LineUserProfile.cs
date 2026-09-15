using System.Text.Json.Serialization;

namespace Ozakboy.Line;

/// <summary>
/// LINE 使用者的個人檔案。
/// A LINE user's profile.
/// </summary>
/// <remarks>
/// 這個型別放在根命名空間,因為 Login 與 Messaging 兩邊都會拿到它:LINE Login 以使用者的 access token
/// 查自己的檔案,Messaging API 以官方帳號的權杖查好友的檔案,兩者回的是同一組欄位。
/// This type lives in the root namespace because both sides return it: LINE Login reads the user's own profile
/// with their access token, while the Messaging API reads a friend's with the official account's token, and both
/// answer with the same fields.
/// </remarks>
public sealed class LineUserProfile
{
    /// <summary>
    /// 使用者在這個 channel 下的識別碼。
    /// The user's identifier within this channel.
    /// </summary>
    /// <remarks>
    /// 這個值<b>綁定 channel</b>:同一個人在 Login channel 與 Messaging channel 拿到的 userId 相同,
    /// 但換一個 provider 底下的 channel 就是另一個值。把它當成跨系統的永久個人識別碼會出事。
    /// This value is <b>scoped to the provider</b>: the same person yields the same userId across a Login channel
    /// and a Messaging channel under one provider, but a different value under another provider. Treating it as a
    /// permanent cross-system identity for a person will not hold.
    /// </remarks>
    [JsonPropertyName("userId")]
    public string UserId { get; init; } = string.Empty;

    /// <summary>
    /// 使用者的顯示名稱。
    /// The user's display name.
    /// </summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 大頭貼位址,使用者沒有設定時為 <see langword="null"/>。
    /// The avatar address, or <see langword="null"/> when the user has not set one.
    /// </summary>
    [JsonPropertyName("pictureUrl")]
    public string? PictureUrl { get; init; }

    /// <summary>
    /// 個人狀態訊息,沒有設定時為 <see langword="null"/>。
    /// The status message, or <see langword="null"/> when the user has not set one.
    /// </summary>
    [JsonPropertyName("statusMessage")]
    public string? StatusMessage { get; init; }

    /// <summary>
    /// 使用者的語言(只有 Messaging API 查好友檔案時才有值)。
    /// The user's language, present only when the Messaging API reads a friend's profile.
    /// </summary>
    [JsonPropertyName("language")]
    public string? Language { get; init; }
}
