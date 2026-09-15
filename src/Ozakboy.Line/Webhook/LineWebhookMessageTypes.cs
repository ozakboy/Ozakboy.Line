namespace Ozakboy.Line.Webhook;

/// <summary>
/// webhook 訊息事件裡的訊息型別字串。
/// The message type strings inside a webhook message event.
/// </summary>
public static class LineWebhookMessageTypes
{
    /// <summary>文字。Text.</summary>
    public const string Text = "text";

    /// <summary>圖片。An image.</summary>
    public const string Image = "image";

    /// <summary>影片。A video.</summary>
    public const string Video = "video";

    /// <summary>語音。An audio clip.</summary>
    public const string Audio = "audio";

    /// <summary>檔案。A file.</summary>
    public const string File = "file";

    /// <summary>位置。A location.</summary>
    public const string Location = "location";

    /// <summary>貼圖。A sticker.</summary>
    public const string Sticker = "sticker";
}
