using System.ComponentModel;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Ozakboy.Line.AutoReply;
using Ozakboy.Line.Templates;

namespace Ozakboy.Line.Mcp.Tools;

/// <summary>
/// 關鍵字自動回覆規則的管理工具。
/// The tools for managing keyword auto reply rules.
/// </summary>
/// <remarks>
/// 規則<b>不會自己送訊息</b>,所以這一組工具不走待審流程,也不計每日上限:
/// 改壞一條規則的後果是回錯話,而不是主動打擾全體好友,改回來也只是再 upsert 一次。
/// Rules <b>send nothing by themselves</b>, so this group skips the review queue and the daily cap: a broken rule
/// answers wrongly rather than reaching out to everyone uninvited, and undoing it is one more upsert.
/// </remarks>
[McpServerToolType]
public sealed class LineAutoReplyTools
{
    private readonly IOptions<LineMcpOptions> _options;
    private readonly ILineAutoReplyStore? _store;

    /// <summary>
    /// 建立工具組。
    /// Creates the tool set.
    /// </summary>
    /// <param name="options">MCP 設定。The MCP settings.</param>
    /// <param name="store">
    /// 規則儲存體;宿主沒有呼叫 <c>AddLineAutoReply</c> 時為 <see langword="null"/>。
    /// The rule store, or <see langword="null"/> when the host has not called <c>AddLineAutoReply</c>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="options"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// 儲存體是<b>選填</b>的。沒註冊自動回覆的宿主照樣可以掛上這組 MCP 工具,
    /// 只是這幾個工具會回一句「未啟用」—— 而不是讓整個服務容器在啟動時解析失敗。
    /// The store is <b>optional</b>. A host that has not registered auto reply can still mount this tool set;
    /// these tools answer that the feature is off, rather than making the whole container fail to resolve at
    /// startup.
    /// </remarks>
    public LineAutoReplyTools(IOptions<LineMcpOptions> options, ILineAutoReplyStore? store = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
        _store = store;
    }

    /// <summary>
    /// 列出所有自動回覆規則。
    /// Lists every auto reply rule.
    /// </summary>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>規則清單的 JSON。The rules as JSON.</returns>
    [McpServerTool(Name = "line_list_auto_replies")]
    [Description("列出所有關鍵字自動回覆規則,依優先序排列(數字小的先比對)。" +
                 "每條規則含比對方式、樣式、是否啟用、優先序與要回覆的訊息內容。")]
    public async Task<string> ListAutoRepliesAsync(CancellationToken cancellationToken = default)
    {
        if (_store is null)
        {
            return NotEnabled();
        }

        var rules = await _store.ListAsync(cancellationToken).ConfigureAwait(false);

        return LineMcpJson.Ok(new
        {
            ok = true,
            count = rules.Count,
            rules = rules.Select(Describe),
        });
    }

    /// <summary>
    /// 新增或更新一條規則。
    /// Adds or updates a rule.
    /// </summary>
    /// <param name="name">規則名稱。The rule's name.</param>
    /// <param name="match">比對方式。The match mode.</param>
    /// <param name="messagesJson">回覆內容。The reply body.</param>
    /// <param name="id">規則識別碼。The rule identifier.</param>
    /// <param name="pattern">比對樣式。The pattern.</param>
    /// <param name="enabled">是否啟用。Whether it is on.</param>
    /// <param name="ignoreCase">是否忽略大小寫。Whether case is ignored.</param>
    /// <param name="priority">優先序。The priority.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>結果的 JSON。The result as JSON.</returns>
    [McpServerTool(Name = "line_upsert_auto_reply")]
    [Description("新增或更新一條自動回覆規則。id 省略代表新增,給了就是更新那一條。" +
                 "match 可填六種:Exact(整句相同)、StartsWith(開頭符合)、Contains(包含)、Regex(正規表示式)、" +
                 "Follow(加好友時的歡迎訊息,不看 pattern)、Fallback(收到文字但沒有其他規則命中時的回覆,不看 pattern)。" +
                 "messagesJson 為 LINE 訊息陣列(1~5 則),可用佔位符 {{text}}(使用者原訊息)與 {{displayName}}(使用者顯示名稱)。" +
                 "priority 小的先比對;同分時 Exact > StartsWith > Contains > Regex。")]
    public async Task<string> UpsertAutoReplyAsync(
        [Description("規則名稱,給人看的")] string name,
        [Description("比對方式:Exact / StartsWith / Contains / Regex / Follow / Fallback")] string match,
        [Description("回覆的 LINE 訊息陣列 JSON,1~5 則,可含 {{text}} 與 {{displayName}}")] string messagesJson,
        [Description("規則識別碼;省略代表新增一條")] string? id = null,
        [Description("比對樣式;Follow 與 Fallback 不使用")] string? pattern = null,
        [Description("是否啟用,預設 true")] bool enabled = true,
        [Description("比對時是否忽略大小寫,預設 true")] bool ignoreCase = true,
        [Description("優先序,數字小的先比對,預設 100")] int priority = 100,
        CancellationToken cancellationToken = default)
    {
        if (_store is null)
        {
            return NotEnabled();
        }

        if (!_options.Value.AllowRuleEdits)
        {
            return LineMcpJson.Error("這個站台不允許經由 MCP 編輯自動回覆規則(AllowRuleEdits 為 false)。請改由宿主的後台操作。");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return LineMcpJson.Error("name 不可為空白:規則列表上只看得到名稱,沒有名稱的規則沒有人知道是做什麼的。");
        }

        if (!Enum.TryParse<LineAutoReplyMatchMode>(match, ignoreCase: true, out var mode))
        {
            return LineMcpJson.Error(
                $"match「{match}」不是有效的比對方式。可用值:{string.Join("、", Enum.GetNames<LineAutoReplyMatchMode>())}。");
        }

        var rule = new LineAutoReplyRule
        {
            Id = id ?? string.Empty,
            Name = name,
            Enabled = enabled,
            Match = mode,
            Pattern = pattern ?? string.Empty,
            IgnoreCase = ignoreCase,
            Priority = priority,
            MessagesJson = messagesJson,
        };

        // 存進去<b>之前</b>驗證。一條 JSON 壞掉的規則存進去之後,症狀是某些使用者傳訊息完全沒有回應,
        // 而 log 裡只有一行渲染失敗 —— 那時已經很難對回是哪一次編輯造成的。
        // Validated <b>before</b> it is stored. A rule with broken JSON in the store shows up as certain users
        // getting no reply at all, with one rendering failure in the log, and by then it is hard to tie back to
        // which edit caused it.
        var validated = LineAutoReplyMatcher.Validate(rule);
        if (validated.IsFailure)
        {
            return LineMcpJson.Error(validated.Error);
        }

        var stored = await _store.UpsertAsync(rule, cancellationToken).ConfigureAwait(false);

        return LineMcpJson.Ok(new
        {
            ok = true,
            rule = Describe(stored),
        });
    }

    /// <summary>
    /// 刪除一條規則。
    /// Deletes a rule.
    /// </summary>
    /// <param name="id">規則識別碼。The rule identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>結果的 JSON。The result as JSON.</returns>
    [McpServerTool(Name = "line_delete_auto_reply")]
    [Description("刪除一條自動回覆規則。只是想暫時停用的話,改用 line_upsert_auto_reply 把 enabled 設為 false —— " +
                 "停用的規則保留得住內容,刪掉的要重打一次。")]
    public async Task<string> DeleteAutoReplyAsync(
        [Description("規則識別碼")] string id,
        CancellationToken cancellationToken = default)
    {
        if (_store is null)
        {
            return NotEnabled();
        }

        if (!_options.Value.AllowRuleEdits)
        {
            return LineMcpJson.Error("這個站台不允許經由 MCP 編輯自動回覆規則(AllowRuleEdits 為 false)。請改由宿主的後台操作。");
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            return LineMcpJson.Error("id 不可為空白。");
        }

        var deleted = await _store.DeleteAsync(id, cancellationToken).ConfigureAwait(false);

        return deleted
            ? LineMcpJson.Ok(new { ok = true, id, deleted = true })
            : LineMcpJson.Error($"找不到規則 {id}。");
    }

    /// <summary>
    /// 試跑一段文字,看看會命中哪一條規則。
    /// Dry-runs a piece of text to see which rule it would hit.
    /// </summary>
    /// <param name="text">要試的文字。The text to try.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>試跑結果的 JSON。The dry run's result as JSON.</returns>
    /// <remarks>
    /// 這個工具<b>不會送出任何訊息</b>。它存在的理由是:規則多起來之後,一條寫得寬鬆的 Contains
    /// 很容易把一堆精準的 Exact 全部吃掉,而那種問題從規則列表上完全看不出來 —— 每一條單獨看都是對的。
    /// This tool <b>sends nothing</b>. It exists because, once there are many rules, one loose Contains easily
    /// swallows a pile of precise Exact ones, and nothing in the rule list shows it: every rule looks right on its
    /// own.
    /// </remarks>
    [McpServerTool(Name = "line_test_auto_reply")]
    [Description("試跑:給一段使用者可能會傳的文字,回報會命中哪一條規則、以及渲染後實際會回覆的訊息內容。" +
                 "本工具不會送出任何訊息。規則多的時候用它確認新規則有沒有被既有的寬鬆規則蓋掉。")]
    public async Task<string> TestAutoReplyAsync(
        [Description("要試跑的使用者訊息文字")] string text,
        CancellationToken cancellationToken = default)
    {
        if (_store is null)
        {
            return NotEnabled();
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return LineMcpJson.Error("text 不可為空白。");
        }

        var rules = await _store.ListAsync(cancellationToken).ConfigureAwait(false);
        var rule = LineAutoReplyMatcher.Match(rules, text)
            ?? LineAutoReplyMatcher.FindFirst(rules, LineAutoReplyMatchMode.Fallback);

        if (rule is null)
        {
            return LineMcpJson.Ok(new
            {
                ok = true,
                matched = false,
                note = "沒有任何規則命中,而且沒有 Fallback 規則,這段文字不會得到自動回覆。",
            });
        }

        // 試跑時 displayName 以空字串代入:這裡沒有真實的使用者,而編一個假名字會讓人誤以為
        // 那就是實際會出現的內容。
        // The display name is substituted with an empty string: there is no real user here, and inventing one
        // would suggest that what comes back is what a user would actually see.
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["text"] = text,
            ["displayName"] = string.Empty,
        };

        var rendered = LineTemplateRenderer.Render(rule.MessagesJson, values);

        return rendered.IsFailure
            ? LineMcpJson.Error(rendered.Error)
            : LineMcpJson.Ok(new
            {
                ok = true,
                matched = true,
                rule = Describe(rule),
                renderedMessagesJson = rendered.GetValueOrThrow(),
                note = "這是試跑結果,沒有送出任何訊息;displayName 以空字串代入。",
            });
    }

    /// <summary>
    /// 「宿主沒有啟用自動回覆」的回應。
    /// The response for a host that has not enabled auto reply.
    /// </summary>
    /// <returns>JSON 字串。The JSON string.</returns>
    private static string NotEnabled() =>
        LineMcpJson.Error("這個站台沒有啟用自動回覆功能(宿主未呼叫 services.AddLineAutoReply)。規則相關的工具都不可用。");

    /// <summary>
    /// 把規則整理成回傳用的形狀。
    /// Shapes a rule for the response.
    /// </summary>
    /// <param name="rule">規則。The rule.</param>
    /// <returns>回傳用的物件。The object to return.</returns>
    private static object Describe(LineAutoReplyRule rule) => new
    {
        id = rule.Id,
        name = rule.Name,
        enabled = rule.Enabled,
        match = rule.Match.ToString(),
        pattern = rule.Pattern,
        ignoreCase = rule.IgnoreCase,
        priority = rule.Priority,
        messagesJson = rule.MessagesJson,
        createdAt = rule.CreatedAt,
        updatedAt = rule.UpdatedAt,
    };
}
