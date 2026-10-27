using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Messages.Flex;

/// <summary>
/// 原樣送出的 Flex 元件:整個 JSON 物件由呼叫端提供。
/// A Flex component sent verbatim, whose entire JSON object comes from the caller.
/// </summary>
/// <remarks>
/// 這是 Flex 模型的逃生口。LINE 新增元件、或某個欄位還沒建模時,不必等套件升版。
/// 從 JSON 讀回時,認不得的元件型別(含已廢止的 <c>spacer</c>),以及含有未建模欄位的元件,也會以這個型別保存。
/// This is the Flex model's escape hatch: when LINE adds a component or a field is not modelled yet, there is no need to
/// wait for a package release. Reading JSON back also keeps unknown component types (the deprecated <c>spacer</c>
/// included), and components carrying unmodelled fields, as this type.
/// </remarks>
[JsonConverter(typeof(FlexComponentJsonConverterFactory))]
public sealed class RawFlexComponent : FlexComponent
{
    private readonly string _type;

    /// <summary>
    /// 以完整的元件 JSON 物件建立。
    /// Creates one from a complete component JSON object.
    /// </summary>
    /// <param name="contents">
    /// 完整的元件物件,必須是 JSON 物件且含字串型別的 <c>type</c> 欄位。
    /// The complete component object, which must be a JSON object carrying a string <c>type</c> field.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="contents"/> 不是 JSON 物件,或沒有字串型別的 <c>type</c> 欄位時擲出。
    /// Thrown when <paramref name="contents"/> is not a JSON object, or has no string <c>type</c> field.
    /// </exception>
    public RawFlexComponent(JsonElement contents)
    {
        if (contents.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException(
                "原樣 Flex 元件必須是一個 JSON 物件。A raw Flex component must be a JSON object.",
                nameof(contents));
        }

        if (!contents.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
        {
            throw new ArgumentException(
                "原樣 Flex 元件必須含有字串型別的 type 欄位。A raw Flex component must carry a string type field.",
                nameof(contents));
        }

        // Clone 之後才保存:JsonElement 的生命週期綁在產生它的 JsonDocument 上。
        // Cloned before being kept: a JsonElement's lifetime is tied to the JsonDocument that produced it.
        Contents = contents.Clone();
        _type = type.GetString() ?? string.Empty;
    }

    /// <inheritdoc />
    public override string Type => _type;

    /// <summary>
    /// 完整的元件物件。
    /// The complete component object.
    /// </summary>
    public JsonElement Contents { get; }

    /// <inheritdoc />
    internal override void WriteTo(Utf8JsonWriter writer) => Contents.WriteTo(writer);
}
