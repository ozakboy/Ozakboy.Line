namespace Ozakboy.Line.Mcp.Outbox;

/// <summary>
/// 待發佇列裡的一筆項目。
/// One item in the outbox.
/// </summary>
/// <remarks>
/// 這個型別同時是<b>待審佇列</b>與<b>稽核紀錄</b>。即使在 <see cref="LineMcpSendMode.Direct"/> 模式下
/// 訊息直接送出,項目仍然會被寫進來 —— 沒有這份紀錄,「那則訊息是誰、什麼時候、為什麼發的」
/// 事後沒有任何地方查得到,而那是每一次事故調查的第一個問題。
/// This type is both the <b>review queue</b> and the <b>audit trail</b>. An item is written even in
/// <see cref="LineMcpSendMode.Direct"/> mode, where the message goes straight out: without the record there is
/// nowhere afterwards to find out who sent that message, when, or why — the first question of every incident
/// review.
/// </remarks>
public sealed class LineMcpOutboxItem
{
    /// <summary>
    /// 項目識別碼。
    /// The item's identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 要做的是哪一件事。
    /// What it will do.
    /// </summary>
    public LineMcpOutboxKind Kind { get; set; }

    /// <summary>
    /// 內容的 JSON,形狀隨 <see cref="Kind"/> 而定。
    /// The payload as JSON, in the shape that goes with <see cref="Kind"/>.
    /// </summary>
    public string PayloadJson { get; set; } = "{}";

    /// <summary>
    /// 給人看的一行摘要。
    /// A one-line summary for a person to read.
    /// </summary>
    /// <remarks>
    /// 審核的人看的是這一行。要他們逐筆讀 JSON 的話,實務上的結果是他們不讀就按核准,
    /// 而那時待審制度只剩下形式。
    /// This line is what a reviewer reads. Asking them to read the JSON item by item ends, in practice, with
    /// approvals given without reading — and at that point the review step is a formality.
    /// </remarks>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// 目前狀態。
    /// The current status.
    /// </summary>
    public LineMcpOutboxStatus Status { get; set; }

    /// <summary>
    /// 建立時間;每日上限就是以這個欄位計數。
    /// When it was created; the daily cap counts on this field.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// 核准、退回或取消的時間。
    /// When it was approved, rejected, or cancelled.
    /// </summary>
    public DateTimeOffset? DecidedAt { get; set; }

    /// <summary>
    /// 送出時間。
    /// When it was sent.
    /// </summary>
    public DateTimeOffset? SentAt { get; set; }

    /// <summary>
    /// 失敗原因或退回理由。
    /// Why it failed, or why it was rejected.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// 來源標記,預設 <c>mcp</c>。
    /// Where it came from; <c>mcp</c> by default.
    /// </summary>
    /// <remarks>
    /// 宿主自己的後台若也寫進同一個佇列,請用別的標記。分得開,才答得出
    /// 「這一批待審裡有多少是 AI 提的」——而那是決定要不要調 <see cref="LineMcpOptions.SendMode"/> 的依據。
    /// A host writing into the same queue from its own admin pages should use a different marker. Keeping them
    /// apart is what answers "how much of this queue came from the AI", which is what a decision about
    /// <see cref="LineMcpOptions.SendMode"/> rests on.
    /// </remarks>
    public string Source { get; set; } = "mcp";

    /// <summary>
    /// 複製一份。
    /// Makes a copy.
    /// </summary>
    /// <returns>內容相同的新物件。A new object with the same contents.</returns>
    internal LineMcpOutboxItem Clone() => new()
    {
        Id = Id,
        Kind = Kind,
        PayloadJson = PayloadJson,
        Summary = Summary,
        Status = Status,
        CreatedAt = CreatedAt,
        DecidedAt = DecidedAt,
        SentAt = SentAt,
        Error = Error,
        Source = Source,
    };
}
