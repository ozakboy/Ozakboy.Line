using System.Text.Json;
using Ozakboy.Core.Abstractions;

namespace Ozakboy.Line.Webhook;

/// <summary>
/// 把 webhook 請求內容解析成事件。
/// Parses a webhook request body into events.
/// </summary>
/// <remarks>
/// 解析<b>不擋任何事件</b>。沒見過的事件型別、沒建模的欄位、少了某個欄位,都不會讓整批失敗 ——
/// 解不出來的部分留在 <see cref="LineWebhookEvent.Raw"/> 裡。整批解析失敗只有一種情況:內容根本不是
/// 預期形狀的 JSON,那時請求本來就不可信。
/// Parsing <b>refuses nothing</b>. An unfamiliar event type, an unmodelled field, a missing field: none of them
/// fails the batch, and whatever could not be read stays in <see cref="LineWebhookEvent.Raw"/>. There is only one
/// way for the whole parse to fail — the body is not JSON of the expected shape — and at that point the request
/// was never trustworthy.
/// </remarks>
public static class LineWebhookParser
{
    /// <summary>
    /// 解析請求內容。
    /// Parses a request body.
    /// </summary>
    /// <param name="body">原始請求內容。The raw request body.</param>
    /// <returns>解析結果。The parsed payload.</returns>
    public static Result<LineWebhookPayload> Parse(ReadOnlySpan<byte> body)
    {
        JsonDocument document;
        try
        {
            var reader = new Utf8JsonReader(body);
            document = JsonDocument.ParseValue(ref reader);
        }
        catch (JsonException exception)
        {
            return InvalidPayload(exception);
        }

        using (document)
        {
            return ReadPayload(document.RootElement);
        }
    }

    /// <summary>
    /// 解析請求內容(字串多載)。
    /// Parses a request body, taking it as a string.
    /// </summary>
    /// <param name="body">原始請求內容。The raw request body.</param>
    /// <returns>解析結果。The parsed payload.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="body"/> 為 <see langword="null"/> 時擲出。Thrown when <paramref name="body"/> is <see langword="null"/>.
    /// </exception>
    public static Result<LineWebhookPayload> Parse(string body)
    {
        ArgumentNullException.ThrowIfNull(body);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException exception)
        {
            return InvalidPayload(exception);
        }

        using (document)
        {
            return ReadPayload(document.RootElement);
        }
    }

    /// <summary>
    /// 從根物件讀出整批內容。
    /// Reads the whole payload from the root object.
    /// </summary>
    /// <param name="root">根元素。The root element.</param>
    /// <returns>解析結果。The parsed payload.</returns>
    private static Result<LineWebhookPayload> ReadPayload(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return Error.Validation(
                LineErrorCodes.WebhookInvalidPayload,
                "webhook 內容必須是一個 JSON 物件。A webhook body must be a JSON object.");
        }

        var events = new List<LineWebhookEvent>();
        if (root.TryGetProperty("events", out var eventsElement) && eventsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in eventsElement.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    events.Add(ReadEvent(element));
                }
            }
        }

        return Result.Success(new LineWebhookPayload
        {
            Destination = ReadString(root, "destination") ?? string.Empty,
            Events = events,
        });
    }

    /// <summary>
    /// 讀出一個事件。
    /// Reads one event.
    /// </summary>
    /// <param name="element">事件元素。The event element.</param>
    /// <returns>事件。The event.</returns>
    private static LineWebhookEvent ReadEvent(JsonElement element)
    {
        var deliveryContext = element.TryGetProperty("deliveryContext", out var context)
            && context.ValueKind == JsonValueKind.Object
                ? context
                : default;

        return new LineWebhookEvent
        {
            Type = ReadString(element, "type") ?? string.Empty,
            Mode = ReadString(element, "mode") ?? string.Empty,
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(ReadInt64(element, "timestamp") ?? 0L),
            WebhookEventId = ReadString(element, "webhookEventId") ?? string.Empty,
            IsRedelivery = deliveryContext.ValueKind == JsonValueKind.Object
                && ReadBoolean(deliveryContext, "isRedelivery") == true,
            ReplyToken = ReadString(element, "replyToken"),
            Source = ReadSource(element),
            Message = ReadMessage(element),
            Postback = ReadPostback(element),
            Follow = ReadFollow(element),

            // Clone 之後才保存:JsonElement 的生命週期綁在 JsonDocument 上,而那份 document 在
            // Parse 回傳之前就已經釋放,不 Clone 的話呼叫端拿到的是一個會擲出例外的空殼。
            // Cloned before being kept: a JsonElement's lifetime is tied to its JsonDocument, which is disposed
            // before Parse returns; without the clone the caller would hold a shell that throws.
            Raw = element.Clone(),
        };
    }

    /// <summary>
    /// 讀出事件來源。
    /// Reads the event's source.
    /// </summary>
    /// <param name="element">事件元素。The event element.</param>
    /// <returns>來源。The source.</returns>
    private static LineWebhookSource ReadSource(JsonElement element)
    {
        if (!element.TryGetProperty("source", out var source) || source.ValueKind != JsonValueKind.Object)
        {
            return new LineWebhookSource();
        }

        return new LineWebhookSource
        {
            Type = ReadString(source, "type") ?? string.Empty,
            UserId = ReadString(source, "userId"),
            GroupId = ReadString(source, "groupId"),
            RoomId = ReadString(source, "roomId"),
        };
    }

    /// <summary>
    /// 讀出訊息內容。
    /// Reads the message.
    /// </summary>
    /// <param name="element">事件元素。The event element.</param>
    /// <returns>沒有訊息時為 <see langword="null"/>。<see langword="null"/> when there is none.</returns>
    private static LineWebhookMessage? ReadMessage(JsonElement element)
    {
        if (!element.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        LineWebhookContentProvider? provider = null;
        if (message.TryGetProperty("contentProvider", out var providerElement)
            && providerElement.ValueKind == JsonValueKind.Object)
        {
            provider = new LineWebhookContentProvider
            {
                Type = ReadString(providerElement, "type") ?? string.Empty,
                OriginalContentUrl = ReadString(providerElement, "originalContentUrl"),
            };
        }

        return new LineWebhookMessage
        {
            Id = ReadString(message, "id") ?? string.Empty,
            Type = ReadString(message, "type") ?? string.Empty,
            Text = ReadString(message, "text"),
            QuoteToken = ReadString(message, "quoteToken"),
            Latitude = ReadDouble(message, "latitude"),
            Longitude = ReadDouble(message, "longitude"),
            Address = ReadString(message, "address"),
            Title = ReadString(message, "title"),
            PackageId = ReadString(message, "packageId"),
            StickerId = ReadString(message, "stickerId"),
            ContentProvider = provider,
            Duration = (int?)ReadInt64(message, "duration"),
            FileName = ReadString(message, "fileName"),
            FileSize = ReadInt64(message, "fileSize"),
        };
    }

    /// <summary>
    /// 讀出回傳內容。
    /// Reads the postback.
    /// </summary>
    /// <param name="element">事件元素。The event element.</param>
    /// <returns>沒有回傳內容時為 <see langword="null"/>。<see langword="null"/> when there is none.</returns>
    private static LineWebhookPostback? ReadPostback(JsonElement element)
    {
        if (!element.TryGetProperty("postback", out var postback) || postback.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        if (postback.TryGetProperty("params", out var parametersElement)
            && parametersElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in parametersElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    parameters[property.Name] = property.Value.GetString() ?? string.Empty;
                }
            }
        }

        return new LineWebhookPostback
        {
            Data = ReadString(postback, "data") ?? string.Empty,
            Params = parameters,
        };
    }

    /// <summary>
    /// 讀出加好友的附加資訊。
    /// Reads the follow details.
    /// </summary>
    /// <param name="element">事件元素。The event element.</param>
    /// <returns>不是加好友事件時為 <see langword="null"/>。<see langword="null"/> when this is not a follow event.</returns>
    private static LineWebhookFollow? ReadFollow(JsonElement element)
    {
        if (!string.Equals(ReadString(element, "type"), LineWebhookEventTypes.Follow, StringComparison.Ordinal))
        {
            return null;
        }

        // follow 事件不一定帶 follow 物件(舊版的事件就沒有),這時視為「第一次加好友」而不是把整個欄位留成 null:
        // 呼叫端關心的是「要不要發歡迎訊息」,而沒有這個物件本來就代表不是解除封鎖。
        // A follow event does not always carry a follow object — older ones do not — and it is then read as a
        // first-time follow rather than left null: what the caller wants to know is whether to send the welcome
        // message, and the object's absence already means this was not an unblock.
        var isUnblocked = element.TryGetProperty("follow", out var follow) && follow.ValueKind == JsonValueKind.Object
            && ReadBoolean(follow, "isUnblocked") == true;

        return new LineWebhookFollow { IsUnblocked = isUnblocked };
    }

    /// <summary>
    /// 讀一個字串欄位。
    /// Reads a string field.
    /// </summary>
    /// <param name="element">來源物件。The source object.</param>
    /// <param name="name">欄位名。The field name.</param>
    /// <returns>不存在或型別不符時為 <see langword="null"/>。<see langword="null"/> when absent or of another kind.</returns>
    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>
    /// 讀一個整數欄位。
    /// Reads an integer field.
    /// </summary>
    /// <param name="element">來源物件。The source object.</param>
    /// <param name="name">欄位名。The field name.</param>
    /// <returns>不存在或型別不符時為 <see langword="null"/>。<see langword="null"/> when absent or of another kind.</returns>
    private static long? ReadInt64(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt64(out var parsed)
                ? parsed
                : null;

    /// <summary>
    /// 讀一個浮點欄位。
    /// Reads a floating-point field.
    /// </summary>
    /// <param name="element">來源物件。The source object.</param>
    /// <param name="name">欄位名。The field name.</param>
    /// <returns>不存在或型別不符時為 <see langword="null"/>。<see langword="null"/> when absent or of another kind.</returns>
    private static double? ReadDouble(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            && value.TryGetDouble(out var parsed)
                ? parsed
                : null;

    /// <summary>
    /// 讀一個布林欄位。
    /// Reads a boolean field.
    /// </summary>
    /// <param name="element">來源物件。The source object.</param>
    /// <param name="name">欄位名。The field name.</param>
    /// <returns>不存在或型別不符時為 <see langword="null"/>。<see langword="null"/> when absent or of another kind.</returns>
    private static bool? ReadBoolean(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    /// <summary>
    /// 產生「內容無法解析」的失敗。
    /// Builds the unparsable-body failure.
    /// </summary>
    /// <param name="exception">解析時的例外。The exception from parsing.</param>
    /// <returns>失敗。The failure.</returns>
    private static Error InvalidPayload(JsonException exception)
    {
        var error = Error.Validation(
            LineErrorCodes.WebhookInvalidPayload,
            "webhook 內容不是合法的 JSON。The webhook body is not valid JSON.");
        return error with { Exception = exception };
    }
}
