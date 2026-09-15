namespace Ozakboy.Line.Mcp;

/// <summary>
/// MCP 工具組的設定:AI 能做到哪裡為止。
/// The MCP tool set's settings: how far the AI is allowed to go.
/// </summary>
/// <remarks>
/// 這裡每一個開關的預設值都偏保守。讓一個沒讀過文件的人直接掛上去也不會出事,
/// 比讓他掛上去很方便、但某天 AI 讀到一段奇怪的文字就對全體好友廣播要好。
/// Every default here leans conservative. Someone attaching this without reading the documentation should not be
/// able to cause harm, which matters more than convenience the day an AI reads something odd and broadcasts it
/// to every friend of the account.
/// </remarks>
public sealed class LineMcpOptions
{
    /// <summary>
    /// 送出類工具的處置方式,預設為 <see cref="LineMcpSendMode.Review"/>(只進待審,不送出)。
    /// What sending tools do; <see cref="LineMcpSendMode.Review"/> by default, which queues and sends nothing.
    /// </summary>
    public LineMcpSendMode SendMode { get; set; } = LineMcpSendMode.Review;

    /// <summary>
    /// 每日(依 <see cref="TimeZoneId"/> 的當地日期)可建立的待發項目上限,預設 10 筆。
    /// The most outbox items that can be created in one local day, by <see cref="TimeZoneId"/>; ten by default.
    /// </summary>
    /// <remarks>
    /// 這是防暴走的閘,不是配額管理。AI 在迴圈裡重試同一個工具是很常見的失敗模式,
    /// 而沒有上限時那個迴圈會把待審佇列灌到沒有人願意去看 —— 待審制度到那一步就等於失效了。
    /// This is a runaway guard rather than quota management. An AI retrying the same tool in a loop is a common
    /// failure, and without a cap that loop fills the review queue until nobody is willing to read it — at which
    /// point the review step has stopped working.
    /// </remarks>
    public int MaxSendRequestsPerDay { get; set; } = 10;

    /// <summary>
    /// 是否允許 AI 新增 / 修改 / 刪除自動回覆規則,預設為 <see langword="true"/>。
    /// Whether the AI may add, change, or delete auto reply rules; <see langword="true"/> by default.
    /// </summary>
    /// <remarks>
    /// 允許是因為規則<b>不會自己送出訊息</b>:改壞一條規則的後果是回錯話,而不是主動打擾全體好友,
    /// 而且改回來也只是再 upsert 一次。真正要管的是送出,那由 <see cref="SendMode"/> 負責。
    /// It is allowed because a rule <b>sends nothing by itself</b>: a broken rule answers wrongly rather than
    /// reaching out to everyone uninvited, and undoing it is one more upsert. What needs guarding is sending, and
    /// that is <see cref="SendMode"/>'s job.
    /// </remarks>
    public bool AllowRuleEdits { get; set; } = true;

    /// <summary>
    /// 是否允許 AI 新增 / 修改 / 刪除訊息範本,預設為 <see langword="true"/>。
    /// Whether the AI may add, change, or delete message templates; <see langword="true"/> by default.
    /// </summary>
    public bool AllowTemplateEdits { get; set; } = true;

    /// <summary>
    /// 是否允許 AI 替換圖文選單,預設為 <see langword="true"/>。
    /// Whether the AI may replace rich menus; <see langword="true"/> by default.
    /// </summary>
    /// <remarks>
    /// 替換選單仍然要過 <see cref="SendMode"/>:Review 模式下它一樣只進待審佇列。
    /// 這個開關是給「選單由設計流程管、不該由 AI 碰」的宿主用的。
    /// Replacing a menu still goes through <see cref="SendMode"/>, and in Review mode it only queues. This switch
    /// is for hosts whose menus belong to a design process the AI has no business touching.
    /// </remarks>
    public bool AllowRichMenuChanges { get; set; } = true;

    /// <summary>
    /// 金鑰閘用的金鑰;為 <see langword="null"/> 或空白時<b>整個 MCP 端點關閉</b>(所有路徑一律 404)。
    /// The key for the key gate. When it is <see langword="null"/> or blank the <b>whole MCP endpoint is off</b>
    /// and every path answers 404.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 「沒設定 = 關閉」是刻意的預設。反過來(沒設定就不驗)的話,任何一次部署忘了帶環境變數,
    /// 就是把一組可以操作官方帳號的工具直接開在網路上,而且不會有任何跡象。
    /// "Unset means off" is the deliberate default. The other way round — unset means no check — turns any
    /// deployment that forgets an environment variable into a set of tools for operating an official account,
    /// open to the internet, with nothing to indicate it.
    /// </para>
    /// <para>
    /// 金鑰走<b>路徑</b>而不是標頭:多數 MCP 連接器只讓使用者填一個網址,沒有地方填自訂標頭。
    /// 代價是金鑰會進反向代理與瀏覽器的存取記錄,所以它要夠長、而且輪替方式就是改設定重啟。
    /// The key travels in the <b>path</b> rather than a header, because most MCP connectors give the user one
    /// field for a URL and nowhere for a custom header. The cost is that it lands in reverse proxy and browser
    /// access logs, so it needs to be long, and rotating it means changing the setting and restarting.
    /// </para>
    /// </remarks>
    public string? ApiKey { get; set; }

    /// <summary>
    /// 計算「今日」用的時區,預設 <c>Asia/Taipei</c>。
    /// The time zone that decides what "today" means; <c>Asia/Taipei</c> by default.
    /// </summary>
    /// <remarks>
    /// 每日上限依這個時區的當地日期計算。用 UTC 的話,台灣時間早上八點才換日,
    /// 而「昨天的額度為什麼還沒回來」是一個沒有人想在早上查的問題。
    /// The daily cap counts against this zone's local date. On UTC the day would turn over at eight in the
    /// morning in Taiwan, and "why has yesterday's allowance not come back" is not a question anyone wants to
    /// investigate before lunch.
    /// </remarks>
    public string TimeZoneId { get; set; } = "Asia/Taipei";

    /// <summary>
    /// Review 模式下回給 AI 的提示文字。
    /// The notice handed back to the AI in Review mode.
    /// </summary>
    /// <remarks>
    /// 這段話會出現在工具的回傳 JSON 裡。它的作用是讓 AI 對使用者<b>如實說明</b>「這只是草稿」——
    /// 少了它,AI 很容易把工具回的 <c>ok:true</c> 讀成「已經發出去了」,然後這樣告訴使用者。
    /// This text appears in the tool's JSON result. It is there so the AI tells the user <b>truthfully</b> that
    /// this is only a draft: without it, an AI readily reads the tool's <c>ok:true</c> as "it has been sent" and
    /// says so.
    /// </remarks>
    public string? ReviewNotice { get; set; } = "此為待審項目,需宿主人工核准後才會發送。";
}
