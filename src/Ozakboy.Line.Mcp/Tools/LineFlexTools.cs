using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using ModelContextProtocol.Server;
using Ozakboy.Line.Messaging.Messages;
using Ozakboy.Line.Messaging.Messages.Flex;

namespace Ozakboy.Line.Mcp.Tools;

/// <summary>
/// Flex Message 的自檢工具。
/// The self-check tool for Flex messages.
/// </summary>
/// <remarks>
/// 唯讀、不送出任何東西、不進待審佇列、不計每日額度:它讓外部 AI 在把 Flex 版面排進佇列<b>之前</b>先知道
/// 它能不能過本地驗證 —— 否則要等送出類工具提出時才失敗,或更糟,等到 LINE 回一個只說「請求內容有 1 個錯誤」的 400。
/// Read-only: it sends nothing, skips the review queue and counts against no daily cap. It lets an external AI learn
/// whether a Flex layout passes local validation <b>before</b> queueing it — otherwise the failure only shows when a
/// sending tool proposes it, or worse, as a 400 from LINE that says only that the body has one error.
/// </remarks>
[McpServerToolType]
public sealed class LineFlexTools
{
    private const string PlaceholderAltText = "flex";

    private static readonly JsonSerializerOptions SizeOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// 檢查一份 Flex 版面 JSON。
    /// Checks a Flex layout's JSON.
    /// </summary>
    /// <param name="contentsJson">
    /// Flex 版面 JSON(<c>bubble</c> 或 <c>carousel</c>);也接受整則 <c>type=flex</c> 的訊息物件(取其 <c>contents</c>)。
    /// The Flex layout JSON (a <c>bubble</c> or a <c>carousel</c>); a whole <c>type=flex</c> message object is accepted too, in which case its <c>contents</c> is used.
    /// </param>
    /// <param name="altText">替代文字;省略時不檢查它。The alternative text, not checked when omitted.</param>
    /// <returns>檢查結果的 JSON。The check result as JSON.</returns>
    [McpServerTool(Name = "line_validate_flex", ReadOnly = true)]
    [Description("在排進佇列之前自檢一份 Flex Message 版面 JSON(bubble 或 carousel,也接受整則 type=flex 的訊息)。唯讀,不送出、不計每日額度。回傳:能否解析為強型別(parsed)、哪些節點因本套件沒建模而原樣保留(rawNodes,這些節點不會被驗證)、本地驗證結果(valid,失敗時 code 與 error 會帶版面路徑)、JSON 大小與上限。")]
    public static string ValidateFlex(
        [Description("Flex 版面 JSON(bubble 或 carousel)")] string contentsJson,
        [Description("替代文字(選填);給了就一併檢查 400 字上限")] string? altText = null)
    {
        if (string.IsNullOrWhiteSpace(contentsJson))
        {
            return LineMcpJson.Error("contentsJson 不可為空白。");
        }

        var layout = contentsJson;
        var alt = altText;

        // 整則訊息物件:取出 contents 與 altText,免得 AI 把外層也貼進來就整個失敗。
        // A whole message object: take its contents and altText, so an AI that pastes the outer envelope too is not failed for it.
        try
        {
            using var document = JsonDocument.Parse(contentsJson);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("type", out var type)
                && type.ValueKind == JsonValueKind.String
                && type.GetString() == "flex"
                && document.RootElement.TryGetProperty("contents", out var inner))
            {
                layout = inner.GetRawText();
                if (alt is null
                    && document.RootElement.TryGetProperty("altText", out var altElement)
                    && altElement.ValueKind == JsonValueKind.String)
                {
                    alt = altElement.GetString();
                }
            }
        }
        catch (JsonException)
        {
            // 交給下面的 Parse 回報不合法的 JSON。
            // Parse below reports the invalid JSON.
        }

        var parsed = FlexMessage.Parse(string.IsNullOrWhiteSpace(alt) ? PlaceholderAltText : alt, layout);
        if (parsed.IsFailure)
        {
            return LineMcpJson.Ok(new
            {
                ok = true,
                parsed = false,
                valid = false,
                code = parsed.Error!.Code,
                error = parsed.Error.Message,
            });
        }

        var message = parsed.GetValueOrThrow();
        var container = message.Container!;
        var rawNodes = container.GetRawNodePaths();
        var validation = message.Validate();
        var sizeBytes = JsonSerializer.SerializeToUtf8Bytes(container, SizeOptions).Length;

        return LineMcpJson.Ok(new
        {
            ok = true,
            parsed = true,
            containerType = container.Type,
            fullyTyped = rawNodes.Count == 0,
            rawNodes,
            valid = validation.IsSuccess,
            code = validation.IsSuccess ? null : validation.Error!.Code,
            error = validation.IsSuccess ? null : validation.Error!.Message,
            sizeBytes,
            sizeLimitBytes = container.Type == "carousel" ? LineMessagingLimits.MaxFlexCarouselBytes : LineMessagingLimits.MaxFlexBubbleBytes,
            note = rawNodes.Count == 0
                ? null
                : "rawNodes 列出的節點本套件不認得或含未建模欄位,原樣保留、不經本地驗證,是否合規以 LINE 為準。",
        });
    }
}
