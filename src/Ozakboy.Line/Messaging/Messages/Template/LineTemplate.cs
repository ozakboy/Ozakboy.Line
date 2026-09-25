using System.Text.Json;
using Ozakboy.Core.Abstractions;

namespace Ozakboy.Line.Messaging.Messages.Template;

/// <summary>
/// 範本訊息(<see cref="TemplateMessage"/>)裡的 <c>template</c> 物件。
/// The <c>template</c> object inside a <see cref="TemplateMessage"/>.
/// </summary>
/// <remarks>
/// LINE 有四種範本:<see cref="ButtonsTemplate"/>(<c>buttons</c>)、<see cref="ConfirmTemplate"/>
/// (<c>confirm</c>)、<see cref="CarouselTemplate"/>(<c>carousel</c>)與
/// <see cref="ImageCarouselTemplate"/>(<c>image_carousel</c>)。每一種的動作數與欄數上限都在
/// <see cref="Validate"/> 本地檢查;與 <see cref="LineMessage"/> 同理,只在本組件內可以繼承。
/// LINE has four templates: <see cref="ButtonsTemplate"/> (<c>buttons</c>), <see cref="ConfirmTemplate"/>
/// (<c>confirm</c>), <see cref="CarouselTemplate"/> (<c>carousel</c>) and <see cref="ImageCarouselTemplate"/>
/// (<c>image_carousel</c>). Each one's action and column limits are checked locally by <see cref="Validate"/>;
/// as with <see cref="LineMessage"/>, derivation is limited to this assembly.
/// </remarks>
public abstract class LineTemplate
{
    /// <summary>
    /// 只允許本組件內繼承。
    /// Derivation is limited to this assembly.
    /// </summary>
    private protected LineTemplate()
    {
    }

    /// <summary>
    /// LINE 規格裡的範本型別字串。
    /// The template type string from LINE's specification.
    /// </summary>
    public abstract string Type { get; }

    /// <summary>
    /// 檢查動作數與欄數是否在 LINE 允許的範圍內。
    /// Checks that the action and column counts are within what LINE allows.
    /// </summary>
    /// <returns>
    /// 合規時為成功,否則為 <see cref="LineErrorCodes.InvalidTemplate"/> 失敗。
    /// Success when it conforms, otherwise a <see cref="LineErrorCodes.InvalidTemplate"/> failure.
    /// </returns>
    public abstract Result Validate();

    /// <summary>
    /// 把整個 <c>template</c> 物件寫出來。
    /// Writes the whole <c>template</c> object.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    internal void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", Type);
        WriteBody(writer);
        writer.WriteEndObject();
    }

    /// <summary>
    /// 寫出這個範本專屬的欄位(不含 <c>type</c>)。
    /// Writes the fields specific to this template, excluding <c>type</c>.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    internal abstract void WriteBody(Utf8JsonWriter writer);

    /// <summary>
    /// 產生 <see cref="LineErrorCodes.InvalidTemplate"/> 失敗。
    /// Builds an <see cref="LineErrorCodes.InvalidTemplate"/> failure.
    /// </summary>
    /// <param name="message">失敗原因。Why it failed.</param>
    /// <returns>失敗。The failure.</returns>
    private protected static Result Invalid(string message) =>
        Error.Validation(LineErrorCodes.InvalidTemplate, message);

    /// <summary>
    /// 寫出 <c>actions</c> 陣列。
    /// Writes the <c>actions</c> array.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    /// <param name="actions">動作。The actions.</param>
    private protected static void WriteActions(Utf8JsonWriter writer, IList<Actions.LineAction> actions)
    {
        writer.WriteStartArray("actions");
        for (var index = 0; index < actions.Count; index++)
        {
            actions[index].WriteTo(writer);
        }

        writer.WriteEndArray();
    }

    /// <summary>
    /// 有值時寫出一個字串欄位。
    /// Writes a string field when it has a value.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    /// <param name="name">欄位名。The field name.</param>
    /// <param name="value">值。The value.</param>
    private protected static void WriteOptional(Utf8JsonWriter writer, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            writer.WriteString(name, value);
        }
    }
}
