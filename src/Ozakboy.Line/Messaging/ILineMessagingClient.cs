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
}
