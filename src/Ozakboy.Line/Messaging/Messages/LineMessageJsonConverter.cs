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
