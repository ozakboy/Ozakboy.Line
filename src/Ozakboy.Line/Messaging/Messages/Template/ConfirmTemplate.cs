using System.Globalization;
using System.Text.Json;
using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Messaging.Actions;

namespace Ozakboy.Line.Messaging.Messages.Template;

/// <summary>
/// 確認範本(<c>confirm</c>):一段文字與恰好兩個按鈕。
/// The confirm template (<c>confirm</c>): a piece of text and exactly two buttons.
/// </summary>
/// <remarks>
/// 欄位對應 LINE 的 <c>text</c> 與 <c>actions</c>。動作數必須<b>恰好</b>是
/// <see cref="LineMessagingLimits.ConfirmTemplateActions"/>:少一個 LINE 退回,多一個也退回。
/// The fields map to LINE's <c>text</c> and <c>actions</c>. The action count must be <b>exactly</b>
/// <see cref="LineMessagingLimits.ConfirmTemplateActions"/>: LINE rejects one fewer and one more alike.
/// </remarks>
public sealed class ConfirmTemplate : LineTemplate
{
    /// <summary>
    /// 建立確認範本。
    /// Creates a confirm template.
    /// </summary>
    /// <param name="text">內文。The text.</param>
    /// <param name="first">左邊(第一個)按鈕。The left, first, button.</param>
    /// <param name="second">右邊(第二個)按鈕。The right, second, button.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="text"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="text"/> is <see langword="null"/> or blank.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// 任一按鈕為 <see langword="null"/> 時擲出。Thrown when either button is <see langword="null"/>.
    /// </exception>
    public ConfirmTemplate(string text, LineAction first, LineAction second)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        Text = text;
        Actions = [first, second];
    }

    /// <inheritdoc />
    public override string Type => "confirm";

    /// <summary>
    /// 內文。
    /// The text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// 兩個按鈕。建構時就給齊,這個集合仍可改,但送出時必須恰好兩個。
    /// The two buttons. Both are given at construction; the list can still be edited, but must hold exactly two
    /// when sent.
    /// </summary>
    public IList<LineAction> Actions { get; }

    /// <inheritdoc />
    public override Result Validate() =>
        Actions.Count == LineMessagingLimits.ConfirmTemplateActions
            ? Result.Success()
            : Invalid(string.Create(
                CultureInfo.InvariantCulture,
                $"確認範本需帶恰好 {LineMessagingLimits.ConfirmTemplateActions} 個動作,這次是 {Actions.Count} 個。A confirm template takes exactly {LineMessagingLimits.ConfirmTemplateActions} actions; {Actions.Count} were supplied."));

    /// <inheritdoc />
    internal override void WriteBody(Utf8JsonWriter writer)
    {
        writer.WriteString("text", Text);
        WriteActions(writer, Actions);
    }
}
