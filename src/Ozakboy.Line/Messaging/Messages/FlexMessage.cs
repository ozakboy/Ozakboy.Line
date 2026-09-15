using System.Text.Json;
using Ozakboy.Core.Abstractions;

namespace Ozakboy.Line.Messaging.Messages;

/// <summary>
/// Flex Message:版面由一份 JSON 描述的自訂訊息。
/// A Flex Message, whose layout is described by a JSON document.
/// </summary>
/// <remarks>
/// Flex 的版面結構深、欄位多,而且 LINE 提供了 Flex Message Simulator 讓人用拖的把版面做好再複製 JSON。
/// 本套件因此不為它建一整組模型:把模擬器產生的 JSON 原樣交進來即可,升級套件也不會讓既有版面失效。
/// Flex layouts are deeply nested with many fields, and LINE provides a Flex Message Simulator where a layout is
/// assembled by hand and its JSON copied out. This package therefore models none of it: hand the simulator's JSON
/// over as it is, and upgrading the package never invalidates an existing layout.
/// </remarks>
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

    /// <inheritdoc />
    public override string Type => "flex";

    /// <summary>
    /// 替代文字。
    /// The alternative text.
    /// </summary>
    public string AltText { get; }

    /// <summary>
    /// 版面內容。
    /// The layout contents.
    /// </summary>
    public JsonElement Contents { get; }

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

    /// <inheritdoc />
    internal override void WriteBody(Utf8JsonWriter writer)
    {
        writer.WriteString("altText", AltText);
        writer.WritePropertyName("contents");
        Contents.WriteTo(writer);
    }
}
