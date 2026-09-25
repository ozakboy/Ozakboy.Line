namespace Ozakboy.Line;

/// <summary>
/// LINE 各項服務的端點位址。
/// The endpoint addresses of LINE's services.
/// </summary>
/// <remarks>
/// 端點集中在這裡而不是散落在各用戶端裡,一來測試可以直接比對「這個方法打的是不是這個位址」,
/// 二來文件與程式讀的是同一份常數,不會出現文件寫 A、程式打 B 的情況。
/// The endpoints live here rather than being scattered across the clients: a test can compare directly against
/// the address a method is expected to call, and the documentation and the code read the same constants, so the
/// docs cannot drift away from what is actually sent.
/// </remarks>
public static class LineEndpoints
{
    /// <summary>
    /// LINE Login 的授權頁位址(使用者的瀏覽器會被導到這裡)。
    /// The LINE Login authorization page, where the user's browser is sent.
    /// </summary>
    public const string Authorization = "https://access.line.me/oauth2/v2.1/authorize";

    /// <summary>
    /// 權杖端點:以授權碼換取權杖,以及以 refresh token 續期。
    /// The token endpoint, used both to exchange an authorization code and to refresh.
    /// </summary>
    public const string Token = "https://api.line.me/oauth2/v2.1/token";

    /// <summary>
    /// 撤銷 access token 的端點。
    /// The endpoint that revokes an access token.
    /// </summary>
    public const string Revoke = "https://api.line.me/oauth2/v2.1/revoke";

    /// <summary>
    /// 驗證 access token 的端點(GET,access_token 放在 query)。
    /// The endpoint that verifies an access token (GET, with access_token in the query).
    /// </summary>
    public const string VerifyAccessToken = "https://api.line.me/oauth2/v2.1/verify";

    /// <summary>
    /// 由 LINE 遠端驗證 id_token 的端點(POST,表單內容)。
    /// The endpoint that verifies an id_token remotely (POST, form encoded).
    /// </summary>
    /// <remarks>
    /// 位址與 <see cref="VerifyAccessToken"/> 相同,差別只在 HTTP 方法。兩個常數刻意分開命名,
    /// 因為呼叫端讀到的是「我要驗哪一種權杖」,而不是「我要打哪一個字串」。
    /// The address is the same as <see cref="VerifyAccessToken"/> and only the HTTP method differs. They are
    /// kept as two names on purpose: the caller reads which kind of token is being verified, not which string is
    /// being called.
    /// </remarks>
    public const string VerifyIdToken = "https://api.line.me/oauth2/v2.1/verify";

    /// <summary>
    /// 以 access token 取得使用者個人檔案的端點。
    /// The endpoint returning the user's profile for an access token.
    /// </summary>
    public const string Profile = "https://api.line.me/v2/profile";

    /// <summary>
    /// OpenID Connect 的 userinfo 端點(需要 openid 權限範圍)。
    /// The OpenID Connect userinfo endpoint, which requires the openid scope.
    /// </summary>
    public const string UserInfo = "https://api.line.me/oauth2/v2.1/userinfo";

    /// <summary>
    /// 查詢使用者是否已加官方帳號好友的端點。
    /// The endpoint reporting whether the user has added the official account as a friend.
    /// </summary>
    /// <remarks>
    /// 只有在 Login channel 設定了 Linked OA 時才會回 2xx;沒設定時 LINE 回 4xx,
    /// 這不是程式錯誤,而是頻道設定的事實。
    /// This returns 2xx only when the Login channel has a linked official account. Without one LINE answers 4xx,
    /// which is a fact about the channel's configuration rather than a defect in the caller.
    /// </remarks>
    public const string FriendshipStatus = "https://api.line.me/friendship/v1/status";

    /// <summary>
    /// Messaging API 的基底位址。
    /// The base address of the Messaging API.
    /// </summary>
    public const string MessagingApiBase = "https://api.line.me";

    /// <summary>
    /// Messaging API 的資料端點基底位址(訊息內容下載、圖文選單圖片上傳走這裡)。
    /// The base address of the Messaging API's data endpoints, used for message content downloads and rich menu
    /// image uploads.
    /// </summary>
    public const string MessagingDataApiBase = "https://api-data.line.me";

    /// <summary>
    /// 推播訊息給單一對象。
    /// Pushes messages to a single recipient.
    /// </summary>
    public const string PushMessage = "https://api.line.me/v2/bot/message/push";

    /// <summary>
    /// 推播訊息給多個使用者(單次上限 <see cref="LineMessagingLimits.MulticastRecipients"/> 人)。
    /// Pushes messages to several users, up to <see cref="LineMessagingLimits.MulticastRecipients"/> per call.
    /// </summary>
    public const string MulticastMessage = "https://api.line.me/v2/bot/message/multicast";

    /// <summary>
    /// 廣播訊息給所有好友。
    /// Broadcasts messages to every friend.
    /// </summary>
    public const string BroadcastMessage = "https://api.line.me/v2/bot/message/broadcast";

    /// <summary>
    /// 以 reply token 回覆訊息(不計推播額度)。
    /// Replies with a reply token, which does not count against the push quota.
    /// </summary>
    public const string ReplyMessage = "https://api.line.me/v2/bot/message/reply";

    /// <summary>
    /// 查詢本月推播額度。
    /// Returns this month's push quota.
    /// </summary>
    public const string MessageQuota = "https://api.line.me/v2/bot/message/quota";

    /// <summary>
    /// 查詢本月已使用的推播則數。
    /// Returns the number of messages already sent this month.
    /// </summary>
    public const string MessageQuotaConsumption = "https://api.line.me/v2/bot/message/quota/consumption";

    /// <summary>
    /// 查詢機器人(官方帳號)自身資訊。
    /// Returns information about the bot, that is, the official account itself.
    /// </summary>
    public const string BotInfo = "https://api.line.me/v2/bot/info";

    /// <summary>
    /// 查詢好友的個人檔案(路徑後接 userId)。
    /// Returns a friend's profile; the user id is appended to the path.
    /// </summary>
    public const string BotProfileBase = "https://api.line.me/v2/bot/profile/";

    /// <summary>
    /// 下載訊息內容(路徑為 <c>/v2/bot/message/{messageId}/content</c>)。
    /// Downloads a message's content at <c>/v2/bot/message/{messageId}/content</c>.
    /// </summary>
    public const string MessageContentBase = "https://api-data.line.me/v2/bot/message/";

    /// <summary>
    /// 圖文選單的建立、查詢與刪除端點。
    /// The endpoint that creates, reads, and deletes rich menus.
    /// </summary>
    public const string RichMenu = "https://api.line.me/v2/bot/richmenu";

    /// <summary>
    /// 列出所有圖文選單。
    /// Lists every rich menu.
    /// </summary>
    public const string RichMenuList = "https://api.line.me/v2/bot/richmenu/list";

    /// <summary>
    /// 上傳圖文選單圖片(路徑為 <c>/v2/bot/richmenu/{richMenuId}/content</c>)。
    /// Uploads a rich menu image at <c>/v2/bot/richmenu/{richMenuId}/content</c>.
    /// </summary>
    public const string RichMenuContentBase = "https://api-data.line.me/v2/bot/richmenu/";

    /// <summary>
    /// 圖文選單別名的建立、更新、查詢與刪除端點。
    /// The endpoint that creates, updates, reads, and deletes rich menu aliases.
    /// </summary>
    /// <remarks>
    /// 別名是「以新換舊」的關鍵:每次替換的選單都是新的識別碼,但別名可以一直指向最新那一個。
    /// 選單之間互相切換的動作(<c>richmenuswitch</c>)因此寫別名而不寫識別碼 ——
    /// 否則換一次選單就得把所有動作裡的識別碼全部改一遍。
    /// Aliases are what makes swapping a menu practical: every replacement is a new menu id, but an alias can
    /// keep pointing at the newest one. The switch action (<c>richmenuswitch</c>) therefore names an alias rather
    /// than an id — otherwise one replacement means rewriting the id inside every action.
    /// </remarks>
    public const string RichMenuAlias = "https://api.line.me/v2/bot/richmenu/alias";

    /// <summary>
    /// 列出所有圖文選單別名。
    /// Lists every rich menu alias.
    /// </summary>
    public const string RichMenuAliasList = "https://api.line.me/v2/bot/richmenu/alias/list";

    /// <summary>
    /// 批次把圖文選單連結到多位使用者(單次上限 <see cref="LineMessagingLimits.RichMenuBulkUsers"/> 人)。
    /// Links a rich menu to several users at once, up to
    /// <see cref="LineMessagingLimits.RichMenuBulkUsers"/> per call.
    /// </summary>
    public const string RichMenuBulkLink = "https://api.line.me/v2/bot/richmenu/bulk/link";

    /// <summary>
    /// 批次解除多位使用者的圖文選單連結。
    /// Unlinks several users' rich menus at once.
    /// </summary>
    public const string RichMenuBulkUnlink = "https://api.line.me/v2/bot/richmenu/bulk/unlink";

    /// <summary>
    /// 驗證圖文選單定義,但不建立。
    /// Validates a rich menu definition without creating it.
    /// </summary>
    /// <remarks>
    /// 區塊超出邊界、尺寸不合規這類問題在這裡就看得到,不必先建一個選單再把它刪掉。
    /// Out-of-bounds areas and non-conforming sizes show up here, without creating a menu only to delete it.
    /// </remarks>
    public const string RichMenuValidate = "https://api.line.me/v2/bot/richmenu/validate";

    /// <summary>
    /// 預設圖文選單的設定、清除與查詢端點。
    /// The endpoint that sets, clears, and reads the default rich menu.
    /// </summary>
    public const string DefaultRichMenu = "https://api.line.me/v2/bot/user/all/richmenu";

    /// <summary>
    /// 單一使用者的圖文選單連結端點基底(路徑為 <c>/v2/bot/user/{userId}/richmenu</c>)。
    /// The base for per-user rich menu links at <c>/v2/bot/user/{userId}/richmenu</c>.
    /// </summary>
    public const string UserRichMenuBase = "https://api.line.me/v2/bot/user/";

    /// <summary>
    /// 好友清單(GET,query 為 <c>start</c> 與 <c>limit</c>)。
    /// The follower list (GET, with <c>start</c> and <c>limit</c> in the query).
    /// </summary>
    /// <remarks>
    /// 只有已認證或進階的官方帳號才拿得到;一般帳號 LINE 回 403。
    /// Available only to verified or premium official accounts; LINE answers 403 for the rest.
    /// </remarks>
    public const string FollowerIds = "https://api.line.me/v2/bot/followers/ids";

    /// <summary>
    /// 群組端點的基底(路徑為 <c>/v2/bot/group/{groupId}/summary</c>、<c>/members/count</c>、
    /// <c>/members/ids</c>、<c>/leave</c> 與 <c>/member/{userId}</c>)。
    /// The base of the group endpoints: <c>/v2/bot/group/{groupId}/summary</c>, <c>/members/count</c>,
    /// <c>/members/ids</c>, <c>/leave</c> and <c>/member/{userId}</c>.
    /// </summary>
    public const string GroupBase = "https://api.line.me/v2/bot/group/";

    /// <summary>
    /// 聊天室端點的基底(路徑為 <c>/v2/bot/room/{roomId}/members/count</c>、<c>/members/ids</c>、
    /// <c>/leave</c> 與 <c>/member/{userId}</c>;聊天室沒有摘要端點)。
    /// The base of the room endpoints: <c>/v2/bot/room/{roomId}/members/count</c>, <c>/members/ids</c>,
    /// <c>/leave</c> and <c>/member/{userId}</c>. Rooms have no summary endpoint.
    /// </summary>
    public const string RoomBase = "https://api.line.me/v2/bot/room/";

    /// <summary>
    /// 顯示載入動畫(POST,內容為 <c>chatId</c> 與 <c>loadingSeconds</c>)。
    /// Shows the loading animation (POST, with <c>chatId</c> and <c>loadingSeconds</c> in the body).
    /// </summary>
    public const string ChatLoadingStart = "https://api.line.me/v2/bot/chat/loading/start";

    /// <summary>
    /// 把使用者的訊息標為已讀(POST,內容為 <c>chat.userId</c>)。
    /// Marks a user's messages as read (POST, with <c>chat.userId</c> in the body).
    /// </summary>
    public const string MarkAsRead = "https://api.line.me/v2/bot/message/markAsRead";

    /// <summary>
    /// 訊息驗證端點的基底(路徑後接 <c>push</c>、<c>multicast</c>、<c>broadcast</c>、<c>reply</c> 或
    /// <c>narrowcast</c>)。只驗訊息物件,不送出、不計額度。
    /// The base of the message validation endpoints, followed by <c>push</c>, <c>multicast</c>,
    /// <c>broadcast</c>, <c>reply</c> or <c>narrowcast</c>. It checks the message objects only: nothing is sent and
    /// no quota is used.
    /// </summary>
    public const string ValidateMessageBase = "https://api.line.me/v2/bot/message/validate/";

    /// <summary>
    /// 分眾推播(POST;回應是 202,請求識別碼在 <c>X-Line-Request-Id</c> 標頭)。
    /// Narrowcast (POST; the response is a 202 whose request id is in the <c>X-Line-Request-Id</c> header).
    /// </summary>
    public const string NarrowcastMessage = "https://api.line.me/v2/bot/message/narrowcast";

    /// <summary>
    /// 查詢分眾推播的進度(GET,query 為 <c>requestId</c>)。
    /// The narrowcast progress (GET, with <c>requestId</c> in the query).
    /// </summary>
    public const string NarrowcastProgress = "https://api.line.me/v2/bot/message/progress/narrowcast";

    /// <summary>
    /// 上傳型受眾:POST 建立、PUT 加成員。
    /// Upload audiences: POST creates one, PUT adds members.
    /// </summary>
    public const string AudienceGroupUpload = "https://api.line.me/v2/bot/audienceGroup/upload";

    /// <summary>
    /// 受眾端點的基底(路徑後接 <c>{audienceGroupId}</c>,GET 查詢、DELETE 刪除)。
    /// The base of the audience endpoints, followed by <c>{audienceGroupId}</c>: GET reads, DELETE deletes.
    /// </summary>
    public const string AudienceGroupBase = "https://api.line.me/v2/bot/audienceGroup/";

    /// <summary>
    /// 列出受眾(GET,query 為 <c>page</c>、<c>size</c> 與可選的 <c>description</c>)。
    /// Lists audiences (GET, with <c>page</c>, <c>size</c> and an optional <c>description</c> in the query).
    /// </summary>
    public const string AudienceGroupList = "https://api.line.me/v2/bot/audienceGroup/list";

    /// <summary>
    /// 某一天的訊息傳送數(GET,query 為 <c>date</c>,格式 <c>yyyyMMdd</c>)。
    /// The number of messages delivered on one day (GET, with <c>date</c> as <c>yyyyMMdd</c> in the query).
    /// </summary>
    public const string InsightMessageDelivery = "https://api.line.me/v2/bot/insight/message/delivery";

    /// <summary>
    /// 某一天的好友數(GET,query 為 <c>date</c>,格式 <c>yyyyMMdd</c>)。
    /// The number of followers on one day (GET, with <c>date</c> as <c>yyyyMMdd</c> in the query).
    /// </summary>
    public const string InsightFollowers = "https://api.line.me/v2/bot/insight/followers";

    /// <summary>
    /// 好友的屬性分布(GET,無參數)。
    /// The friend demographics (GET, no parameters).
    /// </summary>
    public const string InsightDemographic = "https://api.line.me/v2/bot/insight/demographic";

    /// <summary>
    /// id_token 的發行者,本地驗證時必須逐字相符。
    /// The id_token issuer, which local validation compares verbatim.
    /// </summary>
    public const string IdTokenIssuer = "https://access.line.me";
}
