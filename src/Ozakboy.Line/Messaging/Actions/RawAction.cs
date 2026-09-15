using System.Text.Json;

namespace Ozakboy.Line.Messaging.Actions;

/// <summary>
/// 原樣送出的動作:整個 JSON 物件由呼叫端提供。
/// An action sent verbatim, whose entire JSON object comes from the caller.
/// </summary>
/// <remarks>
/// 讀回既有圖文選單時,LINE 回來的動作也一律以這個型別呈現:選單可能是別的工具建的,
/// 裡面的動作型別不保證在本套件建模範圍內,原樣保留才不會在讀取時掉資訊。
/// Reading an existing rich menu also yields this type for every action: the menu may have been created by
/// another tool, its action types are not guaranteed to be modelled here, and keeping them verbatim is what stops
/// a read from losing information.
/// </remarks>
public sealed class RawAction : LineAction
{
    private readonly string _type;

    /// <summary>
    /// 以完整的動作 JSON 物件建立。
    /// Creates one from a complete action JSON object.
    /// </summary>
    /// <param name="contents">
    /// 完整的動作物件,必須是 JSON 物件且含 <c>type</c> 欄位。
    /// The complete action object, which must be a JSON object carrying a <c>type</c> field.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="contents"/> 不是 JSON 物件,或沒有字串型別的 <c>type</c> 欄位時擲出。
    /// Thrown when <paramref name="contents"/> is not a JSON object, or has no string <c>type</c> field.
    /// </exception>
    public RawAction(JsonElement contents)
    {
        if (contents.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException(
                "原樣動作必須是一個 JSON 物件。A raw action must be a JSON object.",
                nameof(contents));
        }

        if (!contents.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
        {
            throw new ArgumentException(
                "原樣動作必須含有字串型別的 type 欄位。A raw action must carry a string type field.",
                nameof(contents));
        }

        Contents = contents.Clone();
        _type = type.GetString() ?? string.Empty;

        if (contents.TryGetProperty("label", out var label) && label.ValueKind == JsonValueKind.String)
        {
            Label = label.GetString();
        }
    }

    /// <inheritdoc />
    public override string Type => _type;

    /// <summary>
    /// 完整的動作物件。
    /// The complete action object.
    /// </summary>
    public JsonElement Contents { get; }

    /// <summary>
    /// 原樣寫出呼叫端給的物件。
    /// Writes the caller's object verbatim.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    internal override void WriteTo(Utf8JsonWriter writer) => Contents.WriteTo(writer);

    /// <inheritdoc />
    internal override void WriteBody(Utf8JsonWriter writer)
    {
        // WriteTo 已整份覆寫,這裡不會被呼叫到。
        // WriteTo replaces the whole write, so this is never reached.
    }
}
