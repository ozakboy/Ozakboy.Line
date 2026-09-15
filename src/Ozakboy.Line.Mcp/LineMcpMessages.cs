using System.Globalization;
using System.Text;
using System.Text.Json;
using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Messaging.Messages;

namespace Ozakboy.Line.Mcp;

/// <summary>
/// 把工具參數(純文字或訊息陣列 JSON)整理成一份可送出的 LINE 訊息陣列。
/// Turns a tool's arguments — either plain text or a message array's JSON — into one sendable LINE message array.
/// </summary>
internal static class LineMcpMessages
{
    /// <summary>
    /// 摘要的長度上限。
    /// The cap on a summary's length.
    /// </summary>
    private const int SummaryLength = 60;

    /// <summary>
    /// 從 <c>text</c> 與 <c>messagesJson</c> 兩個參數中擇一,產出訊息陣列 JSON。
    /// Produces the message array's JSON from exactly one of the <c>text</c> and <c>messagesJson</c> arguments.
    /// </summary>
    /// <param name="text">純文字內容。The plain text.</param>
    /// <param name="messagesJson">訊息陣列 JSON。The message array's JSON.</param>
    /// <returns>
    /// 訊息陣列 JSON;兩個都給或都不給時為 <see cref="LineErrorCodes.InvalidJson"/> 失敗。
    /// The message array's JSON. A <see cref="LineErrorCodes.InvalidJson"/> failure when both or neither is
    /// given.
    /// </returns>
    /// <remarks>
    /// 兩個都給時<b>拒絕</b>而不是挑一個來用。挑一個的話,AI 送了兩份不同的內容、系統安靜地用了其中一份,
    /// 而使用者收到的是另一份以外的那一份 —— 那種落差事後沒有任何線索可循。
    /// Both supplied is a <b>refusal</b> rather than a choice. Picking one means the AI sent two different
    /// bodies, the system quietly used one of them, and what reached the user was the other one — a discrepancy
    /// that leaves no trace to follow afterwards.
    /// </remarks>
    internal static Result<string> Compose(string? text, string? messagesJson)
    {
        var hasText = !string.IsNullOrWhiteSpace(text);
        var hasJson = !string.IsNullOrWhiteSpace(messagesJson);

        if (hasText == hasJson)
        {
            return Error.Validation(
                LineErrorCodes.InvalidJson,
                "text 與 messagesJson 需恰好擇一:text 是純文字內容,messagesJson 是完整的 LINE 訊息陣列。Supply exactly one of text and messagesJson: text is plain text, messagesJson is a complete LINE message array.");
        }

        if (hasJson)
        {
            return Templates.LineTemplateRenderer.Validate(messagesJson!);
        }

        // 純文字包成一則 text 訊息。用 Utf8JsonWriter 寫,引號、換行與控制字元的跳脫就不是這裡要煩的事。
        // Plain text is wrapped into one text message, written with Utf8JsonWriter so that escaping quotes,
        // newlines and control characters is not this code's problem.
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            writer.WriteStartObject();
            writer.WriteString("type", "text");
            writer.WriteString("text", text);
            writer.WriteEndObject();
            writer.WriteEndArray();
        }

        return Result.Success(Encoding.UTF8.GetString(buffer.ToArray()));
    }

    /// <summary>
    /// 把訊息陣列 JSON 轉成訊息物件。
    /// Turns a message array's JSON into message objects.
    /// </summary>
    /// <param name="messagesJson">訊息陣列 JSON。The message array's JSON.</param>
    /// <returns>每則訊息一個 <see cref="RawMessage"/>。One <see cref="RawMessage"/> per message.</returns>
    internal static Result<IReadOnlyList<LineMessage>> Parse(string messagesJson)
    {
        var validated = Templates.LineTemplateRenderer.Validate(messagesJson);
        if (validated.IsFailure)
        {
            return validated.ToFailure<IReadOnlyList<LineMessage>>();
        }

        using var document = JsonDocument.Parse(validated.GetValueOrThrow());

        var messages = new List<LineMessage>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object
                || !element.TryGetProperty("type", out var type)
                || type.ValueKind != JsonValueKind.String)
            {
                return Error.Validation(
                    LineErrorCodes.InvalidJson,
                    "每一則訊息都必須是含有字串 type 欄位的 JSON 物件。Every message must be a JSON object carrying a string type field.");
            }

            messages.Add(new RawMessage(element));
        }

        return Result.Success<IReadOnlyList<LineMessage>>(messages);
    }

    /// <summary>
    /// 產生一行給人看的摘要。
    /// Produces a one-line summary for a person to read.
    /// </summary>
    /// <param name="messagesJson">訊息陣列 JSON。The message array's JSON.</param>
    /// <returns>摘要。The summary.</returns>
    /// <remarks>
    /// 取第一則訊息的文字;不是文字訊息就寫「(型別)訊息」。這一行是審核的人唯一會逐筆讀的東西,
    /// 所以寧可短而準確,也不要把整份 JSON 塞進去 —— 塞進去的結果是沒有人讀。
    /// It takes the first message's text, or names the type when the first message is not text. This line is the
    /// only thing a reviewer reads for every item, so it is better short and accurate than a JSON dump, which
    /// ends up read by nobody.
    /// </remarks>
    internal static string Summarize(string messagesJson)
    {
        try
        {
            using var document = JsonDocument.Parse(messagesJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
            {
                return "(空的訊息內容)";
            }

            var count = document.RootElement.GetArrayLength();
            var first = document.RootElement[0];
            var type = first.TryGetProperty("type", out var typeElement) && typeElement.ValueKind == JsonValueKind.String
                ? typeElement.GetString() ?? "unknown"
                : "unknown";

            var head = string.Equals(type, "text", StringComparison.Ordinal)
                && first.TryGetProperty("text", out var textElement)
                && textElement.ValueKind == JsonValueKind.String
                    ? Truncate(textElement.GetString() ?? string.Empty)
                    : $"({type} 訊息)";

            return count > 1
                ? string.Create(CultureInfo.InvariantCulture, $"{head}(共 {count} 則)")
                : head;
        }
        catch (JsonException)
        {
            // 摘要失敗不該讓整個工具呼叫失敗:內容本身已經在別處驗證過,這裡只是給人看的一行字。
            // A failed summary should not fail the tool call: the body itself is validated elsewhere, and this is
            // one line for a person to read.
            return "(無法摘要的訊息內容)";
        }
    }

    /// <summary>
    /// 截斷過長的文字。
    /// Truncates text that runs long.
    /// </summary>
    /// <param name="text">原文。The text.</param>
    /// <returns>截斷後的文字。The truncated text.</returns>
    private static string Truncate(string text)
    {
        var single = text.ReplaceLineEndings(" ").Trim();
        return single.Length <= SummaryLength ? single : single[..SummaryLength] + "…";
    }
}
