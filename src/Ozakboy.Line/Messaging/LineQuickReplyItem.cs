using System.Text.Json;
using Ozakboy.Line.Messaging.Actions;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// 快速回覆列上的一個按鈕。
/// One button on a quick reply row.
/// </summary>
/// <remarks>
/// JSON 形狀是 <c>{ "type": "action", "imageUrl"?: …, "action": {…} }</c> —— 最外層的 <c>type</c> 固定是
/// <c>action</c>,真正決定按下去做什麼的是裡面那個 <see cref="Action"/>。這兩層很容易看混,
/// 把動作的 <c>type</c> 寫到外層就會得到一個沒有動作的按鈕。
/// The JSON shape is <c>{ "type": "action", "imageUrl"?: …, "action": {…} }</c>: the outer <c>type</c> is always
/// <c>action</c>, and what the tap actually does is the inner <see cref="Action"/>. The two levels are easy to
/// conflate, and putting the action's <c>type</c> on the outer object yields a button that does nothing.
/// </remarks>
public sealed class LineQuickReplyItem
{
    /// <summary>
    /// 建立一個按鈕。
    /// Creates a button.
    /// </summary>
    /// <param name="action">按下去要做的事。What the tap does.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="action"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="action"/> is <see langword="null"/>.
    /// </exception>
    public LineQuickReplyItem(LineAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Action = action;
    }

    /// <summary>
    /// 按鈕上的小圖示位址;不設定時 LINE 只顯示文字。
    /// The address of the button's icon; without one LINE shows text only.
    /// </summary>
    /// <remarks>
    /// LINE 要求 HTTPS、PNG、正方形且不超過 1 MB。不合規時那個按鈕的圖示就是不出現,沒有錯誤訊息。
    /// LINE asks for HTTPS, PNG, square, and under 1 MB. When it does not comply the icon simply does not appear,
    /// with no error to say so.
    /// </remarks>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// 按下去要做的事。
    /// What the tap does.
    /// </summary>
    public LineAction Action { get; }

    /// <summary>
    /// 寫出這個按鈕。
    /// Writes this button.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    internal void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "action");

        if (!string.IsNullOrWhiteSpace(ImageUrl))
        {
            writer.WriteString("imageUrl", ImageUrl);
        }

        writer.WritePropertyName("action");
        Action.WriteTo(writer);
        writer.WriteEndObject();
    }
}
