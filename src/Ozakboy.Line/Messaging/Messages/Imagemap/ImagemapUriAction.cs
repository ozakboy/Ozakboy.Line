using System.Text.Json;

namespace Ozakboy.Line.Messaging.Messages.Imagemap;

/// <summary>
/// 點了開啟網址的圖片地圖區域。
/// An imagemap area that opens a URL when tapped.
/// </summary>
/// <remarks>
/// 欄位對應 LINE 的 <c>linkUri</c>(最長 1000 字元;<c>http</c>、<c>https</c>、<c>line</c>、<c>tel</c>
/// 都可以)。注意欄位名是 <c>linkUri</c>,不是快速回覆動作用的 <c>uri</c>。
/// The field maps to LINE's <c>linkUri</c> (up to 1000 characters; <c>http</c>, <c>https</c>, <c>line</c> and
/// <c>tel</c> are all accepted). Note the field name is <c>linkUri</c>, not the <c>uri</c> of a quick reply
/// action.
/// </remarks>
public sealed class ImagemapUriAction : LineImagemapAction
{
    /// <summary>
    /// 建立開啟網址的區域。
    /// Creates a URL area.
    /// </summary>
    /// <param name="linkUri">要開啟的位址。The address to open.</param>
    /// <param name="area">可點擊的範圍。The tappable area.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="linkUri"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="linkUri"/> is <see langword="null"/> or blank.
    /// </exception>
    public ImagemapUriAction(string linkUri, LineImagemapArea area)
        : base(area)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(linkUri);
        LinkUri = linkUri;
    }

    /// <inheritdoc />
    public override string Type => "uri";

    /// <summary>
    /// 要開啟的位址。
    /// The address to open.
    /// </summary>
    public string LinkUri { get; }

    /// <inheritdoc />
    internal override void WriteBody(Utf8JsonWriter writer) => writer.WriteString("linkUri", LinkUri);
}
