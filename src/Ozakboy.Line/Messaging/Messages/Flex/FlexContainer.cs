using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Messages.Flex;

/// <summary>
/// Flex 訊息最外層的容器:<see cref="FlexBubble"/> 或 <see cref="FlexCarousel"/>。
/// The outermost container of a Flex message: a <see cref="FlexBubble"/> or a <see cref="FlexCarousel"/>.
/// </summary>
/// <remarks>
/// <para>
/// 與 <see cref="LineMessage"/> 同理,只在本組件內可以繼承;本套件還沒建模的容器型別走 <see cref="RawFlexContainer"/>,
/// 它原封不動送出你給的 JSON。
/// As with <see cref="LineMessage"/>, derivation is limited to this assembly; a container type this package has not
/// modelled goes through <see cref="RawFlexContainer"/>, which sends the JSON given to it verbatim.
/// </para>
/// <para>
/// 輸出的 JSON 是逐欄位明確寫出的,不靠反射。從 JSON 讀回時,依 <c>type</c> 分派到具體型別;
/// 含有本套件沒建模的欄位,或型別不認得的節點,會原樣保存為 Raw 型別,所以讀回再寫出不會掉資料。
/// The JSON is written field by field, never reflected. Reading dispatches on <c>type</c> to the concrete type; a node
/// carrying a field this package does not model, or of an unknown type, is kept verbatim as a Raw type, so reading
/// and writing back loses nothing.
/// </para>
/// </remarks>
[JsonConverter(typeof(FlexContainerJsonConverter))]
public abstract class FlexContainer
{
    /// <summary>
    /// 只允許本組件內繼承。
    /// Derivation is limited to this assembly.
    /// </summary>
    private protected FlexContainer()
    {
    }

    /// <summary>
    /// LINE 規格裡的容器型別字串,<c>bubble</c> 或 <c>carousel</c>。
    /// The container type string from LINE's specification, <c>bubble</c> or <c>carousel</c>.
    /// </summary>
    public abstract string Type { get; }

    /// <summary>
    /// 列出版面裡以 Raw 型別原樣保存的節點路徑(例如 <c>contents[1].body.contents[0]</c>);容器自己是 Raw 時路徑為空字串。
    /// Lists the paths of the nodes kept verbatim as Raw types (such as <c>contents[1].body.contents[0]</c>); the path is
    /// empty when the container itself is Raw.
    /// </summary>
    /// <returns>
    /// 依文件順序的路徑;全是強型別時為空清單。這些節點不經 <see cref="FlexMessage.Validate"/> 檢查。
    /// The paths in document order, an empty list when everything is typed. These nodes are not checked by
    /// <see cref="FlexMessage.Validate"/>.
    /// </returns>
    /// <remarks>
    /// 讀回 JSON 時動作一律是 <see cref="Ozakboy.Line.Messaging.Actions.RawAction"/>,不算在這裡。
    /// Actions read from JSON are always <see cref="Ozakboy.Line.Messaging.Actions.RawAction"/> and are not counted here.
    /// </remarks>
    public IReadOnlyList<string> GetRawNodePaths() => FlexRawScanner.Scan(this);

    /// <summary>
    /// 把整個容器寫成一個 JSON 物件。
    /// Writes the whole container as one JSON object.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    internal abstract void WriteTo(Utf8JsonWriter writer);
}
