using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ozakboy.Line.Liff;

/// <summary>
/// LIFF 視窗的大小。
/// The size of the LIFF window.
/// </summary>
/// <remarks>
/// 在 JSON 裡是小寫字串(<c>compact</c>、<c>tall</c>、<c>full</c>)。
/// In JSON these are the lowercase strings <c>compact</c>, <c>tall</c> and <c>full</c>.
/// </remarks>
[JsonConverter(typeof(LineLiffViewTypeConverter))]
public enum LineLiffViewType
{
    /// <summary>
    /// 約螢幕高度的 50%。
    /// About 50% of the screen height.
    /// </summary>
    Compact,

    /// <summary>
    /// 約螢幕高度的 80%。
    /// About 80% of the screen height.
    /// </summary>
    Tall,

    /// <summary>
    /// 滿版。
    /// The whole screen.
    /// </summary>
    Full,
}

/// <summary>
/// <see cref="LineLiffViewType"/> 與 LINE 的小寫字串互轉。
/// Converts <see cref="LineLiffViewType"/> to and from LINE's lowercase strings.
/// </summary>
/// <remarks>
/// 手寫轉換器而不用 <see cref="JsonStringEnumConverter"/>:內建的轉換器寫出來是 <c>Compact</c>,而 LINE 要的是
/// <c>compact</c>;而且對沒見過的字串,這裡擲的 <see cref="JsonException"/> 會被
/// <c>LineHttp.Deserialize</c> 轉成 <c>line.api.invalid_response</c>。
/// Hand-written rather than <see cref="JsonStringEnumConverter"/>: the built-in one writes <c>Compact</c> where
/// LINE wants <c>compact</c>, and for a string it does not know this throws a <see cref="JsonException"/> that
/// <c>LineHttp.Deserialize</c> turns into <c>line.api.invalid_response</c>.
/// </remarks>
internal sealed class LineLiffViewTypeConverter : JsonConverter<LineLiffViewType>
{
    /// <summary>
    /// 把列舉值轉成 LINE 的字串。
    /// Maps an enum value to LINE's string.
    /// </summary>
    /// <param name="type">列舉值。The enum value.</param>
    /// <returns>小寫字串。The lowercase string.</returns>
    internal static string ToWire(LineLiffViewType type) => type switch
    {
        LineLiffViewType.Compact => "compact",
        LineLiffViewType.Tall => "tall",
        LineLiffViewType.Full => "full",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "不認得的 LIFF 視窗大小。Unknown LIFF view type."),
    };

    /// <inheritdoc />
    public override LineLiffViewType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() switch
        {
            "compact" => LineLiffViewType.Compact,
            "tall" => LineLiffViewType.Tall,
            "full" => LineLiffViewType.Full,
            _ => throw new JsonException("Unknown LIFF view type."),
        };

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, LineLiffViewType value, JsonSerializerOptions options) =>
        writer.WriteStringValue(ToWire(value));
}
