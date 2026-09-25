namespace Ozakboy.Line.Messaging;

/// <summary>
/// 一頁使用者識別碼,以及取下一頁用的游標。
/// One page of user identifiers, plus the cursor for the next page.
/// </summary>
/// <remarks>
/// 好友清單回的是 <c>userIds</c>,群組與聊天室的成員清單回的是 <c>memberIds</c>;兩者的分頁方式相同
/// (<c>next</c> 游標),所以這裡用同一個型別呈現,呼叫端不必因為欄位名不同而寫兩套迴圈。
/// The follower list answers with <c>userIds</c> while the group and room member lists answer with
/// <c>memberIds</c>; both page the same way, with a <c>next</c> cursor, so one type presents both and a caller
/// needs one loop rather than two.
/// </remarks>
public sealed class LineUserIdsPage
{
    /// <summary>
    /// 這一頁的使用者識別碼。
    /// The user identifiers on this page.
    /// </summary>
    public IReadOnlyList<string> UserIds { get; init; } = [];

    /// <summary>
    /// 取下一頁時要傳的 <c>start</c> 值;沒有下一頁時為 <see langword="null"/>。
    /// The <c>start</c> value for the next page, or <see langword="null"/> when this is the last page.
    /// </summary>
    public string? Next { get; init; }
}
