using System.Text.Json;

namespace Ozakboy.Line.Messaging.Actions;

/// <summary>
/// 把一段文字複製到使用者剪貼簿的動作。
/// An action that copies a piece of text to the user's clipboard.
/// </summary>
/// <remarks>
/// 複製這件事發生在使用者的裝置上,伺服器這邊<b>收不到任何事件</b> —— 想知道「有沒有人按了」就得另外
/// 配一顆 <see cref="PostbackAction"/> 按鈕,這個動作本身不會回報。
/// The copy happens on the user's device and the server <b>receives no event at all</b>. Knowing whether anyone
/// tapped it means pairing it with a <see cref="PostbackAction"/>; this action reports nothing by itself.
/// </remarks>
public sealed class ClipboardAction : LineAction
{
    /// <summary>
    /// 建立複製文字的動作。
    /// Creates a clipboard action.
    /// </summary>
    /// <param name="clipboardText">要複製的文字。The text to copy.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="clipboardText"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="clipboardText"/> is <see langword="null"/> or blank.
    /// </exception>
    public ClipboardAction(string clipboardText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clipboardText);
        ClipboardText = clipboardText;
    }

    /// <inheritdoc />
    public override string Type => "clipboard";

    /// <summary>
    /// 要複製的文字。
    /// The text to copy.
    /// </summary>
    public string ClipboardText { get; }

    /// <inheritdoc />
    internal override void WriteBody(Utf8JsonWriter writer) => writer.WriteString("clipboardText", ClipboardText);
}
