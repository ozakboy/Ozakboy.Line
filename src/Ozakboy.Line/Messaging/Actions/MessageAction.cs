using System.Text.Json;

namespace Ozakboy.Line.Messaging.Actions;

/// <summary>
/// 代替使用者送出一則文字訊息的動作。
/// An action that sends a text message on the user's behalf.
/// </summary>
/// <remarks>
/// 送出的文字會出現在聊天室裡,看起來就是使用者自己打的 —— 需要「按了但不留痕跡」的效果請用
/// <see cref="PostbackAction"/>。
/// The text appears in the chat as though the user typed it. For a tap that leaves no trace, use
/// <see cref="PostbackAction"/>.
/// </remarks>
public sealed class MessageAction : LineAction
{
    /// <summary>
    /// 建立送出訊息的動作。
    /// Creates a message action.
    /// </summary>
    /// <param name="text">要送出的文字。The text to send.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="text"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="text"/> is <see langword="null"/> or blank.
    /// </exception>
    public MessageAction(string text)
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
