namespace Ozakboy.Line.Templates;

/// <summary>
/// 一份訊息範本:一組可重複使用、可帶變數的 LINE 訊息。
/// One message template: a reusable set of LINE messages that can carry variables.
/// </summary>
/// <remarks>
/// 範本存的是 <b>LINE 訊息陣列的 JSON</b>,而不是一段純文字。這樣一來,圖片、Flex、快速回覆這些
/// 排版上的東西全都能存進範本,而不是只有「一句話」;要換掉文案也不必動程式碼。
/// A template stores <b>the JSON of a LINE message array</b> rather than a piece of plain text, so images, Flex
/// layouts and quick replies all fit into one — not just a single sentence — and changing the wording never means
/// changing code.
/// </remarks>
public sealed class LineMessageTemplate
{
    /// <summary>
    /// 範本識別碼;交給 <see cref="ILineTemplateStore.UpsertAsync"/> 時留空,由 store 產生。
    /// The template identifier; leave it blank when handing the template to
    /// <see cref="ILineTemplateStore.UpsertAsync"/> and the store produces one.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 範本名稱,給人看的。
    /// The template's name, for people to read.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 補充說明;沒有時為 <see langword="null"/>。
    /// A description, or <see langword="null"/> when there is none.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// LINE 訊息陣列的 JSON,1 到 5 則,可含 <c>{{變數}}</c> 佔位符。
    /// The JSON of a LINE message array, between one and five messages, which may carry <c>{{variable}}</c>
    /// placeholders.
    /// </summary>
    /// <remarks>
    /// 佔位符的名字由 <see cref="LineTemplateVariables.Extract"/> 抽出,值由
    /// <see cref="LineTemplateRenderer.Render"/> 代入,代入前會先做 JSON 字串跳脫 ——
    /// 值裡有引號或換行時,JSON 不會因此破掉。
    /// The placeholder names come from <see cref="LineTemplateVariables.Extract"/> and the values go in through
    /// <see cref="LineTemplateRenderer.Render"/>, which escapes them for JSON first, so a value carrying a quote
    /// or a newline does not break the document.
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
    /// <remarks>
    /// store 在存入與取出時都複製一份,呼叫端拿到的物件與 store 裡的那一份因此是分開的。
    /// 少了這一步,呼叫端改了手上的物件就等於改了「已儲存」的資料,而檔案裡的內容卻沒有跟著變。
    /// The stores copy on the way in and on the way out, so what a caller holds is separate from what the store
    /// holds. Without it, changing the object in hand silently changes the "stored" data while the file on disk
    /// stays as it was.
    /// </remarks>
    internal LineMessageTemplate Clone() => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        MessagesJson = MessagesJson,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt,
    };
}
