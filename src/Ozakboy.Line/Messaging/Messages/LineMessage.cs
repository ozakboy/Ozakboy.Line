using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Messages;

/// <summary>
/// 一則要送給 LINE 的訊息。
/// One message to send to LINE.
/// </summary>
/// <remarks>
/// <para>
/// 這個型別只在本組件內可以被繼承(改寫輸出的成員是 internal)。LINE 的訊息種類會隨著平台演進,
/// 想送本套件還沒建模的新型訊息時請用 <see cref="RawMessage"/> —— 它原封不動送出你給的 JSON,
/// 不會因為套件還沒跟上而擋住任何東西。
/// This type can be derived from only inside this assembly, since the member that writes its JSON is internal.
/// LINE's message types keep growing, and a kind this package has not modelled yet goes out through
/// <see cref="RawMessage"/>, which sends the JSON given to it verbatim so a package that has not caught up
/// blocks nothing.
/// </para>
/// <para>
/// 輸出的 JSON 是<b>明確寫出來</b>的,不是靠反射把屬性名轉成 camelCase:LINE 的欄位名並不全等於
/// 屬性名(<c>originalContentUrl</c>、<c>packageId</c>、<c>altText</c>),而反射轉出來的名字錯了,
/// 症狀是 LINE 回一個只說「請求內容有 1 個錯誤」的 400。
/// The JSON is written out <b>explicitly</b> rather than reflected from property names into camelCase: LINE's
/// field names do not all match the property names (<c>originalContentUrl</c>, <c>packageId</c>,
/// <c>altText</c>), and a name that reflection gets wrong shows up as a 400 saying only that the request body
/// has one error.
/// </para>
/// </remarks>
[JsonConverter(typeof(LineMessageJsonConverter))]
public abstract class LineMessage
{
    /// <summary>
    /// 只允許本組件內繼承。
    /// Derivation is limited to this assembly.
    /// </summary>
    private protected LineMessage()
    {
    }

    /// <summary>
    /// LINE 規格裡的訊息型別字串,例如 <c>text</c>、<c>image</c>。
    /// The message type string from LINE's specification, such as <c>text</c> or <c>image</c>.
    /// </summary>
    public abstract string Type { get; }

    /// <summary>
    /// 快速回覆的原始 JSON;不需要時為 <see langword="null"/>。
    /// The quick reply as raw JSON, or <see langword="null"/> when not needed.
    /// </summary>
    /// <remarks>
    /// 快速回覆的結構深、變動快,而且多數呼叫端根本不用;以 <see cref="JsonElement"/> 原樣帶過去,
    /// 需要的人自己組,不需要的人不必為它付出一整組模型的維護成本。
    /// Quick replies are deeply structured, change often, and most callers never use them. Passing one through as
    /// a <see cref="JsonElement"/> lets whoever needs it build its JSON, without everyone else paying the
    /// maintenance cost of a full model for it.
    /// </remarks>
    public JsonElement? QuickReply { get; set; }

    /// <summary>
    /// 把整則訊息寫成一個 JSON 物件。
    /// Writes the whole message as one JSON object.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    internal virtual void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", Type);
        WriteBody(writer);

        if (QuickReply is { } quickReply)
        {
            writer.WritePropertyName("quickReply");
            quickReply.WriteTo(writer);
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// 寫出這個型別專屬的欄位(不含 <c>type</c> 與 <c>quickReply</c>)。
    /// Writes the fields specific to this type, excluding <c>type</c> and <c>quickReply</c>.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    internal abstract void WriteBody(Utf8JsonWriter writer);
}
