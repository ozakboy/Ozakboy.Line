using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Messaging.Messages;

namespace Ozakboy.Line.Templates;

/// <summary>
/// 把範本裡的 <c>{{變數}}</c> 換成實際的值,並確認結果仍是一份合法的訊息陣列。
/// Substitutes a template's <c>{{variable}}</c> placeholders and checks that the result is still a valid message
/// array.
/// </summary>
/// <remarks>
/// <para>
/// 值在代入之前一律先做 <b>JSON 字串跳脫</b>。少了這一步,一個使用者暱稱裡的引號就足以把整份 JSON 弄壞,
/// 而症狀是 LINE 回一個「請求內容有錯」的 400 —— 看不出是哪個欄位、更看不出是誰的名字造成的。
/// Values are <b>escaped for JSON</b> before they go in. Without that, one quotation mark in a user's display
/// name is enough to break the whole document, and the symptom is a 400 from LINE saying the body is wrong —
/// with nothing to say which field, let alone whose name did it.
/// </para>
/// <para>
/// 跳脫用的編碼器放行全部 Unicode 字元:中文不轉成 <c>\uXXXX</c>,範本渲染後仍然看得懂,
/// 除錯時不必再解一次碼。引號、反斜線與控制字元照樣跳脫,安全性不受影響。
/// The encoder used lets every Unicode character through, so Chinese text is not turned into <c>\uXXXX</c> and a
/// rendered template stays readable without a second decoding pass while debugging. Quotes, backslashes and
/// control characters are still escaped, so nothing is given up on the safety side.
/// </para>
/// </remarks>
public static class LineTemplateRenderer
{
    /// <summary>
    /// 跳脫用的編碼器。
    /// The encoder used for escaping.
    /// </summary>
    private static readonly JavaScriptEncoder ValueEncoder = JavaScriptEncoder.Create(UnicodeRanges.All);

    /// <summary>
    /// 代入變數並驗證結果。
    /// Substitutes the variables and validates the result.
    /// </summary>
    /// <param name="messagesJson">範本內容。The template body.</param>
    /// <param name="values">變數名對應值。The values by variable name.</param>
    /// <returns>
    /// 代入後的訊息陣列 JSON;缺變數時為 <see cref="LineErrorCodes.MissingTemplateVariables"/>、
    /// 代入後不是合法 JSON 時為 <see cref="LineErrorCodes.InvalidJson"/>、
    /// 則數不在 1 到 5 之間時為 <see cref="LineErrorCodes.TooManyMessages"/> 的失敗。
    /// The substituted message array. A <see cref="LineErrorCodes.MissingTemplateVariables"/> failure when a
    /// variable has no value, a <see cref="LineErrorCodes.InvalidJson"/> one when the result does not parse, and
    /// a <see cref="LineErrorCodes.TooManyMessages"/> one when the count is outside one to five.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="messagesJson"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="messagesJson"/> is <see langword="null"/> or blank.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="values"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="values"/> is <see langword="null"/>.
    /// </exception>
    public static Result<string> Render(string messagesJson, IReadOnlyDictionary<string, string> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messagesJson);
        ArgumentNullException.ThrowIfNull(values);

        var missing = new List<string>();
        foreach (var name in LineTemplateVariables.Extract(messagesJson))
        {
            if (!values.ContainsKey(name))
            {
                missing.Add(name);
            }
        }

        if (missing.Count > 0)
        {
            // 訊息列出缺了哪幾個。只說「缺變數」的話,範本一長就得自己一個一個比對。
            // The message names them. A bare "a variable is missing" leaves whoever owns a long template to
            // compare them one by one.
            return Error.Validation(
                LineErrorCodes.MissingTemplateVariables,
                $"範本缺少變數的值:{string.Join("、", missing)}。The template is missing values for: {string.Join(", ", missing)}.");
        }

        var rendered = LineTemplateVariables.PlaceholderRegex().Replace(
            messagesJson,
            match => JsonEncodedText.Encode(values[LineTemplateVariables.ReadName(match.ValueSpan)], ValueEncoder).ToString());

        return Validate(rendered);
    }

    /// <summary>
    /// 代入變數並轉成訊息物件。
    /// Substitutes the variables and turns the result into message objects.
    /// </summary>
    /// <param name="messagesJson">範本內容。The template body.</param>
    /// <param name="values">變數名對應值。The values by variable name.</param>
    /// <returns>
    /// 每則訊息一個 <see cref="RawMessage"/>;失敗時的代碼與 <see cref="Render"/> 相同。
    /// One <see cref="RawMessage"/> per message; failures carry the same codes as <see cref="Render"/>.
    /// </returns>
    /// <remarks>
    /// 回的是 <see cref="RawMessage"/> 而不是各自的具體型別:範本裡可能有本套件還沒建模的訊息型別,
    /// 硬要還原成具體型別必然會在讀取時掉欄位,而範本的重點正是「原封不動地送出去」。
    /// These come back as <see cref="RawMessage"/> rather than concrete types: a template can hold a message type
    /// this package has not modelled, and reconstructing concrete types would drop fields on the way in — while
    /// the whole point of a template is that it goes out exactly as written.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="messagesJson"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="messagesJson"/> is <see langword="null"/> or blank.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="values"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="values"/> is <see langword="null"/>.
    /// </exception>
    public static Result<IReadOnlyList<LineMessage>> RenderMessages(
        string messagesJson,
        IReadOnlyDictionary<string, string> values)
    {
        var rendered = Render(messagesJson, values);
        if (rendered.IsFailure)
        {
            return rendered.ToFailure<IReadOnlyList<LineMessage>>();
        }

        using var document = JsonDocument.Parse(rendered.GetValueOrThrow());

        var messages = new List<LineMessage>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object
                || !element.TryGetProperty("type", out var type)
                || type.ValueKind != JsonValueKind.String)
            {
                return Error.Validation(
                    LineErrorCodes.InvalidJson,
                    "範本裡的每一則訊息都必須是含有字串 type 欄位的 JSON 物件。Every message in a template must be a JSON object carrying a string type field.");
            }

            messages.Add(new RawMessage(element));
        }

        return Result.Success<IReadOnlyList<LineMessage>>(messages);
    }

    /// <summary>
    /// 檢查一份訊息陣列 JSON 是不是合法的 1 到 5 則訊息。
    /// Checks that a message array's JSON is a valid one-to-five-message array.
    /// </summary>
    /// <param name="messagesJson">訊息陣列 JSON。The message array as JSON.</param>
    /// <returns>合法時為原樣回傳。The same JSON when it is valid.</returns>
    /// <remarks>
    /// 範本存進去之前、渲染之後都檢查一次:存的時候擋下來,錯誤還在編輯範本的人面前;
    /// 只在送出時才檢查的話,壞掉的範本要等到真的有人觸發它才會被發現。
    /// This runs both before a template is stored and after it renders. Caught at store time, the error is still
    /// in front of whoever is editing; checked only at send time, a broken template waits until someone actually
    /// triggers it.
    /// </remarks>
    public static Result<string> Validate(string messagesJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messagesJson);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(messagesJson);
        }
        catch (JsonException exception)
        {
            var error = Error.Validation(
                LineErrorCodes.InvalidJson,
                "訊息內容不是合法的 JSON。The message body is not valid JSON.");
            return error with { Exception = exception };
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Error.Validation(
                    LineErrorCodes.InvalidJson,
                    "訊息內容必須是一個 JSON 陣列。The message body must be a JSON array.");
            }

            var count = document.RootElement.GetArrayLength();
            return count is >= LineMessagingLimits.MinMessagesPerRequest and <= LineMessagingLimits.MaxMessagesPerRequest
                ? Result.Success(messagesJson)
                : Error.Validation(
                    LineErrorCodes.TooManyMessages,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"一次請求需帶 {LineMessagingLimits.MinMessagesPerRequest} 到 {LineMessagingLimits.MaxMessagesPerRequest} 則訊息,這份內容是 {count} 則。One request takes between {LineMessagingLimits.MinMessagesPerRequest} and {LineMessagingLimits.MaxMessagesPerRequest} messages; this body has {count}."));
        }
    }
}
