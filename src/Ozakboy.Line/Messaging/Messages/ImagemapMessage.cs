using System.Text.Json.Serialization;
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
[JsonConverter(typeof(LineMessageJsonConverterFactory))]
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

    /// <summary>
    /// 建立「單張圖」的圖片地圖:不必準備 240 / 300 / 460 / 700 / 1040 多種尺寸,任何寬度都拿到同一張圖。
    /// Creates a single-image imagemap: no need to prepare the 240 / 300 / 460 / 700 / 1040 sizes, every width
    /// gets the same image.
    /// </summary>
    /// <param name="imageUrl">圖片的 https 位址(要有 ≥ 1040 寬的原圖)。The image's https address, which must serve an original at least 1040 wide.</param>
    /// <param name="altText">替代文字。The alternative text.</param>
    /// <param name="height">底圖高度(寬度固定 1040)。The base image height; the width is fixed at 1040.</param>
    /// <param name="action">
    /// 點擊動作;它原本帶的範圍會被換成整張圖 <c>(0, 0, 1040, height)</c>(複製一份,不改動傳入的物件)。
    /// The tap action; the area it carries is replaced by the whole image, <c>(0, 0, 1040, height)</c>, on a
    /// copy, leaving the object passed in untouched.
    /// </param>
    /// <returns>
    /// 成功時為已通過 <see cref="Validate"/> 的訊息;位址不是 https、含 <c>#</c> 片段、超長,或
    /// <paramref name="altText"/> / <paramref name="height"/> / 動作不合規時為
    /// <see cref="LineErrorCodes.InvalidImagemap"/>(動作問題為 <see cref="LineErrorCodes.InvalidAction"/>)失敗。
    /// On success, a message that has passed <see cref="Validate"/>; a <see cref="LineErrorCodes.InvalidImagemap"/>
    /// failure (<see cref="LineErrorCodes.InvalidAction"/> for an action problem) when the address is not https,
    /// carries a <c>#</c> fragment, is too long, or <paramref name="altText"/>, <paramref name="height"/> or the
    /// action does not conform.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>這個技巧依賴兩個 LINE 官方文件並未保證的行為:</b>(1) LINE 會把 <c>/{width}</c> 接在
    /// <c>baseUrl</c> 之後去抓圖;(2) 你的圖片伺服器會忽略 query string。做法是在位址尾端補 <c>?</c>
    /// (已有 query 就補 <c>&amp;</c>),讓 <c>/1040</c> 變成 query string 的一部分,於是不論 LINE 要哪個寬度,
    /// 抓到的都是同一個網址。正式環境使用前,請先用 <c>ValidateMessagesAsync</c> 與實機各測一次;
    /// 寬度方面,伺服器仍需提供 ≥ 1040 寬的原圖(LINE 會自行縮小,但不會放大)。
    /// <b>This trick rests on two behaviours LINE's official documentation does not guarantee:</b> (1) LINE appends
    /// <c>/{width}</c> to <c>baseUrl</c> to fetch the image, and (2) your image server ignores the query string.
    /// A <c>?</c> is added to the end of the address (<c>&amp;</c> if a query is already there), so that
    /// <c>/1040</c> becomes part of the query string and every width LINE asks for resolves to the same URL.
    /// Before using it in production, test it once with <c>ValidateMessagesAsync</c> and once on a real device;
    /// as for width, the server must still serve an original at least 1040 wide (LINE scales down, not up).
    /// </para>
    /// <para>
    /// 位址含 <c>#</c> 片段時直接失敗:片段不會送到伺服器,補在它後面的 <c>?</c> 也就沒有作用。
    /// An address with a <c>#</c> fragment fails outright: the fragment never reaches the server, so a <c>?</c>
    /// added after it would do nothing.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> 為 <see langword="null"/>。<paramref name="action"/> is <see langword="null"/>.</exception>
    public static Result<ImagemapMessage> ForSingleImage(string imageUrl, string altText, int height, LineImagemapAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return Result.Failure<ImagemapMessage>(Error.Validation(
                LineErrorCodes.InvalidImagemap,
                "單圖圖片地圖的 imageUrl 不可為空。The imageUrl of a single-image imagemap must not be blank."));
        }

        if (string.IsNullOrWhiteSpace(altText))
        {
            return Result.Failure<ImagemapMessage>(Error.Validation(
                LineErrorCodes.InvalidImagemap,
                "單圖圖片地圖的 altText 不可為空。The altText of a single-image imagemap must not be blank."));
        }

        if (height <= 0)
        {
            return Result.Failure<ImagemapMessage>(Error.Validation(
                LineErrorCodes.InvalidImagemap,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"單圖圖片地圖的 height 必須是正數,這次是 {height}。The height of a single-image imagemap must be positive; it was {height}.")));
        }

        if (imageUrl.Contains('#', StringComparison.Ordinal))
        {
            return Result.Failure<ImagemapMessage>(Error.Validation(
                LineErrorCodes.InvalidImagemap,
                $"單圖圖片地圖的 imageUrl 不可含 # 片段(片段不會送到伺服器,補的 ? 會失效):{imageUrl}。The imageUrl of a single-image imagemap must not carry a # fragment (it never reaches the server, so the added ? would be useless): {imageUrl}."));
        }

        // 尾端已是 ? 或 & 就不再補:再補一個只會讓位址多出一段沒有意義的 query。
        // Nothing is added when it already ends in ? or &: another would only leave a meaningless extra query piece.
        var baseUrl = imageUrl.EndsWith('?') || imageUrl.EndsWith('&')
            ? imageUrl
            : imageUrl + (imageUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?');

        if (LineMessageValidation.CheckMediaUrl("單圖圖片地圖的 imageUrl", baseUrl) is { } problem)
        {
            return Result.Failure<ImagemapMessage>(Error.Validation(LineErrorCodes.InvalidImagemap, problem));
        }

        var message = new ImagemapMessage(baseUrl, altText, height);
        message.Actions.Add(action.WithArea(new LineImagemapArea(0, 0, LineMessagingLimits.ImagemapBaseWidth, height)));

        var validated = message.Validate();
        return validated.IsFailure
            ? Result.Failure<ImagemapMessage>(validated.Error!)
            : Result.Success(message);
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

        if (Actions.Count is < 1 or > LineMessagingLimits.MaxImagemapActions)
        {
            return Error.Validation(
                LineErrorCodes.InvalidImagemap,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"圖片地圖需帶 1 到 {LineMessagingLimits.MaxImagemapActions} 塊區域,這次是 {Actions.Count} 塊。An imagemap takes between 1 and {LineMessagingLimits.MaxImagemapActions} areas; {Actions.Count} were supplied."));
        }

        if (LineMessageValidation.IsTooLong(AltText, LineMessagingLimits.MaxAltTextLength))
        {
            return Error.Validation(
                LineErrorCodes.InvalidImagemap,
                LineMessageValidation.TooLong("圖片地圖的 altText", AltText, LineMessagingLimits.MaxAltTextLength));
        }

        for (var index = 0; index < Actions.Count; index++)
        {
            var validated = Actions[index].Validate(string.Create(CultureInfo.InvariantCulture, $"actions[{index}]"));
            if (validated.IsFailure)
            {
                return validated;
            }
        }

        return Result.Success();
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
