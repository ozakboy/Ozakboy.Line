using System.Text.Json.Serialization;
using System.Text.Json;
using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Messaging.Messages.Flex;

namespace Ozakboy.Line.Messaging.Messages;

/// <summary>
/// Flex Message:版面由一份 JSON 描述的自訂訊息。
/// A Flex Message, whose layout is described by a JSON document.
/// </summary>
/// <remarks>
/// <para>
/// 版面有兩種給法,擇一:<b>強型別</b>(<see cref="FlexMessage(string, FlexContainer)"/>,用
/// <see cref="FlexBubble"/> / <see cref="FlexCarousel"/> 與其元件組出來,<see cref="Container"/> 有值),
/// 或<b>原樣 JSON</b>(<see cref="FlexMessage(string, JsonElement)"/> / <see cref="FromJson"/>,<see cref="Contents"/> 有值)。
/// 後者是把 LINE Flex Message Simulator 產生的 JSON 原樣交進來,升級套件也不會讓既有版面失效;
/// <see cref="Parse"/> 則把 JSON 讀成強型別(認不得的節點以 Raw 型別原樣保留)。
/// The layout comes in one of two ways. <b>Typed</b> (<see cref="FlexMessage(string, FlexContainer)"/>): assembled from
/// <see cref="FlexBubble"/> / <see cref="FlexCarousel"/> and their components, with <see cref="Container"/> set. Or <b>verbatim
/// JSON</b> (<see cref="FlexMessage(string, JsonElement)"/> / <see cref="FromJson"/>): hand over the JSON from LINE's Flex
/// Message Simulator as it is, which a package upgrade never invalidates, with <see cref="Contents"/> set.
/// <see cref="Parse"/> reads JSON into the typed model, keeping nodes it does not recognise verbatim as Raw types.
/// </para>
/// </remarks>
[JsonConverter(typeof(LineMessageJsonConverterFactory))]
public sealed class FlexMessage : LineMessage
{
    /// <summary>
    /// 建立 Flex 訊息。
    /// Creates a Flex message.
    /// </summary>
    /// <param name="altText">
    /// 替代文字,顯示在通知與不支援 Flex 的環境。
    /// The alternative text, shown in notifications and where Flex is not supported.
    /// </param>
    /// <param name="contents">版面內容的 JSON。The layout contents as JSON.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="altText"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="altText"/> is <see langword="null"/> or blank.
    /// </exception>
    public FlexMessage(string altText, JsonElement contents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(altText);

        AltText = altText;

        // Clone 之後才保存:JsonElement 的生命週期綁在產生它的 JsonDocument 上,
        // 那份 document 一旦釋放,這裡留著的就是一個會擲出例外的空殼。
        // It is cloned before being kept: a JsonElement's lifetime is tied to the JsonDocument that produced it,
        // and once that document is disposed what remains here is a shell that throws.
        Contents = contents.Clone();
    }

    /// <summary>
    /// 以強型別容器建立 Flex 訊息。
    /// Creates a Flex message from a typed container.
    /// </summary>
    /// <param name="altText">
    /// 替代文字,顯示在通知與不支援 Flex 的環境。
    /// The alternative text, shown in notifications and where Flex is not supported.
    /// </param>
    /// <param name="contents">版面容器(氣泡或輪播)。The layout container (a bubble or a carousel).</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="altText"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="altText"/> is <see langword="null"/> or blank.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="contents"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="contents"/> is <see langword="null"/>.
    /// </exception>
    public FlexMessage(string altText, FlexContainer contents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(altText);
        ArgumentNullException.ThrowIfNull(contents);

        AltText = altText;
        Container = contents;
    }

    /// <inheritdoc />
    public override string Type => "flex";

    /// <summary>
    /// 替代文字。
    /// The alternative text.
    /// </summary>
    public string AltText { get; }

    /// <summary>
    /// 原樣 JSON 的版面內容;以強型別容器建立時為 <see cref="JsonValueKind.Undefined"/>(改看 <see cref="Container"/>)。
    /// The layout contents as verbatim JSON; <see cref="JsonValueKind.Undefined"/> when built from a typed container
    /// (look at <see cref="Container"/> instead).
    /// </summary>
    public JsonElement Contents { get; }

    /// <summary>
    /// 強型別的版面容器;以原樣 JSON 建立時為 <see langword="null"/>(改看 <see cref="Contents"/>)。
    /// The typed layout container; <see langword="null"/> when built from verbatim JSON (look at
    /// <see cref="Contents"/> instead).
    /// </summary>
    public FlexContainer? Container { get; }

    /// <summary>
    /// 以 JSON 字串建立 Flex 訊息。
    /// Creates a Flex message from a JSON string.
    /// </summary>
    /// <param name="altText">替代文字。The alternative text.</param>
    /// <param name="contentsJson">版面內容的 JSON 字串。The layout contents as a JSON string.</param>
    /// <returns>
    /// JSON 可解析時為訊息,否則為 <see cref="LineErrorCodes.InvalidJson"/> 失敗。
    /// The message when the JSON parses, otherwise a <see cref="LineErrorCodes.InvalidJson"/> failure.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="altText"/> 或 <paramref name="contentsJson"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="altText"/> or <paramref name="contentsJson"/> is <see langword="null"/> or
    /// blank.
    /// </exception>
    public static Result<FlexMessage> FromJson(string altText, string contentsJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(altText);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentsJson);

        try
        {
            using var document = JsonDocument.Parse(contentsJson);
            return Result.Success(new FlexMessage(altText, document.RootElement));
        }
        catch (JsonException exception)
        {
            var error = Error.Validation(
                LineErrorCodes.InvalidJson,
                "Flex 訊息的版面內容不是合法的 JSON。The Flex message contents are not valid JSON.");
            return error with { Exception = exception };
        }
    }

    /// <summary>
    /// 把版面 JSON 讀成強型別的 Flex 訊息。
    /// Reads layout JSON into a typed Flex message.
    /// </summary>
    /// <param name="altText">替代文字。The alternative text.</param>
    /// <param name="contentsJson">版面內容的 JSON 字串(<c>bubble</c> 或 <c>carousel</c>)。The layout contents as a JSON string (a <c>bubble</c> or a <c>carousel</c>).</param>
    /// <returns>
    /// 讀得成時為訊息,否則為 <see cref="LineErrorCodes.InvalidJson"/> 失敗。
    /// The message when it reads, otherwise a <see cref="LineErrorCodes.InvalidJson"/> failure.
    /// </returns>
    /// <remarks>
    /// 本套件沒建模的欄位、型別不認得的節點不會讓讀取失敗,也不會被丟掉:該節點以
    /// <see cref="RawFlexComponent"/> / <see cref="RawFlexContainer"/> 原樣保留,寫出時與輸入一致。
    /// 失敗的只有不合法的 JSON、根不是帶 <c>type</c> 的物件,或元件不是物件。
    /// A field this package does not model, or a node of an unknown type, neither fails the read nor is dropped: the node
    /// is kept verbatim as a <see cref="RawFlexComponent"/> / <see cref="RawFlexContainer"/> and written back as given.
    /// Only invalid JSON, a root that is not an object carrying <c>type</c>, or a component that is not an object fails.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="altText"/> 或 <paramref name="contentsJson"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="altText"/> or <paramref name="contentsJson"/> is <see langword="null"/> or
    /// blank.
    /// </exception>
    public static Result<FlexMessage> Parse(string altText, string contentsJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(altText);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentsJson);

        try
        {
            using var document = JsonDocument.Parse(contentsJson);
            return Result.Success(new FlexMessage(altText, FlexReader.ReadContainer(document.RootElement)));
        }
        catch (JsonException exception)
        {
            var error = Error.Validation(
                LineErrorCodes.InvalidJson,
                $"Flex 訊息的版面內容無法讀成 Flex 容器:{exception.Message} The Flex message contents cannot be read as a Flex container: {exception.Message}");
            return error with { Exception = exception };
        }
    }

    /// <summary>
    /// 送出前的本地檢查:替代文字不超過 400 字。
    /// The local check before sending: the alternative text is at most 400 characters.
    /// </summary>
    /// <returns>
    /// 通過時為成功,否則為 <see cref="LineErrorCodes.InvalidMessage"/> 失敗(快速回覆的問題照該處的代碼)。
    /// Success when it passes, otherwise an <see cref="LineErrorCodes.InvalidMessage"/> failure (quick reply problems keep their own codes).
    /// </returns>
    public override Result Validate()
    {
        var baseResult = base.Validate();
        if (baseResult.IsFailure)
        {
            return baseResult;
        }

        return LineMessageValidation.IsTooLong(AltText, LineMessagingLimits.MaxAltTextLength)
            ? LineMessageValidation.InvalidMessage(
                LineMessageValidation.TooLong("Flex 訊息的 altText", AltText, LineMessagingLimits.MaxAltTextLength))
            : Result.Success();
    }

    /// <inheritdoc />
    internal override void WriteBody(Utf8JsonWriter writer)
    {
        writer.WriteString("altText", AltText);
        writer.WritePropertyName("contents");

        if (Container is { } container)
        {
            container.WriteTo(writer);
        }
        else
        {
            Contents.WriteTo(writer);
        }
    }
}
