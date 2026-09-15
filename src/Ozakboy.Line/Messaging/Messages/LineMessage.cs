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
    /// 掛在訊息下方的快速回覆按鈕列;不需要時為 <see langword="null"/>。
    /// The quick reply row shown under the message, or <see langword="null"/> when not needed.
    /// </summary>
    /// <remarks>
    /// 按鈕數上限由 <see cref="LineQuickReply.Validate"/> 把關,用戶端在送出之前會逐則訊息檢查一次;
    /// 超量的快速回覆在 LINE 那頭是整則訊息被退回,不是「只顯示前幾個」。
    /// The button limit is <see cref="LineQuickReply.Validate"/>'s business and the client checks every message
    /// before sending: an oversized quick reply has LINE reject the whole message rather than show the first few.
    /// </remarks>
    public LineQuickReply? QuickReply { get; set; }

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

        // 空的按鈕列不輸出。這種情形在用戶端的送出路徑上會先被 Validate 擋下,
        // 這裡的判斷是給「直接呼叫序列化」的路徑用的 —— 寫出一個空的 items 陣列會被 LINE 退回整則訊息。
        // An empty row is not written. The client's send path stops that at Validate first; this check is for
        // callers who serialise directly, since an empty items array has LINE reject the whole message.
        if (QuickReply is { Items.Count: > 0 } quickReply)
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
