using System.Text.Json;
using System.Text.Json.Serialization;
using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Messaging.Messages;

namespace Ozakboy.Line.Messaging.Actions;

/// <summary>
/// 使用者點下圖文選單區塊或快速回覆按鈕時觸發的動作。
/// The action taken when a user taps a rich menu area or a quick reply button.
/// </summary>
/// <remarks>
/// 與 <see cref="Ozakboy.Line.Messaging.Messages.LineMessage"/> 同理,只在本組件內可以繼承;
/// 尚未建模的動作型別走 <see cref="RawAction"/>。
/// As with <see cref="Ozakboy.Line.Messaging.Messages.LineMessage"/>, derivation is limited to this assembly;
/// an action type not modelled yet goes out through <see cref="RawAction"/>.
/// </remarks>
[JsonConverter(typeof(LineActionJsonConverter))]
public abstract class LineAction
{
    /// <summary>
    /// 只允許本組件內繼承。
    /// Derivation is limited to this assembly.
    /// </summary>
    private protected LineAction()
    {
    }

    /// <summary>
    /// LINE 規格裡的動作型別字串,例如 <c>uri</c>、<c>message</c>、<c>postback</c>。
    /// The action type string from LINE's specification, such as <c>uri</c>, <c>message</c>, or <c>postback</c>.
    /// </summary>
    public abstract string Type { get; }

    /// <summary>
    /// 動作的標籤;某些情境下會顯示給使用者。
    /// The action's label, shown to the user in some contexts.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// 把整個動作寫成一個 JSON 物件。
    /// Writes the whole action as one JSON object.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    internal virtual void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", Type);

        if (!string.IsNullOrWhiteSpace(Label))
        {
            writer.WriteString("label", Label);
        }

        WriteBody(writer);
        writer.WriteEndObject();
    }

    /// <summary>
    /// 送出前的本地檢查:標籤長度,以及各型別自己的欄位規則。
    /// The local check before sending: the label's length, plus each type's own field rules.
    /// </summary>
    /// <param name="maxLabelLength">
    /// 這個動作所在位置允許的標籤字數上限(快速回覆與範本按鈕是 20,圖片輪播是 12)。
    /// The label limit where this action sits — 20 for quick replies and template buttons, 12 in an image carousel.
    /// </param>
    /// <param name="location">
    /// 動作所在位置的描述,放進錯誤訊息,例如 <c>quickReply.items[2].action</c>。
    /// A description of where the action sits, written into the error message, such as
    /// <c>quickReply.items[2].action</c>.
    /// </param>
    /// <returns>
    /// 通過時為成功,否則為 <see cref="LineErrorCodes.InvalidAction"/> 失敗。
    /// Success when it passes, otherwise an <see cref="LineErrorCodes.InvalidAction"/> failure.
    /// </returns>
    /// <remarks>
    /// <see cref="RawAction"/> 覆寫成永遠通過:它是逃生口,內容由呼叫端負責。
    /// <see cref="RawAction"/> overrides this to always pass: it is the escape hatch, and its content is the
    /// caller's responsibility.
    /// </remarks>
    internal virtual Result Validate(int maxLabelLength, string location)
    {
        if (LineMessageValidation.IsTooLong(Label, maxLabelLength))
        {
            return LineMessageValidation.InvalidAction(
                LineMessageValidation.TooLong($"{location}(type={Type})的 label", Label!, maxLabelLength));
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
    /// 寫出這個型別專屬的欄位(不含 <c>type</c> 與 <c>label</c>)。
    /// Writes the fields specific to this type, excluding <c>type</c> and <c>label</c>.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    internal abstract void WriteBody(Utf8JsonWriter writer);
}
