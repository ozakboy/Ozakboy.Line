using System.Text.Json;
using Ozakboy.Line.Messaging.Actions;

namespace Ozakboy.Line.Messaging.Messages.Template;

/// <summary>
/// 輪播範本的一欄:可選的圖片、標題、內文與最多三個按鈕。
/// One column of a carousel template: an optional image, a title, body text and up to three buttons.
/// </summary>
/// <remarks>
/// 欄位對應 LINE 的 <c>thumbnailImageUrl</c>、<c>imageBackgroundColor</c>、<c>title</c>、<c>text</c>、
/// <c>defaultAction</c> 與 <c>actions</c>。圖片比例與填滿方式是整個輪播共用的,在
/// <see cref="CarouselTemplate"/> 上設定,不在單一欄上。
/// The fields map to LINE's <c>thumbnailImageUrl</c>, <c>imageBackgroundColor</c>, <c>title</c>, <c>text</c>,
/// <c>defaultAction</c> and <c>actions</c>. The image ratio and fill are shared by the whole carousel and set on
/// <see cref="CarouselTemplate"/>, not per column.
/// </remarks>
public sealed class CarouselColumn
{
    /// <summary>
    /// 建立一欄。
    /// Creates a column.
    /// </summary>
    /// <param name="text">內文。The body text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="text"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="text"/> is <see langword="null"/> or blank.
    /// </exception>
    public CarouselColumn(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Text = text;
    }

    /// <summary>
    /// 內文。
    /// The body text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// 圖片位址;不放圖時為 <see langword="null"/>。同一個輪播裡要嘛每欄都有圖、要嘛都沒有。
    /// The image address, or <see langword="null"/> for none. Within one carousel either every column has an
    /// image or none does.
    /// </summary>
    public string? ThumbnailImageUrl { get; set; }

    /// <summary>
    /// 圖片區域的背景色,<c>#RRGGBB</c> 格式。
    /// The image area's background colour as <c>#RRGGBB</c>.
    /// </summary>
    public string? ImageBackgroundColor { get; set; }

    /// <summary>
    /// 標題;不放時為 <see langword="null"/>。同一個輪播裡要嘛每欄都有標題、要嘛都沒有。
    /// The title, or <see langword="null"/> for none. Within one carousel either every column has a title or none
    /// does.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// 點圖片或標題時觸發的動作。
    /// The action for a tap on the image or title.
    /// </summary>
    public LineAction? DefaultAction { get; set; }

    /// <summary>
    /// 按鈕,1 到 <see cref="LineMessagingLimits.MaxCarouselColumnActions"/> 個,且每欄數量必須一致。
    /// The buttons, between one and <see cref="LineMessagingLimits.MaxCarouselColumnActions"/>, the same number
    /// in every column.
    /// </summary>
    public IList<LineAction> Actions { get; } = [];

    /// <summary>
    /// 寫出這一欄。
    /// Writes this column.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    internal void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();

        if (!string.IsNullOrWhiteSpace(ThumbnailImageUrl))
        {
            writer.WriteString("thumbnailImageUrl", ThumbnailImageUrl);
        }

        if (!string.IsNullOrWhiteSpace(ImageBackgroundColor))
        {
            writer.WriteString("imageBackgroundColor", ImageBackgroundColor);
        }

        if (!string.IsNullOrWhiteSpace(Title))
        {
            writer.WriteString("title", Title);
        }

        writer.WriteString("text", Text);

        if (DefaultAction is { } defaultAction)
        {
            writer.WritePropertyName("defaultAction");
            defaultAction.WriteTo(writer);
        }

        writer.WriteStartArray("actions");
        for (var index = 0; index < Actions.Count; index++)
        {
            Actions[index].WriteTo(writer);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
