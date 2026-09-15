using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Actions;

/// <summary>
/// 動作的 JSON 轉換器:寫出時走各型別的明確寫法,讀回時一律成為 <see cref="RawAction"/>。
/// The action converter: writing goes through each type's explicit writer, and reading always yields a
/// <see cref="RawAction"/>.
/// </summary>
/// <remarks>
/// 讀回來的動作不還原成具體型別,是因為還原必然是不完整的:LINE 隨時可能有本套件沒建模的動作型別,
/// 猜錯就等於在讀取時把欄位丟掉。<see cref="RawAction"/> 保留全部內容,要什麼自己取。
/// A read does not reconstruct the concrete type because any reconstruction would be lossy: LINE may carry an
/// action type this package has not modelled, and guessing wrong drops fields on the way in.
/// <see cref="RawAction"/> keeps everything and lets the caller take what it needs.
/// </remarks>
internal sealed class LineActionJsonConverter : JsonConverter<LineAction>
{
    /// <summary>
    /// 讀成 <see cref="RawAction"/>。
    /// Reads into a <see cref="RawAction"/>.
    /// </summary>
    /// <param name="reader">JSON 讀取器。The JSON reader.</param>
    /// <param name="typeToConvert">目標型別。The target type.</param>
    /// <param name="options">序列化設定。The serialiser options.</param>
    /// <returns>保留全部內容的動作。An action keeping the whole object.</returns>
    /// <exception cref="JsonException">
    /// 內容不是帶 <c>type</c> 欄位的 JSON 物件時擲出。
    /// Thrown when the content is not a JSON object carrying a <c>type</c> field.
    /// </exception>
    public override LineAction Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);

        try
        {
            return new RawAction(document.RootElement);
        }
        catch (ArgumentException exception)
        {
            throw new JsonException(
                "動作必須是含有 type 欄位的 JSON 物件。An action must be a JSON object carrying a type field.",
                exception);
        }
    }

    /// <summary>
    /// 寫出動作。
    /// Writes an action.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    /// <param name="value">動作。The action.</param>
    /// <param name="options">序列化設定。The serialiser options.</param>
    public override void Write(Utf8JsonWriter writer, LineAction value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        value.WriteTo(writer);
    }
}
