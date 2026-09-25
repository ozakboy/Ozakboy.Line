using System.Globalization;
using System.Text.Json;
using Ozakboy.Core.Abstractions;

namespace Ozakboy.Line.Messaging.Messages.Template;

/// <summary>
/// 圖片輪播範本(<c>image_carousel</c>):橫向滑動的多張圖,每張各一個動作。
/// The image carousel template (<c>image_carousel</c>): images that scroll sideways, one action each.
/// </summary>
/// <remarks>
/// 欄位對應 LINE 的 <c>columns</c>;欄數 1 到 <see cref="LineMessagingLimits.MaxCarouselColumns"/>
/// 在本地檢查。
/// The field maps to LINE's <c>columns</c>; the column count, one to
/// <see cref="LineMessagingLimits.MaxCarouselColumns"/>, is checked locally.
/// </remarks>
public sealed class ImageCarouselTemplate : LineTemplate
{
    /// <inheritdoc />
    public override string Type => "image_carousel";

    /// <summary>
    /// 各欄,1 到 <see cref="LineMessagingLimits.MaxCarouselColumns"/> 欄。
    /// The columns, between one and <see cref="LineMessagingLimits.MaxCarouselColumns"/>.
    /// </summary>
    public IList<ImageCarouselColumn> Columns { get; } = [];

    /// <inheritdoc />
    public override Result Validate() =>
        Columns.Count is >= 1 and <= LineMessagingLimits.MaxCarouselColumns
            ? Result.Success()
            : Invalid(string.Create(
                CultureInfo.InvariantCulture,
                $"圖片輪播範本需帶 1 到 {LineMessagingLimits.MaxCarouselColumns} 欄,這次是 {Columns.Count} 欄。An image carousel template takes between 1 and {LineMessagingLimits.MaxCarouselColumns} columns; {Columns.Count} were supplied."));

    /// <inheritdoc />
    internal override void WriteBody(Utf8JsonWriter writer)
    {
        writer.WriteStartArray("columns");
        for (var index = 0; index < Columns.Count; index++)
        {
            Columns[index].WriteTo(writer);
        }

        writer.WriteEndArray();
    }
}
