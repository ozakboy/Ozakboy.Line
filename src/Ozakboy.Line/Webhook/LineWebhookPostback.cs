namespace Ozakboy.Line.Webhook;

/// <summary>
/// 回傳事件的內容。
/// The contents of a postback event.
/// </summary>
public sealed class LineWebhookPostback
{
    /// <summary>
    /// 送出動作時帶的資料,原樣回來。
    /// The data carried by the action, returned verbatim.
    /// </summary>
    /// <remarks>
    /// 這段資料<b>走過使用者的裝置</b>再回來。內容本身沒有簽章,LINE 也不驗證它 ——
    /// 任何影響權限的判斷都不能只靠它,必須拿事件來源的使用者識別碼回頭查自己的資料。
    /// This data makes a round trip <b>through the user's device</b>. It is not signed, and LINE does not verify
    /// it: no authorization decision can rest on it alone, and must instead look the event source's user
    /// identifier up in one's own records.
    /// </remarks>
    public string Data { get; init; } = string.Empty;

    /// <summary>
    /// 日期時間選擇器等動作附帶的參數;沒有時為空字典。
    /// The parameters carried by actions such as a datetime picker; an empty dictionary when there are none.
    /// </summary>
    public IReadOnlyDictionary<string, string> Params { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
