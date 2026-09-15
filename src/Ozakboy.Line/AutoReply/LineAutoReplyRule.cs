namespace Ozakboy.Line.AutoReply;

/// <summary>
/// 一條關鍵字自動回覆規則。
/// One keyword auto reply rule.
/// </summary>
/// <remarks>
/// 規則是<b>資料</b>而不是程式碼:整條規則都能存進 store、由後台或 MCP 工具編輯,改一句歡迎詞不必重新部署。
/// A rule is <b>data</b> rather than code: the whole thing goes into a store and can be edited from an admin page
/// or an MCP tool, so changing a welcome line never means a deployment.
/// </remarks>
public sealed class LineAutoReplyRule
{
    /// <summary>
    /// 規則識別碼;交給 <see cref="ILineAutoReplyStore.UpsertAsync"/> 時留空,由 store 產生。
    /// The rule identifier; leave it blank for <see cref="ILineAutoReplyStore.UpsertAsync"/> and the store
    /// produces one.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 規則名稱,給人看的。
    /// The rule's name, for people to read.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 是否啟用;停用的規則不參與比對。
    /// Whether the rule is on; a rule that is off takes no part in matching.
    /// </summary>
    /// <remarks>
    /// 有「停用」是為了讓人把規則<b>關掉而不是刪掉</b>。刪掉的規則要復原就得重打一次,
    /// 而季節性的活動回覆一年會被關開好幾次。
    /// Being able to turn a rule off rather than delete it matters: a deleted rule has to be retyped to come
    /// back, and a seasonal campaign reply gets switched off and on several times a year.
    /// </remarks>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 比對方式。
    /// How the rule matches.
    /// </summary>
    public LineAutoReplyMatchMode Match { get; set; }

    /// <summary>
    /// 比對用的樣式;<see cref="LineAutoReplyMatchMode.Follow"/> 與
    /// <see cref="LineAutoReplyMatchMode.Fallback"/> 不使用這個欄位。
    /// The pattern to match. <see cref="LineAutoReplyMatchMode.Follow"/> and
    /// <see cref="LineAutoReplyMatchMode.Fallback"/> do not use this field.
    /// </summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary>
    /// 比對時是否忽略大小寫,預設為 <see langword="true"/>。
    /// Whether matching ignores case; <see langword="true"/> by default.
    /// </summary>
    public bool IgnoreCase { get; set; } = true;

    /// <summary>
    /// 優先序,<b>數字小的先</b>,預設 100。
    /// The priority, where a <b>smaller number goes first</b>; 100 by default.
    /// </summary>
    /// <remarks>
    /// 預設留在中間(而不是 0 或 1)是為了讓之後插隊的規則兩邊都有空間 ——
    /// 預設若是 0,想排在更前面就只能用負數。
    /// The default sits in the middle rather than at 0 or 1 so later rules have room on both sides: with a
    /// default of 0, getting ahead of one means going negative.
    /// </remarks>
    public int Priority { get; set; } = 100;

    /// <summary>
    /// 要回覆的 LINE 訊息陣列 JSON,1 到 5 則。
    /// The JSON of the LINE message array to reply with, between one and five messages.
    /// </summary>
    /// <remarks>
    /// 可用的佔位符是 <c>{{text}}</c>(使用者傳來的原訊息)與 <c>{{displayName}}</c>(使用者的顯示名稱)。
    /// 加好友事件沒有訊息文字,這時 <c>{{text}}</c> 以空字串代入 —— 不算缺變數。
    /// The placeholders are <c>{{text}}</c>, the message the user sent, and <c>{{displayName}}</c>, their display
    /// name. An add-friend event carries no message text, and <c>{{text}}</c> is substituted with an empty string
    /// there rather than counting as a missing variable.
    /// </remarks>
    public string MessagesJson { get; set; } = "[]";

    /// <summary>
    /// 建立時間。
    /// When it was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// 最後修改時間。
    /// When it was last changed.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// 複製一份。
    /// Makes a copy.
    /// </summary>
    /// <returns>內容相同的新物件。A new object with the same contents.</returns>
    internal LineAutoReplyRule Clone() => new()
    {
        Id = Id,
        Name = Name,
        Enabled = Enabled,
        Match = Match,
        Pattern = Pattern,
        IgnoreCase = IgnoreCase,
        Priority = Priority,
        MessagesJson = MessagesJson,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt,
    };
}
