using System.Text.Json;
using Ozakboy.Core.Abstractions;

namespace Ozakboy.Line.Messaging.Messages.Imagemap;

/// <summary>
/// 圖片地圖上一塊可點擊的區域與點下去的反應。
/// One tappable area on an imagemap and what a tap does.
/// </summary>
/// <remarks>
/// 圖片地圖的動作與快速回覆、圖文選單的 <see cref="Actions.LineAction"/> <b>不是同一組</b>:這裡只有
/// <c>uri</c> 與 <c>message</c> 兩種,而且每個動作自己帶一個 <c>area</c>。用錯那一組會得到一個沒指名欄位的
/// 400,所以這裡另立型別而不是重用。
/// Imagemap actions are <b>not the same set</b> as the <see cref="Actions.LineAction"/> used by quick replies
/// and rich menus: there are only <c>uri</c> and <c>message</c> here, and each action carries its own
/// <c>area</c>. Using the other set earns a 400 that names no field, which is why these are separate types
/// rather than a reuse.
/// </remarks>
public abstract class LineImagemapAction
{
    /// <summary>
    /// 只允許本組件內繼承。
    /// Derivation is limited to this assembly.
    /// </summary>
    /// <param name="area">可點擊的範圍。The tappable area.</param>
    private protected LineImagemapAction(LineImagemapArea area)
    {
        ArgumentNullException.ThrowIfNull(area);
        Area = area;
    }

    /// <summary>
    /// LINE 規格裡的動作型別字串:<c>uri</c> 或 <c>message</c>。
    /// The action type string from LINE's specification: <c>uri</c> or <c>message</c>.
    /// </summary>
    public abstract string Type { get; }

    /// <summary>
    /// 動作的標籤(無障礙用途,最長 50 字元);不設定時為 <see langword="null"/>。
    /// The action's label, for accessibility, up to 50 characters; <see langword="null"/> when unset.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// 可點擊的範圍。
    /// The tappable area.
    /// </summary>
    public LineImagemapArea Area { get; }

    /// <summary>
    /// 把整個動作寫成一個 JSON 物件。
    /// Writes the whole action as one JSON object.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    internal void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", Type);

        if (!string.IsNullOrWhiteSpace(Label))
        {
            writer.WriteString("label", Label);
        }

        WriteBody(writer);
        writer.WritePropertyName("area");
        Area.WriteTo(writer);
        writer.WriteEndObject();
    }

    /// <summary>
    /// 送出前的本地檢查:標籤長度,以及各型別自己的欄位規則。
    /// The local check before sending: the label's length, plus each type's own field rules.
    /// </summary>
    /// <param name="location">動作所在位置的描述,例如 <c>actions[2]</c>。Where the action sits, such as <c>actions[2]</c>.</param>
    /// <returns>
    /// 通過時為成功,否則為 <see cref="LineErrorCodes.InvalidAction"/> 失敗。
    /// Success when it passes, otherwise an <see cref="LineErrorCodes.InvalidAction"/> failure.
    /// </returns>
    internal Result Validate(string location)
    {
        if (LineMessageValidation.IsTooLong(Label, LineMessagingLimits.MaxImagemapActionLabelLength))
        {
            return LineMessageValidation.InvalidAction(
                LineMessageValidation.TooLong($"{location}(type={Type})的 label", Label!, LineMessagingLimits.MaxImagemapActionLabelLength));
        }

        return ValidateBody(location);
    }

    /// <summary>
    /// 檢查這個型別專屬的欄位;預設沒有額外規則。
    /// Checks the fields specific to this type; by default there are no extra rules.
    /// </summary>
    /// <param name="location">動作所在位置的描述。A description of where the action sits.</param>
    /// <returns>通過時為成功。Success when it passes.</returns>
    internal virtual Result ValidateBody(string location) => Result.Success();

    /// <summary>
    /// 寫出這個型別專屬的欄位(不含 <c>type</c>、<c>label</c> 與 <c>area</c>)。
    /// Writes the fields specific to this type, excluding <c>type</c>, <c>label</c> and <c>area</c>.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    internal abstract void WriteBody(Utf8JsonWriter writer);
}
