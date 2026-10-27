namespace Ozakboy.Line.AspNetCore.Webhook;

/// <summary>
/// 處理常式擲出例外時,是否推播通知管理員。
/// Whether to push a notification to administrators when a handler throws.
/// </summary>
/// <remarks>
/// <para>
/// 通知內容:
/// The notification reads:
/// <c>[Ozakboy.Line] webhook 事件處理失敗\n事件:{type} {webhookEventId}\n來源:{source type}/{userId}\n例外:{ExceptionType}: {Message}</c>,
/// 整則截到 5000 字。
/// and is cut to 5000 characters in all.
/// </para>
/// <para>
/// 通知用的是<b>推播</b>(會計入推播額度),並且<b>絕不影響回應</b>:各管理員並行推送、各自獨立失敗,
/// 失敗只記錄;整體另有時限,狀態碼仍是 200。
/// It is a <b>push</b> (it counts against the push quota) and it <b>never affects the response</b>: administrators
/// are notified in parallel and fail independently, a failure is only logged, and the whole thing is time-boxed;
/// the status code stays 200.
/// </para>
/// <para>
/// 例外訊息可能含有內部細節,請只把管理員自己的識別碼放進 <see cref="AdminUserIds"/>。
/// An exception message can carry internal detail, so put only the administrators' own IDs in
/// <see cref="AdminUserIds"/>.
/// </para>
/// </remarks>
public sealed class LineWebhookErrorNotificationOptions
{
    /// <summary>
    /// 是否開啟,預設為 <see langword="false"/>。
    /// Whether it is on; <see langword="false"/> by default.
    /// </summary>
    /// <remarks>
    /// 開啟但 <see cref="AdminUserIds"/> 為空時,掛端點當下擲出 <see cref="InvalidOperationException"/>。
    /// Turning it on with an empty <see cref="AdminUserIds"/> makes mapping throw an
    /// <see cref="InvalidOperationException"/>.
    /// </remarks>
    public bool Enabled { get; set; }

    /// <summary>
    /// 要通知的管理員 LINE 使用者識別碼。
    /// The LINE user IDs to notify.
    /// </summary>
    public IList<string> AdminUserIds { get; } = [];

    /// <summary>
    /// 通知是否附上堆疊追蹤,預設為 <see langword="false"/>。
    /// Whether the notification carries the stack trace; <see langword="false"/> by default.
    /// </summary>
    public bool IncludeStackTrace { get; set; }
}
