namespace Ozakboy.Line;

/// <summary>
/// LINE Messaging API 的數量上限(訊息與動作的欄位長度)。
/// The Messaging API's size limits — field lengths of messages and actions.
/// </summary>
public static partial class LineMessagingLimits
{
    /// <summary>
    /// 文字訊息(<c>text</c> 與 <c>textV2</c>)的字數上限。
    /// The character limit of a text message (<c>text</c> and <c>textV2</c>).
    /// </summary>
    public const int MaxTextLength = 5000;

    /// <summary>
    /// 單則文字訊息可帶的 LINE emoji 數上限(<c>text</c> 的 <c>emojis</c>,以及 <c>textV2</c> 的 emoji 替換項)。
    /// The most LINE emojis one text message may carry — the <c>emojis</c> of <c>text</c> and the emoji
    /// substitutions of <c>textV2</c>.
    /// </summary>
    public const int MaxTextEmojis = 20;

    /// <summary>
    /// 單則 <c>textV2</c> 訊息可帶的 mention 數上限。
    /// The most mentions one <c>textV2</c> message may carry.
    /// </summary>
    public const int MaxTextV2Mentions = 20;

    /// <summary>
    /// 範本、圖片地圖與 Flex 訊息的替代文字(<c>altText</c>)字數上限。
    /// The character limit of the <c>altText</c> of template, imagemap and flex messages.
    /// </summary>
    public const int MaxAltTextLength = 400;

    /// <summary>
    /// 位置訊息的標題字數上限。
    /// The character limit of a location message's title.
    /// </summary>
    public const int MaxLocationTitleLength = 100;

    /// <summary>
    /// 位置訊息的地址字數上限。
    /// The character limit of a location message's address.
    /// </summary>
    public const int MaxLocationAddressLength = 100;

    /// <summary>
    /// 媒體位址(圖片、影片、語音、快速回覆圖示)的長度上限,而且必須是 HTTPS。
    /// The length limit of a media address — image, video, audio, quick reply icon — which must also be HTTPS.
    /// </summary>
    public const int MaxMediaUrlLength = 2000;

    /// <summary>
    /// 動作標籤(<c>label</c>)的字數上限:快速回覆、範本按鈕與圖文選單適用。
    /// The character limit of an action <c>label</c>, for quick replies, template buttons and rich menus.
    /// </summary>
    public const int MaxActionLabelLength = 20;

    /// <summary>
    /// 圖片輪播範本裡動作標籤的字數上限。
    /// The character limit of an action label inside an image carousel template.
    /// </summary>
    public const int MaxImageCarouselActionLabelLength = 12;

    /// <summary>
    /// 圖片地圖動作標籤的字數上限。
    /// The character limit of an imagemap action label.
    /// </summary>
    public const int MaxImagemapActionLabelLength = 50;

    /// <summary>
    /// 開啟網址動作的位址長度上限。
    /// The length limit of the address of a URI action.
    /// </summary>
    public const int MaxActionUriLength = 1000;

    /// <summary>
    /// 回傳動作 <c>data</c> 的字數上限。
    /// The character limit of a postback action's <c>data</c>.
    /// </summary>
    public const int MaxPostbackDataLength = 300;

    /// <summary>
    /// 訊息動作 <c>text</c> 的字數上限。
    /// The character limit of a message action's <c>text</c>.
    /// </summary>
    public const int MaxMessageActionTextLength = 300;

    /// <summary>
    /// 圖片地圖訊息動作 <c>text</c> 的字數上限。
    /// The character limit of an imagemap message action's <c>text</c>.
    /// </summary>
    public const int MaxImagemapMessageTextLength = 400;
}
