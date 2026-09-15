namespace Ozakboy.Line.AspNetCore;

/// <summary>
/// LINE 專屬的宣告型別。
/// The LINE-specific claim types.
/// </summary>
/// <remarks>
/// 標準的部分沿用 <see cref="System.Security.Claims.ClaimTypes"/>:使用者識別碼是
/// <see cref="System.Security.Claims.ClaimTypes.NameIdentifier"/>,顯示名稱是
/// <see cref="System.Security.Claims.ClaimTypes.Name"/>,電子郵件是
/// <see cref="System.Security.Claims.ClaimTypes.Email"/>。這裡列的是標準宣告沒有對應項目的那些。
/// The standard ones stay on <see cref="System.Security.Claims.ClaimTypes"/>: the user identifier is
/// <see cref="System.Security.Claims.ClaimTypes.NameIdentifier"/>, the display name is
/// <see cref="System.Security.Claims.ClaimTypes.Name"/>, and the email is
/// <see cref="System.Security.Claims.ClaimTypes.Email"/>. Listed here are the ones with no standard counterpart.
/// </remarks>
public static class LineClaimTypes
{
    /// <summary>
    /// 大頭貼位址。
    /// The avatar address.
    /// </summary>
    public const string Picture = "urn:line:picture";

    /// <summary>
    /// 個人狀態訊息。
    /// The status message.
    /// </summary>
    public const string StatusMessage = "urn:line:status_message";

    /// <summary>
    /// 是否已加官方帳號好友,值為 <c>"true"</c> 或 <c>"false"</c>。
    /// Whether the user has added the official account, as <c>"true"</c> or <c>"false"</c>.
    /// </summary>
    /// <remarks>
    /// 查不到好友狀態時<b>不會有這個宣告</b>(而不是給一個 <c>"false"</c>)。「沒查到」與「沒加好友」
    /// 是兩件事,用同一個值表達會讓引導加好友的畫面對已經是好友的人一直跳出來。
    /// When the friendship status cannot be read, this claim is <b>absent</b> rather than set to
    /// <c>"false"</c>. "Could not tell" and "not a friend" are different facts, and collapsing them into one
    /// value makes the add-friend prompt keep appearing for people who are already friends.
    /// </remarks>
    public const string IsFriend = "urn:line:is_friend";

    /// <summary>
    /// LINE 使用者識別碼。
    /// The LINE user identifier.
    /// </summary>
    /// <remarks>
    /// 與 <see cref="System.Security.Claims.ClaimTypes.NameIdentifier"/> 同值。兩個都給,是因為
    /// NameIdentifier 在同時支援多種登入方式的網站上會被其他方案覆蓋或解讀成別的意思,
    /// 而這一個永遠明確地是「LINE 的 userId」。
    /// The same value as <see cref="System.Security.Claims.ClaimTypes.NameIdentifier"/>. Both are issued because
    /// on a site with several sign-in methods NameIdentifier gets overwritten or read as something else, whereas
    /// this one is unambiguously LINE's userId.
    /// </remarks>
    public const string UserId = "urn:line:user_id";
}
