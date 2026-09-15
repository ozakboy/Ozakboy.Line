namespace Ozakboy.Line.Webhook;

/// <summary>
/// 訊息事件裡的訊息。
/// The message inside a message event.
/// </summary>
/// <remarks>
/// 各種訊息型別的欄位合併在同一個型別裡,用不到的就是 <see langword="null"/>。這是刻意的:
/// 分成七個型別會讓每個處理常式都得先做一次型別判斷與轉型,而實務上絕大多數的處理常式只關心
/// 「是不是文字、內容是什麼」。要看沒有建模的欄位,請取
/// <see cref="LineWebhookEvent.Raw"/>。
/// The fields of every message type live in one type, with the ones that do not apply left
/// <see langword="null"/>. This is deliberate: seven types would make every handler start with a type test and a
/// cast, while in practice almost every handler only asks whether this is text and what it says. Fields that are
/// not modelled are available from <see cref="LineWebhookEvent.Raw"/>.
/// </remarks>
public sealed class LineWebhookMessage
{
    /// <summary>
    /// 訊息識別碼,下載內容時用得到。
    /// The message identifier, used when downloading its content.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// 訊息型別,見 <see cref="LineWebhookMessageTypes"/>。
    /// The message type; see <see cref="LineWebhookMessageTypes"/>.
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// 文字內容,只有文字訊息有。
    /// The text, present only on a text message.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// 引用權杖,可用來在回覆時引用這一則。
    /// The quote token, which a reply can use to quote this message.
    /// </summary>
    public string? QuoteToken { get; init; }

    /// <summary>
    /// 緯度,只有位置訊息有。
    /// The latitude, present only on a location message.
    /// </summary>
    public double? Latitude { get; init; }

    /// <summary>
    /// 經度,只有位置訊息有。
    /// The longitude, present only on a location message.
    /// </summary>
    public double? Longitude { get; init; }

    /// <summary>
    /// 地址,只有位置訊息有。
    /// The address, present only on a location message.
    /// </summary>
    public string? Address { get; init; }

    /// <summary>
    /// 地點名稱,只有位置訊息有。
    /// The place's title, present only on a location message.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// 貼圖包識別碼,只有貼圖訊息有。
    /// The sticker package identifier, present only on a sticker message.
    /// </summary>
    public string? PackageId { get; init; }

    /// <summary>
    /// 貼圖識別碼,只有貼圖訊息有。
    /// The sticker identifier, present only on a sticker message.
    /// </summary>
    public string? StickerId { get; init; }

    /// <summary>
    /// 內容來源,只有媒體訊息有。
    /// The content provider, present only on a media message.
    /// </summary>
    public LineWebhookContentProvider? ContentProvider { get; init; }

    /// <summary>
    /// 長度(毫秒),只有語音與影片訊息有。
    /// The length in milliseconds, present only on audio and video messages.
    /// </summary>
    public int? Duration { get; init; }

    /// <summary>
    /// 檔名,只有檔案訊息有。
    /// The file name, present only on a file message.
    /// </summary>
    public string? FileName { get; init; }

    /// <summary>
    /// 檔案大小(位元組),只有檔案訊息有。
    /// The file size in bytes, present only on a file message.
    /// </summary>
    public long? FileSize { get; init; }
}
