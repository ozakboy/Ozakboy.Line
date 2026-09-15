using System.Text.Json;
using Ozakboy.Http.Retry;

namespace Ozakboy.Line.Tests.TestSupport;

/// <summary>
/// 一個送出過的請求。
/// One request that was sent.
/// </summary>
/// <param name="Method">HTTP 方法。The HTTP method.</param>
/// <param name="Uri">目標位址。The target address.</param>
/// <param name="Body">請求內容,沒有時為 <see langword="null"/>。The body, or <see langword="null"/> when there was none.</param>
/// <param name="Headers">請求標頭。The request headers.</param>
/// <param name="ContentType">內容型別。The content type.</param>
/// <param name="Idempotency">管線判定的冪等性。The idempotency the pipeline sees.</param>
internal sealed record RecordedRequest(
    HttpMethod Method,
    Uri Uri,
    string? Body,
    IReadOnlyDictionary<string, string> Headers,
    string? ContentType,
    RequestIdempotency Idempotency)
{
    /// <summary>
    /// 把請求內容解析成 JSON。
    /// Parses the body as JSON.
    /// </summary>
    /// <returns>解析後的根元素。The parsed root element.</returns>
    internal JsonElement Json()
    {
        using var document = JsonDocument.Parse(Body!);
        return document.RootElement.Clone();
    }

    /// <summary>
    /// 把表單編碼的請求內容拆成欄位。
    /// Splits a form-encoded body into its fields.
    /// </summary>
    /// <returns>欄位名對應值。The fields by name.</returns>
    internal IReadOnlyDictionary<string, string> Form()
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in (Body ?? string.Empty).Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            if (separator > 0)
            {
                fields[System.Uri.UnescapeDataString(pair[..separator])] =
                    System.Uri.UnescapeDataString(pair[(separator + 1)..].Replace('+', ' '));
            }
        }

        return fields;
    }
}
