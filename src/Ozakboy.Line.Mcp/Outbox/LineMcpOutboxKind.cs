namespace Ozakboy.Line.Mcp.Outbox;

/// <summary>
/// 待發項目要做的是哪一件事。
/// What a queued item will do.
/// </summary>
/// <remarks>
/// 種類決定核准之後呼叫 <see cref="Ozakboy.Line.Messaging.ILineMessagingClient"/> 的哪一個方法,
/// 也決定 <see cref="LineMcpOutboxItem.PayloadJson"/> 的形狀。
/// The kind decides which <see cref="Ozakboy.Line.Messaging.ILineMessagingClient"/> method runs on approval, and
/// what shape <see cref="LineMcpOutboxItem.PayloadJson"/> takes.
/// </remarks>
public enum LineMcpOutboxKind
{
    /// <summary>
    /// 推播給單一對象。內容為 <c>{ "to": "…", "messages": [...] }</c>。
    /// A push to one recipient. The payload is <c>{ "to": "…", "messages": [...] }</c>.
    /// </summary>
    Push = 0,

    /// <summary>
    /// 推播給多位使用者。內容為 <c>{ "userIds": [...], "messages": [...] }</c>。
    /// A push to several users. The payload is <c>{ "userIds": [...], "messages": [...] }</c>.
    /// </summary>
    Multicast = 1,

    /// <summary>
    /// 廣播給所有好友。內容為 <c>{ "messages": [...] }</c>。
    /// A broadcast to every friend. The payload is <c>{ "messages": [...] }</c>.
    /// </summary>
    Broadcast = 2,

    /// <summary>
    /// 替換圖文選單。內容為
    /// <c>{ "menu": {...}, "imageUrl": "…", "setAsDefault": bool, "oldRichMenuId": "…", "aliasId": "…" }</c>。
    /// A rich menu replacement. The payload is
    /// <c>{ "menu": {...}, "imageUrl": "…", "setAsDefault": bool, "oldRichMenuId": "…", "aliasId": "…" }</c>.
    /// </summary>
    /// <remarks>
    /// 圖片存的是<b>位址</b>而不是位元組:待發項目會被存進 JSON 檔或資料庫,把一張圖的 base64
    /// 塞進去會讓整份佇列難以閱讀與備份。圖片在核准的那一刻才抓。
    /// The image is kept as an <b>address</b> rather than bytes: a queued item goes into a JSON file or a
    /// database, and a base64 image in there makes the whole queue hard to read and to back up. The image is
    /// fetched at the moment of approval.
    /// </remarks>
    RichMenuReplace = 3,

    /// <summary>
    /// 建立 LIFF 應用程式。內容為 <c>{ "viewType": "…", "url": "…", "description": "…", "botPrompt": "…" }</c>
    /// (<c>botPrompt</c> 選填)。
    /// A LIFF app creation. The payload is
    /// <c>{ "viewType": "…", "url": "…", "description": "…", "botPrompt": "…" }</c> (<c>botPrompt</c> optional).
    /// </summary>
    /// <remarks>
    /// 數值接在既有項目之後,0.2.0 寫下的 JSON 檔照常讀得出來。
    /// The value follows the existing ones, so a JSON file written by 0.2.0 still reads.
    /// </remarks>
    LiffCreate = 4,

    /// <summary>
    /// 刪除 LIFF 應用程式。內容為 <c>{ "liffId": "…" }</c>。
    /// A LIFF app deletion. The payload is <c>{ "liffId": "…" }</c>.
    /// </summary>
    LiffDelete = 5,
}
