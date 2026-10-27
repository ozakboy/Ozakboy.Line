using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Messages;

/// <summary>
/// 讓 <see cref="JsonSerializer"/> 走 <see cref="LineMessageSerializer"/> 的明確寫法。
/// Routes <see cref="JsonSerializer"/> through <see cref="LineMessageSerializer"/>'s explicit writing.
/// </summary>
/// <remarks>
/// 只寫不讀。LINE 的訊息在<b>送出</b>方向才是這組型別,<b>收到</b>方向是 webhook 的事件模型 ——
/// 讓它可以讀回來,只會讓人以為這組型別也能拿來解析 webhook,而那是另一組完全不同的 JSON。
/// Write only. These types describe messages on the way <b>out</b>; what comes <b>in</b> is the webhook event
/// model. Making them readable would suggest they can parse a webhook, which is an entirely different JSON shape.
/// </remarks>
internal sealed class LineMessageJsonConverter : JsonConverter<LineMessage>
{
    /// <summary>
    /// 不支援讀取。
    /// Reading is not supported.
    /// </summary>
    /// <param name="reader">JSON 讀取器。The JSON reader.</param>
    /// <param name="typeToConvert">目標型別。The target type.</param>
    /// <param name="options">序列化設定。The serialiser options.</param>
    /// <returns>永遠不會回傳。Never returns.</returns>
    /// <exception cref="NotSupportedException">一律擲出。Always thrown.</exception>
    public override LineMessage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException(
            "送出用的訊息型別不支援反序列化;要解析收到的內容請用 Ozakboy.Line.Webhook 的事件模型。Outgoing message types cannot be deserialised; parse incoming content with the event model in Ozakboy.Line.Webhook.");

    /// <summary>
    /// 寫出訊息。
    /// Writes a message.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    /// <param name="value">訊息。The message.</param>
    /// <param name="options">序列化設定。The serialiser options.</param>
    public override void Write(Utf8JsonWriter writer, LineMessage value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        value.WriteTo(writer);
    }
}

/// <summary>
/// 讓每一個具體訊息型別都走 <see cref="LineMessageSerializer"/> 的明確寫法。
/// Routes every concrete message type through <see cref="LineMessageSerializer"/>'s explicit writing.
/// </summary>
/// <remarks>
/// <see cref="LineMessage"/> 上的 <c>[JsonConverter]</c> 不會套用到衍生型別:System.Text.Json 以泛型參數(或執行時期型別)
/// 取得轉換器,而<b>屬性不被繼承</b>。所以 <c>JsonSerializer.Serialize(new TextMessage("x"))</c> 若沒有這個工廠,
/// 會退回反射,寫出屬性名全是 PascalCase 的錯誤 JSON 且沒有任何錯誤。每個具體型別都掛了這個工廠。
/// The <c>[JsonConverter]</c> on <see cref="LineMessage"/> does not apply to derived types: System.Text.Json picks
/// the converter by the generic argument (or runtime type), and <b>the attribute is not inherited</b>. Without this
/// factory, <c>JsonSerializer.Serialize(new TextMessage("x"))</c> falls back to reflection and writes wrong JSON with
/// PascalCase property names and no error at all. Every concrete type carries this factory.
/// </remarks>
internal sealed class LineMessageJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert is not null && typeToConvert != typeof(LineMessage) && typeof(LineMessage).IsAssignableFrom(typeToConvert);

    /// <inheritdoc />
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter?)Activator.CreateInstance(typeof(LineMessageJsonConverter<>).MakeGenericType(typeToConvert));
}

/// <summary>
/// 具體訊息型別的寫出轉換器,只寫不讀。
/// The write-only converter for a concrete message type.
/// </summary>
/// <typeparam name="T">具體訊息型別。The concrete message type.</typeparam>
#pragma warning disable CA1812 // 由工廠以反射具現化,分析器看不出來。Instantiated by reflection from the factory; the analyser cannot see it.
internal sealed class LineMessageJsonConverter<T> : JsonConverter<T>
    where T : LineMessage
#pragma warning restore CA1812
{
    /// <inheritdoc />
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException(
            "送出用的訊息型別不支援反序列化;要解析收到的內容請用 Ozakboy.Line.Webhook 的事件模型。Outgoing message types cannot be deserialised; parse incoming content with the event model in Ozakboy.Line.Webhook.");

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        value.WriteTo(writer);
    }
}
