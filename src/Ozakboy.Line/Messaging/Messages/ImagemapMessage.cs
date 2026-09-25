using System.Globalization;
using System.Text.Json;
using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Messaging.Messages.Imagemap;

namespace Ozakboy.Line.Messaging.Messages;

/// <summary>
/// 圖片地圖訊息:一張大圖,上面劃出多塊可點擊的區域。
/// An imagemap message: one large image with several tappable areas drawn on it.
/// </summary>
/// <remarks>
/// <para>
/// 欄位對應 LINE 的 <c>baseUrl</c>、<c>altText</c>、<c>baseSize</c>、<c>video</c> 與 <c>actions</c>。
/// <c>baseUrl</c> 的規則值得先知道:LINE <b>不是</b>直接抓這個位址,而是依裝置抓
/// <c>{baseUrl}/{width}</c>(240、300、460、700、1040),所以那個位址底下要準備多個尺寸的檔案,
/// 而且結尾不能有斜線。
/// The fields map to LINE's <c>baseUrl</c>, <c>altText</c>, <c>baseSize</c>, <c>video</c> and
/// <c>actions</c>. The rule for <c>baseUrl</c> is worth knowing up front: LINE does <b>not</b> fetch that address
/// itself but <c>{baseUrl}/{width}</c> to suit the device (240, 300, 460, 700, 1040), so several sizes have to
/// sit under it, and it must not end with a slash.
/// </para>
/// <para>
/// <c>baseSize.width</c> 固定是 <see cref="LineMessagingLimits.ImagemapBaseWidth"/>,只有高度可自訂,
/// 因此建構子只收高度。區域數 1 到 <see cref="LineMessagingLimits.MaxImagemapActions"/> 在本地檢查。
/// <c>baseSize.width</c> is fixed at <see cref="LineMessagingLimits.ImagemapBaseWidth"/> and only the height
/// varies, so the constructor takes the height alone. The area count, one to
/// <see cref="LineMessagingLimits.MaxImagemapActions"/>, is checked locally.
/// </para>
/// </remarks>
public sealed class ImagemapMessage : LineMessage
{
    /// <summary>
    /// 建立圖片地圖訊息。
    /// Creates an imagemap message.
    /// </summary>
    /// <param name="baseUrl">底圖位址(不含尺寸、結尾無斜線)。The base image address, without size and trailing slash.</param>
    /// <param name="altText">替代文字。The alternative text.</param>
    /// <param name="baseHeight">底圖高度(寬度固定 1040)。The base image height; the width is fixed at 1040.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="baseUrl"/> 或 <paramref name="altText"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="baseUrl"/> or <paramref name="altText"/> is <see langword="null"/> or blank.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="baseHeight"/> 不是正數時擲出。Thrown when <paramref name="baseHeight"/> is not positive.
    /// </exception>
    public ImagemapMessage(string baseUrl, string altText, int baseHeight)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(altText);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baseHeight);

        BaseUrl = baseUrl;
        AltText = altText;
        BaseHeight = baseHeight;
    }

    /// <inheritdoc />
    public override string Type => "imagemap";

    /// <summary>
    /// 底圖位址。
    /// The base image address.
    /// </summary>
    public string BaseUrl { get; }

    /// <summary>
    /// 替代文字。
    /// The alternative text.
    /// </summary>
    public string AltText { get; }

    /// <summary>
    /// 底圖高度;寬度固定為 <see cref="LineMessagingLimits.ImagemapBaseWidth"/>。
    /// The base image height; the width is fixed at <see cref="LineMessagingLimits.ImagemapBaseWidth"/>.
    /// </summary>
    public int BaseHeight { get; }

    /// <summary>
    /// 疊在圖上的影片;不放時為 <see langword="null"/>。
    /// The video laid over the image, or <see langword="null"/> for none.
    /// </summary>
    public LineImagemapVideo? Video { get; set; }

    /// <summary>
    /// 可點擊的區域,1 到 <see cref="LineMessagingLimits.MaxImagemapActions"/> 塊。
    /// The tappable areas, between one and <see cref="LineMessagingLimits.MaxImagemapActions"/>.
    /// </summary>
    public IList<LineImagemapAction> Actions { get; } = [];

    /// <summary>
    /// 先檢查快速回覆,再檢查區域數。
    /// Checks the quick reply first, then the area count.
    /// </summary>
    /// <returns>
    /// 全部通過時為成功;區域數不合規時為 <see cref="LineErrorCodes.InvalidImagemap"/> 失敗。
    /// Success when everything passes; a <see cref="LineErrorCodes.InvalidImagemap"/> failure when the area count
    /// does not conform.
    /// </returns>
    public override Result Validate()
    {
        var quickReply = base.Validate();
        if (quickReply.IsFailure)
        {
            return quickReply;
        }

        return Actions.Count is >= 1 and <= LineMessagingLimits.MaxImagemapActions
            ? Result.Success()
            : Error.Validation(
                LineErrorCodes.InvalidImagemap,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"圖片地圖需帶 1 到 {LineMessagingLimits.MaxImagemapActions} 塊區域,這次是 {Actions.Count} 塊。An imagemap takes between 1 and {LineMessagingLimits.MaxImagemapActions} areas; {Actions.Count} were supplied."));
    }

    /// <inheritdoc />
    internal override void WriteBody(Utf8JsonWriter writer)
    {
        writer.WriteString("baseUrl", BaseUrl);
        writer.WriteString("altText", AltText);

        writer.WriteStartObject("baseSize");
        writer.WriteNumber("width", LineMessagingLimits.ImagemapBaseWidth);
        writer.WriteNumber("height", BaseHeight);
        writer.WriteEndObject();

        if (Video is { } video)
        {
            writer.WritePropertyName("video");
            video.WriteTo(writer);
        }

        writer.WriteStartArray("actions");
        for (var index = 0; index < Actions.Count; index++)
        {
            Actions[index].WriteTo(writer);
        }

        writer.WriteEndArray();
    }
}
