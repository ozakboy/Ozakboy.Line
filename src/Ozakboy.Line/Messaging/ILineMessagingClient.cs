using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Messaging.Messages;
using Ozakboy.Line.Messaging.RichMenu;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// LINE Messaging API 的用戶端。
/// The LINE Messaging API client.
/// </summary>
/// <remarks>
/// 所有預期之內的失敗都以 <see cref="Result"/> 回傳而非擲出例外。額度用完、對方封鎖了官方帳號、
/// reply token 過期,這些是推播系統的日常,不是程式缺陷。
/// Every expected failure comes back as a <see cref="Result"/> rather than an exception. A spent quota, a user
/// who blocked the account, an expired reply token: these are a messaging system's normal days, not defects.
/// </remarks>
public interface ILineMessagingClient
{
    /// <summary>
    /// 推播訊息給單一對象。
    /// Pushes messages to a single recipient.
    /// </summary>
    /// <param name="to">使用者、群組或聊天室識別碼。A user, group, or room identifier.</param>
    /// <param name="messages">訊息,1 到 5 則。The messages, between one and five.</param>
    /// <param name="options">可選參數。The optional parameters.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>已送出的訊息。The messages that were sent.</returns>
    Task<Result<IReadOnlyList<LineSentMessage>>> PushAsync(
        string to,
        IReadOnlyList<LineMessage> messages,
        LinePushOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 推播一則文字訊息給單一對象。
    /// Pushes one text message to a single recipient.
    /// </summary>
    /// <param name="to">使用者、群組或聊天室識別碼。A user, group, or room identifier.</param>
    /// <param name="text">訊息內容。The message text.</param>
    /// <param name="options">可選參數。The optional parameters.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>已送出的訊息。The messages that were sent.</returns>
    Task<Result<IReadOnlyList<LineSentMessage>>> PushTextAsync(
        string to,
        string text,
        LinePushOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 以原始 JSON 推播給單一對象。
    /// Pushes raw JSON to a single recipient.
    /// </summary>
    /// <param name="to">使用者、群組或聊天室識別碼。A user, group, or room identifier.</param>
    /// <param name="messagesJson">
    /// 單一訊息物件或訊息陣列的 JSON。兩種都接受,最後都會被包成 <c>{"messages": [...]}</c>。
    /// A single message object or an array of them, as JSON. Either is accepted and both end up wrapped as
    /// <c>{"messages": [...]}</c>.
    /// </param>
    /// <param name="options">可選參數。The optional parameters.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>已送出的訊息。The messages that were sent.</returns>
    Task<Result<IReadOnlyList<LineSentMessage>>> PushRawJsonAsync(
        string to,
        string messagesJson,
        LinePushOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 推播訊息給多個使用者。
    /// Pushes messages to several users.
    /// </summary>
    /// <param name="to">
    /// 使用者識別碼,1 到 <see cref="LineMessagingLimits.MulticastRecipients"/> 個。群組與聊天室不能用這個端點。
    /// User identifiers, between one and <see cref="LineMessagingLimits.MulticastRecipients"/>. Groups and rooms
    /// cannot be addressed here.
    /// </param>
    /// <param name="messages">訊息,1 到 5 則。The messages, between one and five.</param>
    /// <param name="options">可選參數。The optional parameters.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>成功或失敗。Success or failure.</returns>
    Task<Result> MulticastAsync(
        IReadOnlyList<string> to,
        IReadOnlyList<LineMessage> messages,
        LinePushOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 廣播訊息給所有好友。
    /// Broadcasts messages to every friend.
    /// </summary>
    /// <param name="messages">訊息,1 到 5 則。The messages, between one and five.</param>
    /// <param name="options">可選參數。The optional parameters.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>成功或失敗。Success or failure.</returns>
    Task<Result> BroadcastAsync(
        IReadOnlyList<LineMessage> messages,
        LinePushOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 廣播一則文字訊息給所有好友。
    /// Broadcasts one text message to every friend.
    /// </summary>
    /// <param name="text">訊息內容。The message text.</param>
    /// <param name="options">可選參數。The optional parameters.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>成功或失敗。Success or failure.</returns>
    Task<Result> BroadcastTextAsync(string text, LinePushOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 以原始 JSON 廣播給所有好友。
    /// Broadcasts raw JSON to every friend.
    /// </summary>
    /// <param name="messagesJson">
    /// 單一訊息物件或訊息陣列的 JSON。A single message object or an array of them, as JSON.
    /// </param>
    /// <param name="options">可選參數。The optional parameters.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>成功或失敗。Success or failure.</returns>
    Task<Result> BroadcastRawJsonAsync(string messagesJson, LinePushOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 以 reply token 回覆訊息。
    /// Replies with a reply token.
    /// </summary>
    /// <param name="replyToken">webhook 事件帶來的回覆權杖。The reply token from a webhook event.</param>
    /// <param name="messages">訊息,1 到 5 則。The messages, between one and five.</param>
    /// <param name="notificationDisabled">是否不發出通知。Whether to suppress the notification.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>已送出的訊息。The messages that were sent.</returns>
    /// <remarks>
    /// 回覆<b>不計推播額度</b>,而且一個 reply token 只能用一次、效期很短。能回覆就不要推播 ——
    /// 這是額度用不用得完的關鍵差別。
    /// A reply <b>does not count against the push quota</b>, and a reply token is single-use and short-lived.
    /// Replying instead of pushing wherever possible is what decides whether the quota lasts.
    /// </remarks>
    Task<Result<IReadOnlyList<LineSentMessage>>> ReplyAsync(
        string replyToken,
        IReadOnlyList<LineMessage> messages,
        bool notificationDisabled = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 以 reply token 回覆一則文字訊息。
    /// Replies with one text message.
    /// </summary>
    /// <param name="replyToken">回覆權杖。The reply token.</param>
    /// <param name="text">訊息內容。The message text.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>已送出的訊息。The messages that were sent.</returns>
    Task<Result<IReadOnlyList<LineSentMessage>>> ReplyTextAsync(string replyToken, string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// 以 reply token 回覆原始 JSON。
    /// Replies with raw JSON.
    /// </summary>
    /// <param name="replyToken">回覆權杖。The reply token.</param>
    /// <param name="messagesJson">單一訊息物件或訊息陣列的 JSON。A single message object or an array of them, as JSON.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>已送出的訊息。The messages that were sent.</returns>
    Task<Result<IReadOnlyList<LineSentMessage>>> ReplyRawJsonAsync(string replyToken, string messagesJson, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢本月推播額度。
    /// Reads this month's push quota.
    /// </summary>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>額度。The quota.</returns>
    Task<Result<LineMessageQuota>> GetQuotaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢本月已使用的推播則數。
    /// Reads how many messages have been sent this month.
    /// </summary>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>已使用的則數。The number already sent.</returns>
    Task<Result<long>> GetQuotaConsumptionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢官方帳號自身資訊。
    /// Reads information about the official account itself.
    /// </summary>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>帳號資訊。The account information.</returns>
    Task<Result<LineBotInfo>> GetBotInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢好友的個人檔案。
    /// Reads a friend's profile.
    /// </summary>
    /// <param name="userId">使用者識別碼。The user identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>
    /// 個人檔案。對方不是好友(或已封鎖)時,LINE 回 404,這裡是
    /// <see cref="ErrorCategory.NotFound"/> 的失敗。
    /// The profile. When the user is not a friend, or has blocked the account, LINE answers 404 and this is a
    /// <see cref="ErrorCategory.NotFound"/> failure.
    /// </returns>
    Task<Result<LineUserProfile>> GetProfileAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 下載訊息內容(圖片、影片、語音、檔案)。
    /// Downloads a message's content: an image, video, audio clip, or file.
    /// </summary>
    /// <param name="messageId">訊息識別碼。The message identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>內容與其型別。The content and its type.</returns>
    Task<Result<LineContent>> GetMessageContentAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 建立圖文選單。
    /// Creates a rich menu.
    /// </summary>
    /// <param name="richMenu">選單定義。The menu definition.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>選單識別碼。The menu identifier.</returns>
    Task<Result<string>> CreateRichMenuAsync(LineRichMenu richMenu, CancellationToken cancellationToken = default);

    /// <summary>
    /// 上傳圖文選單的圖片。
    /// Uploads a rich menu's image.
    /// </summary>
    /// <param name="richMenuId">選單識別碼。The menu identifier.</param>
    /// <param name="image">圖片位元組。The image bytes.</param>
    /// <param name="contentType">圖片型別,<c>image/jpeg</c> 或 <c>image/png</c>。The image type, <c>image/jpeg</c> or <c>image/png</c>.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>成功或失敗。Success or failure.</returns>
    Task<Result> UploadRichMenuImageAsync(
        string richMenuId,
        byte[] image,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出所有圖文選單。
    /// Lists every rich menu.
    /// </summary>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>選單清單。The menus.</returns>
    Task<Result<IReadOnlyList<LineRichMenuInfo>>> GetRichMenuListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢單一圖文選單。
    /// Reads one rich menu.
    /// </summary>
    /// <param name="richMenuId">選單識別碼。The menu identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>選單內容。The menu.</returns>
    Task<Result<LineRichMenuInfo>> GetRichMenuAsync(string richMenuId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 刪除圖文選單。
    /// Deletes a rich menu.
    /// </summary>
    /// <param name="richMenuId">選單識別碼。The menu identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>成功或失敗。Success or failure.</returns>
    Task<Result> DeleteRichMenuAsync(string richMenuId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 設定所有使用者的預設圖文選單。
    /// Sets the default rich menu for every user.
    /// </summary>
    /// <param name="richMenuId">選單識別碼。The menu identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>成功或失敗。Success or failure.</returns>
    /// <remarks>
    /// 個別使用者的連結(<see cref="LinkRichMenuToUserAsync"/>)優先於預設選單;設了預設卻看到別的,
    /// 通常是那個使用者身上還掛著舊的個別連結。
    /// A per-user link (<see cref="LinkRichMenuToUserAsync"/>) takes precedence over the default. Setting a
    /// default and seeing something else usually means that user still carries an old per-user link.
    /// </remarks>
    Task<Result> SetDefaultRichMenuAsync(string richMenuId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清除預設圖文選單。
    /// Clears the default rich menu.
    /// </summary>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>成功或失敗。Success or failure.</returns>
    Task<Result> ClearDefaultRichMenuAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢目前的預設圖文選單。
    /// Reads the current default rich menu.
    /// </summary>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>
    /// 沒有設定預設選單時為成功且值為 <see langword="null"/>,不是失敗 —— 「沒有預設選單」是一個正常狀態。
    /// Success with a <see langword="null"/> value when no default is set, rather than a failure: having no
    /// default is a perfectly normal state.
    /// </returns>
    Task<Result<string?>> GetDefaultRichMenuIdAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 把圖文選單連結到單一使用者。
    /// Links a rich menu to one user.
    /// </summary>
    /// <param name="userId">使用者識別碼。The user identifier.</param>
    /// <param name="richMenuId">選單識別碼。The menu identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>成功或失敗。Success or failure.</returns>
    Task<Result> LinkRichMenuToUserAsync(string userId, string richMenuId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 解除單一使用者的圖文選單連結。
    /// Unlinks one user's rich menu.
    /// </summary>
    /// <param name="userId">使用者識別碼。The user identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>成功或失敗。Success or failure.</returns>
    Task<Result> UnlinkRichMenuFromUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢單一使用者目前連結的圖文選單。
    /// Reads the rich menu currently linked to one user.
    /// </summary>
    /// <param name="userId">使用者識別碼。The user identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>
    /// 沒有連結時為成功且值為 <see langword="null"/>,不是失敗。
    /// Success with a <see langword="null"/> value when nothing is linked, rather than a failure.
    /// </returns>
    Task<Result<string?>> GetRichMenuIdOfUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 以新選單換掉舊選單:建立、上傳圖片,再視設定設為預設、指向別名、刪除舊選單。
    /// Replaces a rich menu with a new one: create, upload the image, then optionally make it the default, point
    /// an alias at it, and delete the old one.
    /// </summary>
    /// <param name="menu">新選單的定義。The new menu's definition.</param>
    /// <param name="image">選單圖片的位元組。The menu image's bytes.</param>
    /// <param name="contentType"><c>image/jpeg</c> 或 <c>image/png</c>。Either <c>image/jpeg</c> or <c>image/png</c>.</param>
    /// <param name="options">可選步驟。The optional steps.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>新選單的識別碼。The new menu's identifier.</returns>
    /// <remarks>
    /// <para>
    /// LINE 沒有「就地更新選單」這回事:改版就是建一個新的再把舊的換掉,而那是四到五個 API 呼叫。
    /// 拆開自己寫,漏掉其中一步的代價不小 —— 忘了刪舊選單會慢慢吃掉選單額度,
    /// 圖片傳失敗卻不收回新選單則會在帳號上留下一個空白選單。
    /// LINE has no in-place update for a rich menu: a revision means creating a new one and swapping the old one
    /// out, which is four or five API calls. Written out by hand, a missed step costs: forgetting to delete the
    /// old menu eats the account's menu allowance over time, and an upload failure with no rollback leaves a
    /// blank menu on the account.
    /// </para>
    /// <para>
    /// 失敗處理不對稱,而且是刻意的:<b>圖片上傳失敗會刪掉剛建的新選單</b>(沒有圖片的選單比沒有選單更糟),
    /// 但<b>刪舊選單失敗只記錄、整體仍算成功</b>(新選單已經上線,回報失敗只會讓呼叫端重做而多出一個選單)。
    /// The failure handling is asymmetric on purpose: <b>a failed image upload deletes the menu just created</b>,
    /// since a menu without an image is worse than no menu, while <b>a failed deletion of the old menu is only
    /// logged and the call still succeeds</b>, since the new menu is already live and reporting a failure would
    /// have the caller redo it and end up with one menu more.
    /// </para>
    /// </remarks>
    Task<Result<string>> ReplaceRichMenuAsync(
        LineRichMenu menu,
        byte[] image,
        string contentType,
        LineRichMenuReplaceOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 建立圖文選單別名。
    /// Creates a rich menu alias.
    /// </summary>
    /// <param name="aliasId">別名識別碼。The alias identifier.</param>
    /// <param name="richMenuId">要指向的選單識別碼。The menu identifier to point at.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>
    /// 成功或失敗;別名已存在時 LINE 回 400,那時要用 <see cref="UpdateRichMenuAliasAsync"/>。
    /// Success or failure; LINE answers 400 when the alias already exists, which is
    /// <see cref="UpdateRichMenuAliasAsync"/>'s case.
    /// </returns>
    Task<Result> CreateRichMenuAliasAsync(string aliasId, string richMenuId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 把既有別名改指向另一個選單。
    /// Repoints an existing alias at another menu.
    /// </summary>
    /// <param name="aliasId">別名識別碼。The alias identifier.</param>
    /// <param name="richMenuId">新的選單識別碼。The new menu identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>成功或失敗。Success or failure.</returns>
    Task<Result> UpdateRichMenuAliasAsync(string aliasId, string richMenuId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 刪除圖文選單別名。
    /// Deletes a rich menu alias.
    /// </summary>
    /// <param name="aliasId">別名識別碼。The alias identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>成功或失敗。Success or failure.</returns>
    Task<Result> DeleteRichMenuAliasAsync(string aliasId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢單一別名目前指向哪一個選單。
    /// Reads which menu one alias currently points at.
    /// </summary>
    /// <param name="aliasId">別名識別碼。The alias identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>
    /// 別名內容;別名不存在時為 <see cref="ErrorCategory.NotFound"/> 的失敗。這一點與圖文選單連結的查詢不同
    /// ——「沒有連結」是使用者的正常狀態,「別名不存在」通常是設定漏了一步。
    /// The alias. When it does not exist this is an <see cref="ErrorCategory.NotFound"/> failure, unlike a menu
    /// link lookup: having no link is a normal state for a user, whereas a missing alias usually means a setup
    /// step was skipped.
    /// </returns>
    Task<Result<LineRichMenuAlias>> GetRichMenuAliasAsync(string aliasId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出所有圖文選單別名。
    /// Lists every rich menu alias.
    /// </summary>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>別名清單。The aliases.</returns>
    Task<Result<IReadOnlyList<LineRichMenuAlias>>> GetRichMenuAliasListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次把圖文選單連結到多位使用者。
    /// Links a rich menu to several users at once.
    /// </summary>
    /// <param name="userIds">
    /// 使用者識別碼,1 到 <see cref="LineMessagingLimits.RichMenuBulkUsers"/> 位。
    /// User identifiers, between one and <see cref="LineMessagingLimits.RichMenuBulkUsers"/>.
    /// </param>
    /// <param name="richMenuId">選單識別碼。The menu identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>成功或失敗。Success or failure.</returns>
    /// <remarks>
    /// LINE 的處理是<b>非同步</b>的:回 2xx 只代表工作收下了,不代表每個人的選單都換好了。
    /// 換完沒有事件通知,要確認得逐一問 <see cref="GetRichMenuIdOfUserAsync"/>。
    /// LINE processes this <b>asynchronously</b>: a 2xx means the job was accepted, not that everyone's menu has
    /// changed. No event announces completion, and confirming means asking
    /// <see cref="GetRichMenuIdOfUserAsync"/> one user at a time.
    /// </remarks>
    Task<Result> LinkRichMenuToUsersAsync(IReadOnlyList<string> userIds, string richMenuId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次解除多位使用者的圖文選單連結。
    /// Unlinks several users' rich menus at once.
    /// </summary>
    /// <param name="userIds">
    /// 使用者識別碼,1 到 <see cref="LineMessagingLimits.RichMenuBulkUsers"/> 位。
    /// User identifiers, between one and <see cref="LineMessagingLimits.RichMenuBulkUsers"/>.
    /// </param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>成功或失敗。Success or failure.</returns>
    Task<Result> UnlinkRichMenuFromUsersAsync(IReadOnlyList<string> userIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 驗證圖文選單定義,但不建立。
    /// Validates a rich menu definition without creating it.
    /// </summary>
    /// <param name="richMenu">選單定義。The menu definition.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>
    /// 定義合規時為成功;不合規時的失敗帶有 LINE 指出的欄位(見 <see cref="LineErrorDataKeys.LineDetails"/>)。
    /// Success when the definition conforms; a failure otherwise, carrying the fields LINE named in
    /// <see cref="LineErrorDataKeys.LineDetails"/>.
    /// </returns>
    Task<Result> ValidateRichMenuAsync(LineRichMenu richMenu, CancellationToken cancellationToken = default);
}
