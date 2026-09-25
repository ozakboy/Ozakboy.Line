using System.Globalization;
using System.Text.Json;
using Ozakboy.Core.Abstractions;

namespace Ozakboy.Line.Messaging.Messages.Template;

/// <summary>
/// 輪播範本(<c>carousel</c>):橫向滑動的多欄,每欄有自己的圖片、文字與按鈕。
/// The carousel template (<c>carousel</c>): columns that scroll sideways, each with its own image, text and
/// buttons.
/// </summary>
/// <remarks>
/// 欄位對應 LINE 的 <c>columns</c>、<c>imageAspectRatio</c> 與 <c>imageSize</c>。
/// 本地檢查三件事:欄數 1 到 <see cref="LineMessagingLimits.MaxCarouselColumns"/>、每欄動作數 1 到
/// <see cref="LineMessagingLimits.MaxCarouselColumnActions"/>,以及<b>所有欄的動作數一致</b> ——
/// 最後那一條最容易踩到,而 LINE 的 400 不會說是哪一欄多了一個。
/// The fields map to LINE's <c>columns</c>, <c>imageAspectRatio</c> and <c>imageSize</c>. Three things are
/// checked locally: between one and <see cref="LineMessagingLimits.MaxCarouselColumns"/> columns, between one
/// and <see cref="LineMessagingLimits.MaxCarouselColumnActions"/> actions per column, and <b>the same action
/// count in every column</b>. The last is the easiest to trip over, and LINE's 400 never says which column has one
/// too many.
/// </remarks>
public sealed class CarouselTemplate : LineTemplate
{
    /// <inheritdoc />
    public override string Type => "carousel";

    /// <summary>
    /// 各欄,1 到 <see cref="LineMessagingLimits.MaxCarouselColumns"/> 欄。
    /// The columns, between one and <see cref="LineMessagingLimits.MaxCarouselColumns"/>.
    /// </summary>
    public IList<CarouselColumn> Columns { get; } = [];

    /// <summary>
    /// 圖片比例:<see cref="ButtonsTemplate.AspectRatioRectangle"/> 或
    /// <see cref="ButtonsTemplate.AspectRatioSquare"/>,整個輪播共用。
    /// The image ratio, <see cref="ButtonsTemplate.AspectRatioRectangle"/> or
    /// <see cref="ButtonsTemplate.AspectRatioSquare"/>, shared by the whole carousel.
    /// </summary>
    public string? ImageAspectRatio { get; set; }

    /// <summary>
    /// 圖片的填滿方式:<see cref="ButtonsTemplate.ImageSizeCover"/> 或
    /// <see cref="ButtonsTemplate.ImageSizeContain"/>,整個輪播共用。
    /// How the images fill their area, <see cref="ButtonsTemplate.ImageSizeCover"/> or
    /// <see cref="ButtonsTemplate.ImageSizeContain"/>, shared by the whole carousel.
    /// </summary>
    public string? ImageSize { get; set; }

    /// <inheritdoc />
    public override Result Validate()
    {
        if (Columns.Count is < 1 or > LineMessagingLimits.MaxCarouselColumns)
        {
            return Invalid(string.Create(
                CultureInfo.InvariantCulture,
                $"輪播範本需帶 1 到 {LineMessagingLimits.MaxCarouselColumns} 欄,這次是 {Columns.Count} 欄。A carousel template takes between 1 and {LineMessagingLimits.MaxCarouselColumns} columns; {Columns.Count} were supplied."));
        }

        var expected = Columns[0].Actions.Count;
        for (var index = 0; index < Columns.Count; index++)
        {
            var count = Columns[index].Actions.Count;
            if (count is < 1 or > LineMessagingLimits.MaxCarouselColumnActions)
            {
                return Invalid(string.Create(
                    CultureInfo.InvariantCulture,
                    $"輪播範本每欄需帶 1 到 {LineMessagingLimits.MaxCarouselColumnActions} 個動作,第 {index + 1} 欄是 {count} 個。Each carousel column takes between 1 and {LineMessagingLimits.MaxCarouselColumnActions} actions; column {index + 1} has {count}."));
            }

            if (count != expected)
            {
                return Invalid(string.Create(
                    CultureInfo.InvariantCulture,
                    $"輪播範本每欄的動作數必須一致:第 1 欄是 {expected} 個,第 {index + 1} 欄是 {count} 個。Every carousel column must carry the same number of actions: column 1 has {expected}, column {index + 1} has {count}."));
            }
        }

        return Result.Success();
    }

    /// <inheritdoc />
    internal override void WriteBody(Utf8JsonWriter writer)
    {
        writer.WriteStartArray("columns");
        for (var index = 0; index < Columns.Count; index++)
        {
            Columns[index].WriteTo(writer);
        }

        writer.WriteEndArray();
        WriteOptional(writer, "imageAspectRatio", ImageAspectRatio);
        WriteOptional(writer, "imageSize", ImageSize);
    }
}
