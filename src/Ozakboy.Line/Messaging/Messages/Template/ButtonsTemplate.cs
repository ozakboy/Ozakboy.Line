using System.Globalization;
using System.Text.Json;
using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Messaging.Actions;

namespace Ozakboy.Line.Messaging.Messages.Template;

/// <summary>
/// 按鈕範本(<c>buttons</c>):一張可選的圖片、標題、說明文字與最多四個按鈕。
/// The buttons template (<c>buttons</c>): an optional image, a title, body text and up to four buttons.
/// </summary>
/// <remarks>
/// 欄位對應 LINE 的 <c>thumbnailImageUrl</c>、<c>imageAspectRatio</c>、<c>imageSize</c>、
/// <c>imageBackgroundColor</c>、<c>title</c>、<c>text</c>、<c>defaultAction</c> 與 <c>actions</c>。
/// LINE 對字數的限制(標題 40 字、內文有圖或標題時 60 字、否則 160 字)不在本地檢查 ——
/// 那些上限依語系與版本調整過不只一次,寫死在這裡反而會擋掉 LINE 其實接受的內容。
/// The fields map to LINE's <c>thumbnailImageUrl</c>, <c>imageAspectRatio</c>, <c>imageSize</c>,
/// <c>imageBackgroundColor</c>, <c>title</c>, <c>text</c>, <c>defaultAction</c> and <c>actions</c>. LINE's
/// character limits (40 for the title, 60 for the text with an image or title and 160 otherwise) are not checked
/// locally: those caps have moved more than once across locales and versions, and hard-coding them here would
/// refuse content LINE actually accepts.
/// </remarks>
public sealed class ButtonsTemplate : LineTemplate
{
    /// <summary>圖片比例 1.51:1,LINE 的預設值。The 1.51:1 image ratio, LINE's default.</summary>
    public const string AspectRatioRectangle = "rectangle";

    /// <summary>圖片比例 1:1。The 1:1 image ratio.</summary>
    public const string AspectRatioSquare = "square";

    /// <summary>圖片填滿區域、超出的部分裁掉,LINE 的預設值。The image fills the area and is cropped; LINE's default.</summary>
    public const string ImageSizeCover = "cover";

    /// <summary>圖片完整顯示、留白補滿。The image is shown whole, with the background filling the rest.</summary>
    public const string ImageSizeContain = "contain";

    /// <summary>
    /// 建立按鈕範本。
    /// Creates a buttons template.
    /// </summary>
    /// <param name="text">內文。The body text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="text"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="text"/> is <see langword="null"/> or blank.
    /// </exception>
    public ButtonsTemplate(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Text = text;
    }

    /// <inheritdoc />
    public override string Type => "buttons";

    /// <summary>
    /// 內文。
    /// The body text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// 圖片位址(HTTPS、JPEG 或 PNG);不放圖時為 <see langword="null"/>。
    /// The image address (HTTPS, JPEG or PNG), or <see langword="null"/> for no image.
    /// </summary>
    public string? ThumbnailImageUrl { get; set; }

    /// <summary>
    /// 圖片比例:<see cref="AspectRatioRectangle"/> 或 <see cref="AspectRatioSquare"/>。
    /// The image ratio: <see cref="AspectRatioRectangle"/> or <see cref="AspectRatioSquare"/>.
    /// </summary>
    public string? ImageAspectRatio { get; set; }

    /// <summary>
    /// 圖片的填滿方式:<see cref="ImageSizeCover"/> 或 <see cref="ImageSizeContain"/>。
    /// How the image fills its area: <see cref="ImageSizeCover"/> or <see cref="ImageSizeContain"/>.
    /// </summary>
    public string? ImageSize { get; set; }

    /// <summary>
    /// 圖片區域的背景色,<c>#RRGGBB</c> 格式;<see cref="ImageSize"/> 為 contain 時才看得到。
    /// The image area's background colour as <c>#RRGGBB</c>, visible when <see cref="ImageSize"/> is contain.
    /// </summary>
    public string? ImageBackgroundColor { get; set; }

    /// <summary>
    /// 標題;不放時為 <see langword="null"/>。
    /// The title, or <see langword="null"/> for none.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// 點圖片或標題時觸發的動作;不設定時那些區域點了沒反應。
    /// The action for a tap on the image or title; without one those areas do nothing.
    /// </summary>
    public LineAction? DefaultAction { get; set; }

    /// <summary>
    /// 按鈕,1 到 <see cref="LineMessagingLimits.MaxButtonsTemplateActions"/> 個。
    /// The buttons, between one and <see cref="LineMessagingLimits.MaxButtonsTemplateActions"/>.
    /// </summary>
    public IList<LineAction> Actions { get; } = [];

    /// <inheritdoc />
    public override Result Validate() =>
        Actions.Count is >= 1 and <= LineMessagingLimits.MaxButtonsTemplateActions
            ? Result.Success()
            : Invalid(string.Create(
                CultureInfo.InvariantCulture,
                $"按鈕範本需帶 1 到 {LineMessagingLimits.MaxButtonsTemplateActions} 個動作,這次是 {Actions.Count} 個。A buttons template takes between 1 and {LineMessagingLimits.MaxButtonsTemplateActions} actions; {Actions.Count} were supplied."));

    /// <inheritdoc />
    internal override void WriteBody(Utf8JsonWriter writer)
    {
        WriteOptional(writer, "thumbnailImageUrl", ThumbnailImageUrl);
        WriteOptional(writer, "imageAspectRatio", ImageAspectRatio);
        WriteOptional(writer, "imageSize", ImageSize);
        WriteOptional(writer, "imageBackgroundColor", ImageBackgroundColor);
        WriteOptional(writer, "title", Title);
        writer.WriteString("text", Text);

        if (DefaultAction is { } defaultAction)
        {
            writer.WritePropertyName("defaultAction");
            defaultAction.WriteTo(writer);
        }

        WriteActions(writer, Actions);
    }
}
