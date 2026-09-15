using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Ozakboy.Line.Templates;

namespace Ozakboy.Line.Mcp.Tools;

/// <summary>
/// 訊息範本的管理與渲染工具。
/// The tools for managing and rendering message templates.
/// </summary>
/// <remarks>
/// 範本<b>不會自己送出去</b>,所以這一組工具不走待審流程。渲染工具回的是渲染後的訊息 JSON,
/// 要送出還得再呼叫一次送出類工具 —— 而那一步是有待審的。
/// A template <b>sends nothing by itself</b>, so this group skips the review queue. The render tool returns the
/// rendered message JSON, and actually sending it means calling a sending tool — which does go through review.
/// </remarks>
[McpServerToolType]
public sealed class LineTemplateTools
{
    private readonly IOptions<LineMcpOptions> _options;
    private readonly ILineTemplateStore? _store;

    /// <summary>
    /// 建立工具組。
    /// Creates the tool set.
    /// </summary>
    /// <param name="options">MCP 設定。The MCP settings.</param>
    /// <param name="store">
    /// 範本儲存體;宿主沒有呼叫 <c>AddLineTemplates</c> 時為 <see langword="null"/>。
    /// The template store, or <see langword="null"/> when the host has not called <c>AddLineTemplates</c>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="options"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    public LineTemplateTools(IOptions<LineMcpOptions> options, ILineTemplateStore? store = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
        _store = store;
    }

    /// <summary>
    /// 列出所有範本。
    /// Lists every template.
    /// </summary>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>範本清單的 JSON。The templates as JSON.</returns>
    [McpServerTool(Name = "line_list_templates")]
    [Description("列出所有訊息範本,含名稱、說明與各自用到的變數名稱。內容本身不在列表裡,要看請用 line_get_template。")]
    public async Task<string> ListTemplatesAsync(CancellationToken cancellationToken = default)
    {
        if (_store is null)
        {
            return NotEnabled();
        }

        var templates = await _store.ListAsync(cancellationToken).ConfigureAwait(false);

        return LineMcpJson.Ok(new
        {
            ok = true,
            count = templates.Count,
            templates = templates.Select(template => new
            {
                id = template.Id,
                name = template.Name,
                description = template.Description,
                variables = LineTemplateVariables.Extract(template.MessagesJson),
                createdAt = template.CreatedAt,
                updatedAt = template.UpdatedAt,
            }),
        });
    }

    /// <summary>
    /// 取得單一範本的完整內容。
    /// Gets one template in full.
    /// </summary>
    /// <param name="id">範本識別碼。The template identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>範本的 JSON。The template as JSON.</returns>
    [McpServerTool(Name = "line_get_template")]
    [Description("取得單一訊息範本的完整內容,含未渲染的 messagesJson 與它用到的變數名稱清單。")]
    public async Task<string> GetTemplateAsync(
        [Description("範本識別碼")] string id,
        CancellationToken cancellationToken = default)
    {
        if (_store is null)
        {
            return NotEnabled();
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            return LineMcpJson.Error("id 不可為空白。");
        }

        var template = await _store.GetAsync(id, cancellationToken).ConfigureAwait(false);

        return template is null
            ? LineMcpJson.Error($"找不到範本 {id}。")
            : LineMcpJson.Ok(new
            {
                ok = true,
                template = new
                {
                    id = template.Id,
                    name = template.Name,
                    description = template.Description,
                    messagesJson = template.MessagesJson,
                    variables = LineTemplateVariables.Extract(template.MessagesJson),
                    createdAt = template.CreatedAt,
                    updatedAt = template.UpdatedAt,
                },
            });
    }

    /// <summary>
    /// 新增或更新範本。
    /// Adds or updates a template.
    /// </summary>
    /// <param name="name">範本名稱。The template's name.</param>
    /// <param name="messagesJson">範本內容。The template body.</param>
    /// <param name="id">範本識別碼。The template identifier.</param>
    /// <param name="description">補充說明。The description.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>結果的 JSON。The result as JSON.</returns>
    [McpServerTool(Name = "line_upsert_template")]
    [Description("新增或更新一份訊息範本。id 省略代表新增,給了就是更新那一份。" +
                 "messagesJson 為 LINE 訊息陣列(1~5 則),可用 {{變數名}} 佔位符,變數名限英文字母或底線開頭。" +
                 "存入前會驗證 JSON 與則數,不合規會直接退回而不會存進去。")]
    public async Task<string> UpsertTemplateAsync(
        [Description("範本名稱,給人看的")] string name,
        [Description("LINE 訊息陣列 JSON,1~5 則,可含 {{變數名}} 佔位符")] string messagesJson,
        [Description("範本識別碼;省略代表新增一份")] string? id = null,
        [Description("補充說明")] string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (_store is null)
        {
            return NotEnabled();
        }

        if (!_options.Value.AllowTemplateEdits)
        {
            return LineMcpJson.Error("這個站台不允許經由 MCP 編輯訊息範本(AllowTemplateEdits 為 false)。請改由宿主的後台操作。");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return LineMcpJson.Error("name 不可為空白。");
        }

        var validated = LineTemplateRenderer.Validate(messagesJson ?? string.Empty);
        if (validated.IsFailure)
        {
            return LineMcpJson.Error(validated.Error);
        }

        var stored = await _store.UpsertAsync(
            new LineMessageTemplate
            {
                Id = id ?? string.Empty,
                Name = name,
                Description = description,
                MessagesJson = messagesJson!,
            },
            cancellationToken).ConfigureAwait(false);

        return LineMcpJson.Ok(new
        {
            ok = true,
            template = new
            {
                id = stored.Id,
                name = stored.Name,
                description = stored.Description,
                variables = LineTemplateVariables.Extract(stored.MessagesJson),
                updatedAt = stored.UpdatedAt,
            },
        });
    }

    /// <summary>
    /// 刪除範本。
    /// Deletes a template.
    /// </summary>
    /// <param name="id">範本識別碼。The template identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>結果的 JSON。The result as JSON.</returns>
    [McpServerTool(Name = "line_delete_template")]
    [Description("刪除一份訊息範本。刪掉之後內容不會留下副本,不確定的話請先用 line_get_template 取回內容。")]
    public async Task<string> DeleteTemplateAsync(
        [Description("範本識別碼")] string id,
        CancellationToken cancellationToken = default)
    {
        if (_store is null)
        {
            return NotEnabled();
        }

        if (!_options.Value.AllowTemplateEdits)
        {
            return LineMcpJson.Error("這個站台不允許經由 MCP 編輯訊息範本(AllowTemplateEdits 為 false)。請改由宿主的後台操作。");
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            return LineMcpJson.Error("id 不可為空白。");
        }

        var deleted = await _store.DeleteAsync(id, cancellationToken).ConfigureAwait(false);

        return deleted
            ? LineMcpJson.Ok(new { ok = true, id, deleted = true })
            : LineMcpJson.Error($"找不到範本 {id}。");
    }

    /// <summary>
    /// 把變數代入範本,回傳渲染後的訊息 JSON。
    /// Substitutes a template's variables and returns the rendered message JSON.
    /// </summary>
    /// <param name="id">範本識別碼。The template identifier.</param>
    /// <param name="variablesJson">變數的 JSON 物件。The variables as a JSON object.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>渲染結果的 JSON。The rendered result as JSON.</returns>
    [McpServerTool(Name = "line_render_template")]
    [Description("把變數代入一份訊息範本,回傳渲染後的 LINE 訊息陣列 JSON。變數以 JSON 物件提供,例如 {\"name\":\"王小明\"}。" +
                 "本工具不會送出任何訊息:要送出請把回傳的 messagesJson 交給 line_send_push / line_send_multicast / line_send_broadcast。" +
                 "缺變數時會直接退回並列出缺哪幾個,不會把 {{變數名}} 原樣留在內容裡。")]
    public async Task<string> RenderTemplateAsync(
        [Description("範本識別碼")] string id,
        [Description("變數的 JSON 物件,鍵為變數名、值為字串")] string variablesJson,
        CancellationToken cancellationToken = default)
    {
        if (_store is null)
        {
            return NotEnabled();
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            return LineMcpJson.Error("id 不可為空白。");
        }

        var template = await _store.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (template is null)
        {
            return LineMcpJson.Error($"找不到範本 {id}。");
        }

        var values = ParseVariables(variablesJson);
        if (values is null)
        {
            return LineMcpJson.Error("variablesJson 必須是一個鍵值皆為字串的 JSON 物件,例如 {\"name\":\"王小明\"}。");
        }

        var rendered = LineTemplateRenderer.Render(template.MessagesJson, values);

        return rendered.IsFailure
            ? LineMcpJson.Error(rendered.Error)
            : LineMcpJson.Ok(new
            {
                ok = true,
                templateId = template.Id,
                messagesJson = rendered.GetValueOrThrow(),
                note = "這是渲染結果,尚未送出。要送出請交給 line_send_* 工具。",
            });
    }

    /// <summary>
    /// 把變數 JSON 讀成字典。
    /// Reads the variables JSON into a dictionary.
    /// </summary>
    /// <param name="variablesJson">變數的 JSON 物件。The variables as a JSON object.</param>
    /// <returns>讀不出來時為 <see langword="null"/>。<see langword="null"/> when it cannot be read.</returns>
    /// <remarks>
    /// 數字與布林值也接受,轉成它們的字面文字。範本的洞最後都是填進 JSON 字串裡的,
    /// 而「AI 把 3 寫成數字而不是字串」是很常見的一件事,為此退回一次呼叫不划算。
    /// Numbers and booleans are accepted too and become their literal text. A template's holes end up inside JSON
    /// strings anyway, and an AI writing 3 as a number rather than a string is common enough that refusing the
    /// call over it is not worth it.
    /// </remarks>
    private static Dictionary<string, string>? ParseVariables(string variablesJson)
    {
        if (string.IsNullOrWhiteSpace(variablesJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(variablesJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                values[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                    JsonValueKind.Null => string.Empty,
                    _ => property.Value.GetRawText(),
                };
            }

            return values;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 「宿主沒有啟用訊息範本」的回應。
    /// The response for a host that has not enabled message templates.
    /// </summary>
    /// <returns>JSON 字串。The JSON string.</returns>
    private static string NotEnabled() =>
        LineMcpJson.Error("這個站台沒有啟用訊息範本功能(宿主未呼叫 services.AddLineTemplates)。範本相關的工具都不可用。");
}
