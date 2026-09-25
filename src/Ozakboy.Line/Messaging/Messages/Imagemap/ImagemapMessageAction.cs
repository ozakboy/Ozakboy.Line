using System.Text.Json;

namespace Ozakboy.Line.Messaging.Messages.Imagemap;

/// <summary>
/// 點了代替使用者送出一段文字的圖片地圖區域。
/// An imagemap area that sends a piece of text on the user's behalf when tapped.
/// </summary>
/// <remarks>
/// 欄位對應 LINE 的 <c>text</c>(最長 400 字元)。送出的文字會出現在聊天室裡,看起來就是使用者自己打的。
/// The field maps to LINE's <c>text</c> (up to 400 characters). The text appears in the chat as though the user
/// typed it.
/// </remarks>
public sealed class ImagemapMessageAction : LineImagemapAction
{
    /// <summary>
    /// 建立送出文字的區域。
    /// Creates a message area.
    /// </summary>
    /// <param name="text">要送出的文字。The text to send.</param>
    /// <param name="area">可點擊的範圍。The tappable area.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="text"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="text"/> is <see langword="null"/> or blank.
    /// </exception>
    public ImagemapMessageAction(string text, LineImagemapArea area)
        : base(area)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Text = text;
    }

    /// <inheritdoc />
    public override string Type => "message";

    /// <summary>
    /// 要送出的文字。
    /// The text to send.
    /// </summary>
    public string Text { get; }

    /// <inheritdoc />
    internal override void WriteBody(Utf8JsonWriter writer) => writer.WriteString("text", Text);
}
