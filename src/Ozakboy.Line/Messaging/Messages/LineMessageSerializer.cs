using System.Text.Json;

namespace Ozakboy.Line.Messaging.Messages;

/// <summary>
/// 把訊息寫成 LINE 規格的 JSON。
/// Writes messages as the JSON LINE's specification asks for.
/// </summary>
internal static class LineMessageSerializer
{
    /// <summary>
    /// 寫出一個訊息陣列。
    /// Writes an array of messages.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    /// <param name="messages">訊息。The messages.</param>
    internal static void WriteMessages(Utf8JsonWriter writer, IReadOnlyList<LineMessage> messages)
    {
        writer.WriteStartArray();
        for (var index = 0; index < messages.Count; index++)
        {
            messages[index].WriteTo(writer);
        }

        writer.WriteEndArray();
    }

    /// <summary>
    /// 把訊息轉成 <see cref="JsonElement"/> 陣列,供測試與診斷使用。
    /// Converts messages into an array of <see cref="JsonElement"/> for tests and diagnostics.
    /// </summary>
    /// <param name="messages">訊息。The messages.</param>
    /// <returns>每則訊息一個元素。One element per message.</returns>
    internal static JsonElement[] ToJsonElements(IReadOnlyList<LineMessage> messages)
    {
        var elements = new JsonElement[messages.Count];
        for (var index = 0; index < messages.Count; index++)
        {
            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                messages[index].WriteTo(writer);
            }

            using var document = JsonDocument.Parse(buffer.ToArray());
            elements[index] = document.RootElement.Clone();
        }

        return elements;
    }
}
