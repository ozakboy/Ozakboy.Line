using System.Text.Json;

namespace Ozakboy.Line.Messaging.Messages;

/// <summary>
/// 原樣送出的訊息:整個 JSON 物件由呼叫端提供。
/// A message sent verbatim, whose entire JSON object comes from the caller.
/// </summary>
/// <remarks>
/// 這是本套件的逃生口。LINE 新增一種訊息、或某個欄位還沒建模時,不必等套件升版 —— 自己組 JSON 就送得出去。
/// This is the package's escape hatch. When LINE adds a message type, or a field is not modelled yet, there is no
/// need to wait for a package release: assemble the JSON and it goes out.
/// </remarks>
public sealed class RawMessage : LineMessage
{
    private readonly string _type;

    /// <summary>
    /// 以完整的訊息 JSON 物件建立。
    /// Creates one from a complete message JSON object.
    /// </summary>
    /// <param name="contents">
    /// 完整的訊息物件,必須是 JSON 物件且含 <c>type</c> 欄位。
    /// The complete message object, which must be a JSON object carrying a <c>type</c> field.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="contents"/> 不是 JSON 物件,或沒有字串型別的 <c>type</c> 欄位時擲出。
    /// Thrown when <paramref name="contents"/> is not a JSON object, or has no string <c>type</c> field.
    /// </exception>
    public RawMessage(JsonElement contents)
    {
        if (contents.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException(
                "原樣訊息必須是一個 JSON 物件。A raw message must be a JSON object.",
                nameof(contents));
        }

        if (!contents.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
        {
            throw new ArgumentException(
                "原樣訊息必須含有字串型別的 type 欄位。A raw message must carry a string type field.",
                nameof(contents));
        }

        Contents = contents.Clone();
        _type = type.GetString() ?? string.Empty;
    }

    /// <inheritdoc />
    public override string Type => _type;

    /// <summary>
    /// 完整的訊息物件。
    /// The complete message object.
    /// </summary>
    public JsonElement Contents { get; }

    /// <summary>
    /// 原樣寫出呼叫端給的物件。
    /// Writes the caller's object verbatim.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    /// <remarks>
    /// <see cref="LineMessage.QuickReply"/> 與 <see cref="LineMessage.Sender"/> 在這個型別上<b>不生效</b>:
    /// 呼叫端給的物件可能已經有 <c>quickReply</c> 或 <c>sender</c>,再寫一次會產生重複欄位。
    /// 要加這兩樣請直接寫進這個物件裡。
    /// <see cref="LineMessage.QuickReply"/> and <see cref="LineMessage.Sender"/> have <b>no effect</b> on this
    /// type: the caller's object may already carry a <c>quickReply</c> or a <c>sender</c>, and writing another
    /// would produce a duplicate field. Put them into the object itself.
    /// </remarks>
    internal override void WriteTo(Utf8JsonWriter writer) => Contents.WriteTo(writer);

    /// <inheritdoc />
    internal override void WriteBody(Utf8JsonWriter writer)
    {
        // WriteTo 已整份覆寫,這裡不會被呼叫到。
        // WriteTo replaces the whole write, so this is never reached.
    }
}
