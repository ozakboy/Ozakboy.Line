using System.Text.Json;

namespace Ozakboy.Line.Messaging.Messages.Imagemap;

/// <summary>
/// 圖片地圖上的一塊矩形範圍,單位為底圖上的像素(底圖寬度固定
/// <see cref="LineMessagingLimits.ImagemapBaseWidth"/>)。
/// One rectangle on an imagemap, in pixels on the base image (whose width is fixed at
/// <see cref="LineMessagingLimits.ImagemapBaseWidth"/>).
/// </summary>
/// <remarks>
/// 欄位對應 LINE 的 <c>area</c> 物件:<c>x</c>、<c>y</c>、<c>width</c>、<c>height</c>。
/// 座標要以 1040 寬的底圖為準 —— LINE 會依裝置向 <c>baseUrl/{width}</c> 抓不同尺寸的圖,
/// 但區域座標一律以 1040 為基準換算。
/// The fields map to LINE's <c>area</c> object: <c>x</c>, <c>y</c>, <c>width</c>, <c>height</c>. The
/// coordinates are relative to the 1040-wide base: LINE fetches a size to suit the device from
/// <c>baseUrl/{width}</c>, but area coordinates are always scaled from 1040.
/// </remarks>
/// <param name="X">左上角的水平座標。The horizontal coordinate of the top-left corner.</param>
/// <param name="Y">左上角的垂直座標。The vertical coordinate of the top-left corner.</param>
/// <param name="Width">範圍寬度。The width.</param>
/// <param name="Height">範圍高度。The height.</param>
public sealed record LineImagemapArea(int X, int Y, int Width, int Height)
{
    /// <summary>
    /// 寫出 <c>{ "x", "y", "width", "height" }</c>。
    /// Writes <c>{ "x", "y", "width", "height" }</c>.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    internal void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteNumber("x", X);
        writer.WriteNumber("y", Y);
        writer.WriteNumber("width", Width);
        writer.WriteNumber("height", Height);
        writer.WriteEndObject();
    }
}
