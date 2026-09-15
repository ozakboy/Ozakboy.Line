using System.Globalization;
using System.Text.Json;
using Ozakboy.Core.Abstractions;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// 掛在一則訊息下方的快速回覆按鈕列。
/// The row of quick reply buttons shown under a message.
/// </summary>
/// <remarks>
/// <para>
/// 按鈕是<b>一次性</b>的:使用者按下任何一個、或自己打了字之後,整列就消失。它不是選單,
/// 要一直在的入口請用圖文選單(<see cref="RichMenu.LineRichMenu"/>)。
/// The buttons are <b>one-shot</b>: the whole row disappears once the user taps any of them or types something
/// instead. It is not a menu — an entry point that stays put belongs in a rich menu
/// (<see cref="RichMenu.LineRichMenu"/>).
/// </para>
/// <para>
/// 第一階段這個欄位是 <see cref="JsonElement"/>,呼叫端得自己組 JSON。改成強型別之後,按鈕數上限
/// 與動作的欄位名都由編譯器和 <see cref="Validate"/> 把關,而不是等 LINE 回一個只說「內容有錯」的 400。
/// This field was a <see cref="JsonElement"/> at first and left the caller to assemble the JSON. Typed, the
/// button limit and each action's field names are the compiler's and <see cref="Validate"/>'s business rather
/// than something LINE reports as a 400 saying only that the body is wrong.
/// </para>
/// </remarks>
public sealed class LineQuickReply
{
    /// <summary>
    /// 按鈕,最多 <see cref="LineMessagingLimits.MaxQuickReplyItems"/> 個。
    /// The buttons, up to <see cref="LineMessagingLimits.MaxQuickReplyItems"/> of them.
    /// </summary>
    public IList<LineQuickReplyItem> Items { get; } = [];

    /// <summary>
    /// 檢查按鈕數是否在允許範圍內。
    /// Checks that the number of buttons is within the allowed range.
    /// </summary>
    /// <returns>
    /// 1 到 <see cref="LineMessagingLimits.MaxQuickReplyItems"/> 個時為成功,否則為
    /// <see cref="LineErrorCodes.TooManyQuickReplyItems"/> 失敗。
    /// Success for between one and <see cref="LineMessagingLimits.MaxQuickReplyItems"/> buttons, otherwise a
    /// <see cref="LineErrorCodes.TooManyQuickReplyItems"/> failure.
    /// </returns>
    /// <remarks>
    /// 零個按鈕也算失敗,而不是「當作沒設定」。掛了一個空的快速回覆,幾乎都是迴圈沒跑到或條件寫反了,
    /// 靜靜地送出一則沒有按鈕的訊息只會讓那個 bug 活得更久。
    /// Zero buttons is a failure too, rather than being read as "not set". An empty quick reply almost always
    /// means a loop that did not run or a condition the wrong way round, and quietly sending a message without
    /// buttons only lets that bug live longer.
    /// </remarks>
    public Result Validate() =>
        Items.Count is >= 1 and <= LineMessagingLimits.MaxQuickReplyItems
            ? Result.Success()
            : Error.Validation(
                LineErrorCodes.TooManyQuickReplyItems,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"快速回覆需帶 1 到 {LineMessagingLimits.MaxQuickReplyItems} 個按鈕,這次是 {Items.Count} 個。A quick reply takes between 1 and {LineMessagingLimits.MaxQuickReplyItems} buttons; {Items.Count} were supplied."));

    /// <summary>
    /// 寫出 <c>{ "items": [...] }</c>。
    /// Writes <c>{ "items": [...] }</c>.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    internal void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteStartArray("items");

        for (var index = 0; index < Items.Count; index++)
        {
            Items[index].WriteTo(writer);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
