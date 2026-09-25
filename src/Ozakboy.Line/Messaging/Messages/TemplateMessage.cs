using System.Text.Json;
using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Messaging.Messages.Template;

namespace Ozakboy.Line.Messaging.Messages;

/// <summary>
/// 範本訊息:LINE 預先排好版的按鈕、確認、輪播與圖片輪播。
/// A template message: LINE's pre-laid-out buttons, confirm, carousel and image carousel.
/// </summary>
/// <remarks>
/// <para>
/// JSON 形狀是 <c>{ "type": "template", "altText": …, "template": { "type": "buttons" | "confirm" |
/// "carousel" | "image_carousel", … } }</c>。外層的 <c>type</c> 固定是 <c>template</c>,真正決定長相的是
/// 裡面那個 <see cref="Template"/>。
/// The JSON shape is <c>{ "type": "template", "altText": …, "template": { "type": "buttons" | "confirm" |
/// "carousel" | "image_carousel", … } }</c>. The outer <c>type</c> is always <c>template</c>; what decides the
/// look is the inner <see cref="Template"/>.
/// </para>
/// <para>
/// 範本是 LINE 早期的版面機制,樣式固定、欄位少;要自訂版面請用 <see cref="FlexMessage"/>。
/// 範本的長處是不必自己排版,四個按鈕、兩個選項、十張卡片這些常見形狀直接就能用。
/// Templates are LINE's older layout mechanism, fixed in style with few fields; a custom layout is
/// <see cref="FlexMessage"/>'s job. What templates offer is not having to lay anything out: four buttons, two
/// choices or ten cards come ready-made.
/// </para>
/// </remarks>
public sealed class TemplateMessage : LineMessage
{
    /// <summary>
    /// 建立範本訊息。
    /// Creates a template message.
    /// </summary>
    /// <param name="altText">
    /// 替代文字,顯示在通知與不支援範本的環境。
    /// The alternative text, shown in notifications and where templates are not supported.
    /// </param>
    /// <param name="template">範本內容。The template.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="altText"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="altText"/> is <see langword="null"/> or blank.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="template"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="template"/> is <see langword="null"/>.
    /// </exception>
    public TemplateMessage(string altText, LineTemplate template)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(altText);
        ArgumentNullException.ThrowIfNull(template);

        AltText = altText;
        Template = template;
    }

    /// <inheritdoc />
    public override string Type => "template";

    /// <summary>
    /// 替代文字。
    /// The alternative text.
    /// </summary>
    public string AltText { get; }

    /// <summary>
    /// 範本內容。
    /// The template.
    /// </summary>
    public LineTemplate Template { get; }

    /// <summary>
    /// 先檢查快速回覆,再檢查範本自己的動作數與欄數。
    /// Checks the quick reply first, then the template's own action and column counts.
    /// </summary>
    /// <returns>
    /// 全部通過時為成功;範本不合規時為 <see cref="LineErrorCodes.InvalidTemplate"/> 失敗。
    /// Success when everything passes; a <see cref="LineErrorCodes.InvalidTemplate"/> failure when the template
    /// does not conform.
    /// </returns>
    public override Result Validate()
    {
        var quickReply = base.Validate();
        return quickReply.IsFailure ? quickReply : Template.Validate();
    }

    /// <inheritdoc />
    internal override void WriteBody(Utf8JsonWriter writer)
    {
        writer.WriteString("altText", AltText);
        writer.WritePropertyName("template");
        Template.WriteTo(writer);
    }
}
