namespace Ozakboy.Line.Webhook;

/// <summary>
/// 圖片訊息所屬的「圖片組」:使用者一次傳多張圖時,LINE 會拆成多則圖片訊息並用同一組識別碼串起來。
/// The image set an image message belongs to: when a user sends several images at once, LINE splits them into
/// several image messages and ties them together with one identifier.
/// </summary>
public sealed class LineWebhookImageSet
{
    /// <summary>
    /// 圖片組識別碼;同一次傳的圖都一樣。
    /// The image set identifier, the same for every image sent together.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// 這張圖在組內的順序(從 1 起算)。
    /// This image's order within the set, from 1.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// 組內總張數。
    /// How many images the set holds.
    /// </summary>
    public int Total { get; init; }
}
