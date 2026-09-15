using System.Text.Json.Serialization;

namespace Ozakboy.Line.Login;

/// <summary>
/// OpenID Connect userinfo 端點的回應。
/// The OpenID Connect userinfo endpoint's response.
/// </summary>
/// <remarks>
/// 這裡的 <see cref="Subject"/> 與 <see cref="LineUserProfile.UserId"/> 是同一個值,兩個端點只是欄位名不同;
/// 需要顯示名稱與狀態訊息時用 profile 端點,只要識別碼時這個端點比較省。
/// The <see cref="Subject"/> here is the same value as <see cref="LineUserProfile.UserId"/>; only the field names
/// differ between the two endpoints. Use the profile endpoint when the display name and status message are
/// wanted, and this one when only the identifier is.
/// </remarks>
public sealed class LineUserInfo
{
    /// <summary>
    /// 使用者識別碼,等同於 <see cref="LineUserProfile.UserId"/>。
    /// The user identifier, the same value as <see cref="LineUserProfile.UserId"/>.
    /// </summary>
    [JsonPropertyName("sub")]
    public string Subject { get; init; } = string.Empty;

    /// <summary>
    /// 顯示名稱,權限範圍不含 <c>profile</c> 時為 <see langword="null"/>。
    /// The display name, or <see langword="null"/> when the scopes do not include <c>profile</c>.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// 大頭貼位址,權限範圍不含 <c>profile</c> 或使用者未設定時為 <see langword="null"/>。
    /// The avatar address, or <see langword="null"/> without the <c>profile</c> scope or when the user has not set
    /// one.
    /// </summary>
    [JsonPropertyName("picture")]
    public string? Picture { get; init; }
}
