using System.Text.Json;
using Ozakboy.Line.Messaging.Actions;

namespace Ozakboy.Line.Messaging.Messages.Flex;

/// <summary>
/// Flex 模型逐欄位寫出 JSON 時共用的小工具:null 的欄位一律不輸出。
/// Small helpers shared by the Flex model's field-by-field JSON writing: a null field is never written.
/// </summary>
internal static class FlexJson
{
    /// <summary>
    /// 有值才寫出字串欄位。
    /// Writes a string field only when it has a value.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    /// <param name="name">欄位名。The field name.</param>
    /// <param name="value">值。The value.</param>
    internal static void WriteString(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(name, value);
        }
    }

    /// <summary>
    /// 有值才寫出整數欄位。
    /// Writes an integer field only when it has a value.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    /// <param name="name">欄位名。The field name.</param>
    /// <param name="value">值。The value.</param>
    internal static void WriteInt(Utf8JsonWriter writer, string name, int? value)
    {
        if (value is { } number)
        {
            writer.WriteNumber(name, number);
        }
    }

    /// <summary>
    /// 有值才寫出布林欄位。
    /// Writes a boolean field only when it has a value.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    /// <param name="name">欄位名。The field name.</param>
    /// <param name="value">值。The value.</param>
    internal static void WriteBool(Utf8JsonWriter writer, string name, bool? value)
    {
        if (value is { } flag)
        {
            writer.WriteBoolean(name, flag);
        }
    }

    /// <summary>
    /// 有值才寫出動作欄位。
    /// Writes an action field only when it has a value.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    /// <param name="name">欄位名。The field name.</param>
    /// <param name="value">動作。The action.</param>
    internal static void WriteAction(Utf8JsonWriter writer, string name, LineAction? value)
    {
        if (value is not null)
        {
            writer.WritePropertyName(name);
            value.WriteTo(writer);
        }
    }
}
