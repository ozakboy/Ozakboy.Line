using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Messaging.Audience;
using Ozakboy.Line.Messaging.Insight;
using Ozakboy.Line.Messaging.Messages;
using Ozakboy.Line.Messaging.Narrowcast;
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

    /// <summary>
    /// 分頁列出所有好友的使用者識別碼。
    /// Lists every friend's user identifier, one page at a time.
    /// </summary>
    /// <param name="start">上一頁回的 <see cref="LineUserIdsPage.Next"/>;第一頁為 <see langword="null"/>。The previous page's <see cref="LineUserIdsPage.Next"/>, or <see langword="null"/> for the first page.</param>
    /// <param name="limit">每頁筆數,1 到 <see cref="LineMessagingLimits.MaxFollowerIdsPerPage"/>;不給時沿用 LINE 的預設(300)。The page size, 1 to <see cref="LineMessagingLimits.MaxFollowerIdsPerPage"/>; LINE's default (300) when omitted.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>一頁識別碼。One page of identifiers.</returns>
    /// <remarks>
    /// 只有已認證或進階的官方帳號才拿得到,一般帳號 LINE 回 403 —— 那是帳號等級的事實,不是程式錯誤。
    /// Available only to verified or premium official accounts; LINE answers 403 for the rest, which is a fact
    /// about the account's tier rather than a defect in the caller.
    /// </remarks>
    Task<Result<LineUserIdsPage>> GetFollowerIdsAsync(string? start = null, int? limit = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢群組的摘要(名稱與圖片)。
    /// Reads a group's summary: its name and picture.
    /// </summary>
    /// <param name="groupId">群組識別碼。The group identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>摘要。The summary.</returns>
    Task<Result<LineGroupSummary>> GetGroupSummaryAsync(string groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢群組的成員人數(不含官方帳號自己)。
    /// Reads a group's member count, excluding the official account itself.
    /// </summary>
    /// <param name="groupId">群組識別碼。The group identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>人數。The count.</returns>
    Task<Result<int>> GetGroupMemberCountAsync(string groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 分頁列出群組成員的使用者識別碼。
    /// Lists a group's member identifiers, one page at a time.
    /// </summary>
    /// <param name="groupId">群組識別碼。The group identifier.</param>
    /// <param name="start">上一頁回的 <see cref="LineUserIdsPage.Next"/>;第一頁為 <see langword="null"/>。The previous page's <see cref="LineUserIdsPage.Next"/>, or <see langword="null"/> for the first page.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>一頁識別碼。One page of identifiers.</returns>
    /// <remarks>
    /// 與好友清單一樣只開放給已認證或進階帳號。
    /// As with the follower list, available only to verified or premium accounts.
    /// </remarks>
    Task<Result<LineUserIdsPage>> GetGroupMemberIdsAsync(string groupId, string? start = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 讓官方帳號離開群組。
    /// Has the official account leave a group.
    /// </summary>
    /// <param name="groupId">群組識別碼。The group identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>成功或失敗。Success or failure.</returns>
    Task<Result> LeaveGroupAsync(string groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢群組裡某位成員的個人檔案。
    /// Reads one group member's profile.
    /// </summary>
    /// <param name="groupId">群組識別碼。The group identifier.</param>
    /// <param name="userId">使用者識別碼。The user identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>
    /// 個人檔案。與 <see cref="GetProfileAsync"/> 不同,這裡<b>不要求</b>對方是官方帳號的好友,只要同在群組裡。
    /// 回的欄位只有識別碼、顯示名稱與大頭貼;狀態訊息與語言是 <see langword="null"/>。
    /// The profile. Unlike <see cref="GetProfileAsync"/> this does <b>not</b> require the user to be a friend of
    /// the account, only to share the group. Only the id, display name and picture come back; the status message
    /// and language are <see langword="null"/>.
    /// </returns>
    Task<Result<LineUserProfile>> GetGroupMemberProfileAsync(string groupId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢聊天室的成員人數(不含官方帳號自己)。
    /// Reads a room's member count, excluding the official account itself.
    /// </summary>
    /// <param name="roomId">聊天室識別碼。The room identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>人數。The count.</returns>
    Task<Result<int>> GetRoomMemberCountAsync(string roomId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 分頁列出聊天室成員的使用者識別碼。
    /// Lists a room's member identifiers, one page at a time.
    /// </summary>
    /// <param name="roomId">聊天室識別碼。The room identifier.</param>
    /// <param name="start">上一頁回的 <see cref="LineUserIdsPage.Next"/>;第一頁為 <see langword="null"/>。The previous page's <see cref="LineUserIdsPage.Next"/>, or <see langword="null"/> for the first page.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>一頁識別碼。One page of identifiers.</returns>
    Task<Result<LineUserIdsPage>> GetRoomMemberIdsAsync(string roomId, string? start = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 讓官方帳號離開聊天室。
    /// Has the official account leave a room.
    /// </summary>
    /// <param name="roomId">聊天室識別碼。The room identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>成功或失敗。Success or failure.</returns>
    Task<Result> LeaveRoomAsync(string roomId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢聊天室裡某位成員的個人檔案。
    /// Reads one room member's profile.
    /// </summary>
    /// <param name="roomId">聊天室識別碼。The room identifier.</param>
    /// <param name="userId">使用者識別碼。The user identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>個人檔案,欄位範圍同 <see cref="GetGroupMemberProfileAsync"/>。The profile, with the same fields as <see cref="GetGroupMemberProfileAsync"/>.</returns>
    Task<Result<LineUserProfile>> GetRoomMemberProfileAsync(string roomId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 在一對一聊天裡顯示「輸入中」的載入動畫。
    /// Shows the typing-style loading animation in a one-to-one chat.
    /// </summary>
    /// <param name="chatId">使用者識別碼(只支援一對一聊天)。The user identifier; one-to-one chats only.</param>
    /// <param name="loadingSeconds">
    /// 顯示幾秒:<see cref="LineMessagingLimits.MinLoadingSeconds"/> 到
    /// <see cref="LineMessagingLimits.MaxLoadingSeconds"/> 之間、<see cref="LineMessagingLimits.LoadingSecondsStep"/>
    /// 的倍數;不給時沿用 LINE 的預設(20 秒)。
    /// How long to show it: a multiple of <see cref="LineMessagingLimits.LoadingSecondsStep"/> between
    /// <see cref="LineMessagingLimits.MinLoadingSeconds"/> and <see cref="LineMessagingLimits.MaxLoadingSeconds"/>;
    /// LINE's default (20 seconds) when omitted.
    /// </param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>成功或失敗。Success or failure.</returns>
    /// <remarks>
    /// 動畫在官方帳號送出任何訊息時提前結束,而且只有使用者正開著那個聊天室時才看得到。
    /// 秒數不合規在本地就失敗(<see cref="LineErrorCodes.InvalidLoadingSeconds"/>),不送出。
    /// The animation ends early once the account sends any message, and is visible only while the user has that
    /// chat open. Non-conforming seconds fail locally (<see cref="LineErrorCodes.InvalidLoadingSeconds"/>)
    /// without sending.
    /// </remarks>
    Task<Result> StartLoadingAnimationAsync(string chatId, int? loadingSeconds = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 把某位使用者傳來的訊息全部標為已讀。
    /// Marks every message from one user as read.
    /// </summary>
    /// <param name="userId">使用者識別碼。The user identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>成功或失敗。Success or failure.</returns>
    /// <remarks>
    /// 只在官方帳號的已讀模式(<see cref="LineBotInfo.MarkAsReadMode"/>)是 <c>manual</c> 時有意義;
    /// <c>auto</c> 模式下 LINE 自己會標。
    /// Meaningful only when the account's read mode (<see cref="LineBotInfo.MarkAsReadMode"/>) is
    /// <c>manual</c>; in <c>auto</c> mode LINE marks them itself.
    /// </remarks>
    Task<Result> MarkAsReadAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 請 LINE 驗證一批訊息物件,但不送出、不計額度。
    /// Has LINE validate a batch of message objects without sending them or using quota.
    /// </summary>
    /// <param name="target">要模擬的送出方式。The kind of send to simulate.</param>
    /// <param name="messages">訊息,1 到 5 則。The messages, between one and five.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>
    /// 合規時為成功;不合規時的失敗帶有 LINE 指出的欄位(見 <see cref="LineErrorDataKeys.LineDetails"/>)。
    /// 本地能檢查的上限(則數、快速回覆、範本、圖片地圖)會先在本地擋下,不送出。
    /// Success when the batch conforms; otherwise a failure carrying the fields LINE named in
    /// <see cref="LineErrorDataKeys.LineDetails"/>. Limits that can be checked locally (count, quick reply,
    /// template, imagemap) are stopped locally first, without sending.
    /// </returns>
    Task<Result> ValidateMessagesAsync(LineMessageValidationTarget target, IReadOnlyList<LineMessage> messages, CancellationToken cancellationToken = default);

    /// <summary>
    /// 請 LINE 驗證原始 JSON 的訊息,但不送出、不計額度。
    /// Has LINE validate raw JSON messages without sending them or using quota.
    /// </summary>
    /// <param name="target">要模擬的送出方式。The kind of send to simulate.</param>
    /// <param name="messagesJson">單一訊息物件或訊息陣列的 JSON。A single message object or an array of them, as JSON.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>合規時為成功。Success when the batch conforms.</returns>
    Task<Result> ValidateMessagesRawJsonAsync(LineMessageValidationTarget target, string messagesJson, CancellationToken cancellationToken = default);

    /// <summary>
    /// 分眾推播:依受眾或屬性篩選送給一部分好友。
    /// Narrowcasts: sends to a subset of friends chosen by audience or demographics.
    /// </summary>
    /// <param name="messages">訊息,1 到 5 則。The messages, between one and five.</param>
    /// <param name="options">收件對象、篩選、上限與重試鍵。The recipients, filter, cap and retry key.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>
    /// LINE 的請求識別碼(<c>X-Line-Request-Id</c> 標頭),之後用它查 <see cref="GetNarrowcastProgressAsync"/>。
    /// LINE's request id from the <c>X-Line-Request-Id</c> header, used afterwards with
    /// <see cref="GetNarrowcastProgressAsync"/>.
    /// </returns>
    /// <remarks>
    /// 回 2xx 只代表 LINE 收下了,訊息是<b>非同步</b>送出的;成功與失敗人數要看進度端點。
    /// 與推播相同,只有帶了重試鍵才會重試。
    /// A 2xx means only that LINE accepted it; delivery is <b>asynchronous</b>, and the success and failure counts
    /// come from the progress endpoint. As with a push, it is retried only with a retry key.
    /// </remarks>
    Task<Result<string>> NarrowcastAsync(IReadOnlyList<LineMessage> messages, LineNarrowcastOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢一次分眾推播的進度。
    /// Reads the progress of one narrowcast.
    /// </summary>
    /// <param name="requestId"><see cref="NarrowcastAsync"/> 回的請求識別碼。The request id from <see cref="NarrowcastAsync"/>.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>進度。The progress.</returns>
    Task<Result<LineNarrowcastProgress>> GetNarrowcastProgressAsync(string requestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 建立上傳型受眾,成員以使用者識別碼指定。
    /// Creates an upload audience whose members are named by user id.
    /// </summary>
    /// <param name="description">受眾名稱(最長 120 字元,同帳號內不可重複)。The audience name, up to 120 characters, unique within the account.</param>
    /// <param name="userIds">
    /// 成員,0 到 <see cref="LineMessagingLimits.MaxAudienceMembersPerRequest"/> 位;可以先建空的再用
    /// <see cref="AddAudienceGroupMembersAsync"/> 加。
    /// The members, zero to <see cref="LineMessagingLimits.MaxAudienceMembersPerRequest"/>; an empty audience can
    /// be created first and filled with <see cref="AddAudienceGroupMembersAsync"/>.
    /// </param>
    /// <param name="uploadDescription">這批成員的說明;不需要時為 <see langword="null"/>。A note on this batch, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>建立結果,含受眾識別碼。The creation result, including the audience id.</returns>
    /// <remarks>
    /// 受眾要有足夠的成員(LINE 目前要求 100 人以上)才會變成 READY;人數不足的受眾建得起來,但分眾推播會失敗。
    /// An audience becomes READY only with enough members (LINE currently asks for 100 or more); a smaller one
    /// can be created, but a narrowcast to it fails.
    /// </remarks>
    Task<Result<LineAudienceGroupCreated>> CreateUploadAudienceGroupAsync(string description, IReadOnlyList<string> userIds, string? uploadDescription = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 把使用者加進既有的上傳型受眾。
    /// Adds users to an existing upload audience.
    /// </summary>
    /// <param name="audienceGroupId">受眾識別碼。The audience identifier.</param>
    /// <param name="userIds">成員,1 到 <see cref="LineMessagingLimits.MaxAudienceMembersPerRequest"/> 位。The members, one to <see cref="LineMessagingLimits.MaxAudienceMembersPerRequest"/>.</param>
    /// <param name="uploadDescription">這批成員的說明;不需要時為 <see langword="null"/>。A note on this batch, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>成功或失敗。Success or failure.</returns>
    Task<Result> AddAudienceGroupMembersAsync(long audienceGroupId, IReadOnlyList<string> userIds, string? uploadDescription = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢單一受眾。
    /// Reads one audience.
    /// </summary>
    /// <param name="audienceGroupId">受眾識別碼。The audience identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>受眾與對它做過的工作。The audience and the jobs run against it.</returns>
    Task<Result<LineAudienceGroupDetail>> GetAudienceGroupAsync(long audienceGroupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 分頁列出受眾。
    /// Lists audiences, one page at a time.
    /// </summary>
    /// <param name="page">頁碼,從 1 起算。The page number, counting from 1.</param>
    /// <param name="size">每頁筆數,1 到 <see cref="LineMessagingLimits.MaxAudienceGroupsPerPage"/>。The page size, 1 to <see cref="LineMessagingLimits.MaxAudienceGroupsPerPage"/>.</param>
    /// <param name="description">只列名稱含這段文字的受眾;不篩選時為 <see langword="null"/>。Only audiences whose name contains this, or <see langword="null"/> for all.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>一頁受眾。One page of audiences.</returns>
    Task<Result<LineAudienceGroupPage>> GetAudienceGroupListAsync(int page = 1, int size = 20, string? description = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 刪除受眾。
    /// Deletes an audience.
    /// </summary>
    /// <param name="audienceGroupId">受眾識別碼。The audience identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>成功或失敗。Success or failure.</returns>
    Task<Result> DeleteAudienceGroupAsync(long audienceGroupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢某一天各管道的訊息傳送數。
    /// Reads the number of messages delivered on one day, by channel.
    /// </summary>
    /// <param name="date">日期(以帳號所在時區計)。The date, in the account's time zone.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>傳送數;狀態不是 ready 時數字欄位為 <see langword="null"/>。The counts; the numbers are <see langword="null"/> when the status is not ready.</returns>
    Task<Result<LineMessageDeliveryInsight>> GetMessageDeliveryInsightAsync(DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢某一天的好友數。
    /// Reads the number of followers on one day.
    /// </summary>
    /// <param name="date">日期(以帳號所在時區計)。The date, in the account's time zone.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>好友數。The follower figures.</returns>
    Task<Result<LineFollowersInsight>> GetFollowersInsightAsync(DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查詢好友的屬性分布。
    /// Reads the friend demographics.
    /// </summary>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>屬性分布;好友不足 20 人時 <see cref="LineDemographicInsight.Available"/> 為 <see langword="false"/>。The demographics; <see cref="LineDemographicInsight.Available"/> is <see langword="false"/> with fewer than 20 friends.</returns>
    Task<Result<LineDemographicInsight>> GetDemographicInsightAsync(CancellationToken cancellationToken = default);
}
