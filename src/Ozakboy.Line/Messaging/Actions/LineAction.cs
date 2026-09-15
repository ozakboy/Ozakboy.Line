using System.Text.Json;
using System.Text.Json.Serialization;

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
    /// 寫出這個型別專屬的欄位(不含 <c>type</c> 與 <c>label</c>)。
    /// Writes the fields specific to this type, excluding <c>type</c> and <c>label</c>.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    internal abstract void WriteBody(Utf8JsonWriter writer);
}
