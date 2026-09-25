namespace Ozakboy.Line;

/// <summary>
/// LINE Messaging API 的數量上限,這些值來自 LINE 的規格而非本套件的選擇。
/// The Messaging API's size limits. These come from LINE's specification, not from this package's choices.
/// </summary>
/// <remarks>
/// 在送出之前先檢查這些上限,是為了讓錯誤發生在呼叫端的機器上而不是 LINE 的伺服器上:
/// 超量的請求在 LINE 那頭會被整批拒絕,連帶浪費一次額度,而本地檢查的錯誤訊息能直接說出「幾筆、上限幾筆」。
/// These are checked before sending so the failure happens on the caller's machine rather than LINE's: an
/// oversized request is rejected wholesale at the far end, costing a slice of quota on the way, whereas a local
/// check can say exactly how many were supplied and how many are allowed.
/// </remarks>
public static class LineMessagingLimits
{
    /// <summary>
    /// 單次 multicast 的收件者人數上限。
    /// The maximum number of recipients in a single multicast.
    /// </summary>
    public const int MulticastRecipients = 500;

    /// <summary>
    /// 單次請求的訊息則數下限。
    /// The minimum number of messages in one request.
    /// </summary>
    public const int MinMessagesPerRequest = 1;

    /// <summary>
    /// 單次請求的訊息則數上限。
    /// The maximum number of messages in one request.
    /// </summary>
    public const int MaxMessagesPerRequest = 5;

    /// <summary>
    /// 一則訊息可掛的快速回覆按鈕數上限。
    /// The maximum number of quick reply buttons one message can carry.
    /// </summary>
    /// <remarks>
    /// 超量時 LINE 不是「只顯示前 13 個」而是整則訊息退回,所以這個上限在本地就先擋。
    /// Going over does not mean LINE shows the first thirteen; it rejects the whole message, which is why this
    /// limit is checked locally first.
    /// </remarks>
    public const int MaxQuickReplyItems = 13;

    /// <summary>
    /// 單次批次連結 / 解除連結圖文選單的使用者人數上限。
    /// The maximum number of users in one bulk rich menu link or unlink.
    /// </summary>
    public const int RichMenuBulkUsers = 500;

    /// <summary>
    /// 按鈕範本(<c>buttons</c>)的動作數上限。
    /// The maximum number of actions on a buttons template.
    /// </summary>
    public const int MaxButtonsTemplateActions = 4;

    /// <summary>
    /// 確認範本(<c>confirm</c>)的動作數,必須恰好是這個數。
    /// The number of actions on a confirm template, which must be exactly this.
    /// </summary>
    public const int ConfirmTemplateActions = 2;

    /// <summary>
    /// 輪播範本(<c>carousel</c>)與圖片輪播範本(<c>image_carousel</c>)的欄數上限。
    /// The maximum number of columns on a carousel or image carousel template.
    /// </summary>
    public const int MaxCarouselColumns = 10;

    /// <summary>
    /// 輪播範本每一欄的動作數上限;所有欄的動作數還必須一致。
    /// The maximum number of actions per carousel column; every column must also carry the same number.
    /// </summary>
    public const int MaxCarouselColumnActions = 3;

    /// <summary>
    /// 圖片地圖(<c>imagemap</c>)的可點擊區域數上限。
    /// The maximum number of tappable areas on an imagemap.
    /// </summary>
    public const int MaxImagemapActions = 50;

    /// <summary>
    /// 圖片地圖底圖的寬度;LINE 規定固定為 1040,高度依比例自訂。
    /// The width of an imagemap's base image, which LINE fixes at 1040; the height follows the aspect ratio.
    /// </summary>
    public const int ImagemapBaseWidth = 1040;
}
