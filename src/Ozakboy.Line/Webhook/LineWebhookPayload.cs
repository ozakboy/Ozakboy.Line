namespace Ozakboy.Line.Webhook;

/// <summary>
/// 一次 webhook 請求帶來的全部內容。
/// Everything one webhook request carries.
/// </summary>
public sealed class LineWebhookPayload
{
    /// <summary>
    /// 收件的官方帳號識別碼。
    /// The identifier of the official account these events are for.
    /// </summary>
    /// <remarks>
    /// 一個服務同時服務多個官方帳號時,靠這個值分辨這批事件屬於誰。
    /// When one service serves several official accounts, this is what tells whose events these are.
    /// </remarks>
    public string Destination { get; init; } = string.Empty;

    /// <summary>
    /// 這次帶來的事件。
    /// The events in this request.
    /// </summary>
    /// <remarks>
    /// 可能是空陣列:LINE 在後台驗證 webhook 位址時送的就是一個沒有事件的請求,這時必須照樣回 200。
    /// It may be empty: LINE's console sends a request with no events when verifying a webhook address, and that
    /// still has to be answered with a 200.
    /// </remarks>
    public IReadOnlyList<LineWebhookEvent> Events { get; init; } = [];
}
