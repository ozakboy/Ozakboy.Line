using System.Text.Json;

namespace Ozakboy.Line.Tests.TestSupport;

/// <summary>
/// 讀取 <c>Samples/</c> 底下依官方文件整理的回應樣本。
/// Reads the response samples under <c>Samples/</c>, transcribed from LINE's official reference.
/// </summary>
/// <remarks>
/// 每個樣本檔的形狀是 <c>{ "source": …, "retrieved": …, "body": {…} }</c>:前兩個欄位是給人看的出處與日期,
/// <c>body</c> 才是 LINE 會回的內容。測試只拿 <c>body</c>,出處留在檔案裡讓人核對。
/// Every sample has the shape <c>{ "source": …, "retrieved": …, "body": {…} }</c>: the first two fields name
/// the origin and date for a person, and <c>body</c> is what LINE answers. The tests take only <c>body</c>; the
/// origin stays in the file for anyone checking.
/// </remarks>
internal static class Samples
{
    /// <summary>
    /// 取出樣本的 <c>body</c> 並寫回 JSON 字串。
    /// Extracts a sample's <c>body</c> and writes it back out as a JSON string.
    /// </summary>
    /// <param name="name">檔名(不含副檔名)。The file name without extension.</param>
    /// <returns>LINE 回應內容的 JSON。The JSON of LINE's response body.</returns>
    internal static string Body(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Samples", name + ".json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("body").GetRawText();
    }
}
