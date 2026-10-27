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

/// <summary>
/// 讓每一個具體動作型別都走各自的明確寫法。
/// Routes every concrete action type through its own explicit writing.
/// </summary>
/// <remarks>
/// 與 <see cref="Ozakboy.Line.Messaging.Messages.LineMessage"/> 同理:<see cref="LineAction"/> 上的
/// <c>[JsonConverter]</c> 不會套用到衍生型別(屬性不被繼承),直接序列化 <c>new UriAction(...)</c> 會退回反射而寫出錯誤的 JSON。
/// As with <see cref="Ozakboy.Line.Messaging.Messages.LineMessage"/>: the <c>[JsonConverter]</c> on
/// <see cref="LineAction"/> does not apply to derived types (the attribute is not inherited), so serialising a
/// <c>new UriAction(...)</c> directly falls back to reflection and writes wrong JSON.
/// </remarks>
internal sealed class LineActionJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert is not null && typeToConvert != typeof(LineAction) && typeof(LineAction).IsAssignableFrom(typeToConvert);

    /// <inheritdoc />
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter?)Activator.CreateInstance(typeof(LineActionJsonConverter<>).MakeGenericType(typeToConvert));
}

/// <summary>
/// 具體動作型別的寫出轉換器,只寫不讀。
/// The write-only converter for a concrete action type.
/// </summary>
/// <typeparam name="T">具體動作型別。The concrete action type.</typeparam>
#pragma warning disable CA1812 // 由工廠以反射具現化,分析器看不出來。Instantiated by reflection from the factory; the analyser cannot see it.
internal sealed class LineActionJsonConverter<T> : JsonConverter<T>
    where T : LineAction
#pragma warning restore CA1812
{
    /// <inheritdoc />
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException(
            "具體動作型別不支援反序列化;請以 LineAction 為型別讀回(會得到 RawAction)。Concrete action types cannot be deserialised; read with LineAction as the type (which yields a RawAction).");

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        value.WriteTo(writer);
    }
}
