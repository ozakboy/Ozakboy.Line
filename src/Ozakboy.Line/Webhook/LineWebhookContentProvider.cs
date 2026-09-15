namespace Ozakboy.Line.Webhook;

/// <summary>
/// 媒體訊息的內容來源。
/// Where a media message's content comes from.
/// </summary>
public sealed class LineWebhookContentProvider
{
    /// <summary>
    /// 來源型別:<c>line</c> 表示存在 LINE 的伺服器上,<c>external</c> 表示外部網址。
    /// The provider type: <c>line</c> when LINE holds the content, <c>external</c> when it lives at a URL.
    /// </summary>
    /// <remarks>
    /// 為 <c>line</c> 時要取內容必須呼叫
    /// <see cref="Ozakboy.Line.Messaging.ILineMessagingClient.GetMessageContentAsync"/>,
    /// 而且 LINE 只保存一段時間;為 <c>external</c> 時內容在
    /// <see cref="OriginalContentUrl"/>,與 LINE 無關。
    /// When it is <c>line</c>, the content is fetched with
    /// <see cref="Ozakboy.Line.Messaging.ILineMessagingClient.GetMessageContentAsync"/> and LINE keeps it only
    /// for a while; when it is <c>external</c>, the content sits at <see cref="OriginalContentUrl"/> and has
    /// nothing to do with LINE.
    /// </remarks>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// 外部內容的位址;<see cref="Type"/> 為 <c>line</c> 時為 <see langword="null"/>。
    /// The external content's address, or <see langword="null"/> when <see cref="Type"/> is <c>line</c>.
    /// </summary>
    public string? OriginalContentUrl { get; init; }
}
