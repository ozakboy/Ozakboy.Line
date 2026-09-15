namespace Ozakboy.Line.Mcp;

/// <summary>
/// 外部 AI 呼叫送出類工具時的處置方式。
/// What happens when an outside AI calls a sending tool.
/// </summary>
public enum LineMcpSendMode
{
    /// <summary>
    /// 只寫進待審佇列,不送出任何訊息(預設)。
    /// Queues the payload for review and sends nothing. This is the default.
    /// </summary>
    /// <remarks>
    /// 這是本套件的預設,而且應該一直是。AI 讀到的內容有一部分來自外部(使用者訊息、網頁、文件),
    /// 而「把讀到的東西當成指示照做」是這類系統最常見的失控方式 —— 待審這一步就是那道閘。
    /// This is the default and should stay that way. Part of what an AI reads comes from outside — user messages,
    /// web pages, documents — and treating what it read as an instruction is the most common way these systems
    /// get away from their owner. The review step is the gate.
    /// </remarks>
    Review = 0,

    /// <summary>
    /// 直接送出,但仍在待發佇列留下紀錄。
    /// Sends immediately, while still recording the item in the outbox.
    /// </summary>
    /// <remarks>
    /// 只在「AI 完全由自己人驅動、而且訊息送錯的代價可以承受」時才用。留紀錄不是可選的 ——
    /// 沒有紀錄的話,事後沒有任何地方查得出「那則訊息是誰、什麼時候、為什麼發的」。
    /// Only for an AI driven entirely by your own people, where a wrong message is a cost you can absorb. The
    /// record is not optional: without it there is nowhere afterwards to find out who sent that message, when,
    /// or why.
    /// </remarks>
    Direct = 1,
}
