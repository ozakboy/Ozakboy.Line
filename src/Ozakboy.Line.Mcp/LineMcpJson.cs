using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace Ozakboy.Line.Mcp;

/// <summary>
/// MCP 工具共用的輸出格式:一律 camelCase JSON 字串,中文不轉 <c>\uXXXX</c>。
/// The output format shared by the MCP tools: camelCase JSON strings, with Chinese left as Chinese rather than
/// escaped to <c>\uXXXX</c>.
/// </summary>
/// <remarks>
/// 不轉 <c>\uXXXX</c> 有兩個實際好處:AI 讀得懂(不必自己解碼),而且同樣的內容少掉大約五倍的 token。
/// 這兩件事在工具回傳大量文字時都很有感。
/// Leaving the characters alone buys two practical things: the AI reads them without decoding, and the same
/// content costs about a fifth of the tokens. Both show up once a tool returns much text.
/// </remarks>
internal static class LineMcpJson
{
    /// <summary>
    /// 序列化設定。
    /// The serialiser settings.
    /// </summary>
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    /// <summary>
    /// 成功回應。
    /// A successful response.
    /// </summary>
    /// <param name="value">要回傳的物件,通常是匿名型別。The object to return, usually an anonymous type.</param>
    /// <returns>JSON 字串。The JSON string.</returns>
    internal static string Ok(object value) => JsonSerializer.Serialize(value, Options);

    /// <summary>
    /// 失敗回應,一律是 <c>{ "ok": false, "error": "…" }</c>。
    /// A failure response, always <c>{ "ok": false, "error": "…" }</c>.
    /// </summary>
    /// <param name="message">失敗原因,繁體中文。Why it failed, in Traditional Chinese.</param>
    /// <returns>JSON 字串。The JSON string.</returns>
    /// <remarks>
    /// 工具的失敗<b>回成 JSON 而不是擲例外</b>。MCP 用戶端看到例外只會得到一句「工具呼叫失敗」,
    /// 而 AI 需要的是「為什麼失敗、下一步該怎麼做」—— 那句話得在回傳值裡。
    /// A tool's failure <b>comes back as JSON rather than an exception</b>. All an MCP client gets from an
    /// exception is that the call failed, while what the AI needs is why and what to do next, and that sentence
    /// has to be in the return value.
    /// </remarks>
    internal static string Error(string message) => JsonSerializer.Serialize(new { ok = false, error = message }, Options);

    /// <summary>
    /// 把一份 <see cref="Ozakboy.Core.Abstractions.Error"/> 轉成失敗回應。
    /// Turns an <see cref="Ozakboy.Core.Abstractions.Error"/> into a failure response.
    /// </summary>
    /// <param name="error">錯誤。The error.</param>
    /// <returns>JSON 字串。The JSON string.</returns>
    /// <remarks>
    /// 帶上錯誤代碼:AI 面對 <c>line.validation.*</c>(自己改一下再試)與
    /// <c>line.api.error</c>(對方拒絕了,重試沒用)該做的事完全不同,而中文訊息不足以讓它穩定分辨。
    /// The code goes along: an AI does entirely different things with a <c>line.validation.*</c> — fix it and try
    /// again — and a <c>line.api.error</c>, where the far end refused and retrying changes nothing, and a
    /// sentence of Chinese is not enough for it to tell them apart reliably.
    /// </remarks>
    internal static string Error(Ozakboy.Core.Abstractions.Error error) =>
        JsonSerializer.Serialize(new { ok = false, error = error.Message, code = error.Code }, Options);
}
