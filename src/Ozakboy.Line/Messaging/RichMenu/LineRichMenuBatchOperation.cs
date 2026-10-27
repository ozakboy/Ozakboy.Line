using System.Text.Json;

namespace Ozakboy.Line.Messaging.RichMenu;

/// <summary>
/// 圖文選單批次作業中的一個動作:替換連結、解除連結或全部解除。
/// One operation of a rich menu batch: replace a link, unlink, or unlink everything.
/// </summary>
/// <remarks>
/// <para>
/// 用 <see cref="Link"/>、<see cref="Unlink"/>、<see cref="UnlinkAll"/> 建立。這是封閉的型別階層(建構子是
/// internal):三種作業就是 LINE 目前支援的全部,開放繼承只會讓人寫出 LINE 不認得的 <c>type</c>。
/// Build one with <see cref="Link"/>, <see cref="Unlink"/> or <see cref="UnlinkAll"/>. The hierarchy is closed
/// (the constructor is internal): these three are everything LINE supports, and open inheritance would only let
/// someone write a <c>type</c> LINE does not recognise.
/// </para>
/// <para>
/// 批次作業的對象是「目前連結著某個選單的所有使用者」,而不是個別使用者 —— 個別使用者請用
/// <see cref="ILineMessagingClient.LinkRichMenuToUserAsync"/> 與批次連結。
/// A batch acts on "every user currently linked to a given menu", not on individual users; for individual users
/// use <see cref="ILineMessagingClient.LinkRichMenuToUserAsync"/> and the bulk link calls.
/// </para>
/// </remarks>
public abstract class LineRichMenuBatchOperation
{
    private protected LineRichMenuBatchOperation()
    {
    }

    /// <summary>
    /// LINE 的作業種類:<c>link</c>、<c>unlink</c> 或 <c>unlinkAll</c>。
    /// LINE's operation type: <c>link</c>, <c>unlink</c> or <c>unlinkAll</c>.
    /// </summary>
    public abstract string Type { get; }

    /// <summary>
    /// 把目前連結 <paramref name="fromRichMenuId"/> 的所有使用者,改連結到 <paramref name="toRichMenuId"/>。
    /// Re-links every user currently linked to <paramref name="fromRichMenuId"/> to <paramref name="toRichMenuId"/>.
    /// </summary>
    /// <param name="fromRichMenuId">原本的選單識別碼。The menu users are linked to now.</param>
    /// <param name="toRichMenuId">新的選單識別碼。The menu to link them to.</param>
    /// <returns>作業。The operation.</returns>
    /// <exception cref="ArgumentException">任一識別碼為空白時擲出。Thrown when either identifier is blank.</exception>
    public static LineRichMenuBatchOperation Link(string fromRichMenuId, string toRichMenuId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fromRichMenuId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toRichMenuId);

        return new LineRichMenuBatchLinkOperation(fromRichMenuId, toRichMenuId);
    }

    /// <summary>
    /// 解除所有使用者與 <paramref name="richMenuId"/> 的連結。
    /// Unlinks every user from <paramref name="richMenuId"/>.
    /// </summary>
    /// <param name="richMenuId">要解除的選單識別碼。The menu to unlink.</param>
    /// <returns>作業。The operation.</returns>
    /// <exception cref="ArgumentException">識別碼為空白時擲出。Thrown when the identifier is blank.</exception>
    public static LineRichMenuBatchOperation Unlink(string richMenuId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(richMenuId);

        return new LineRichMenuBatchUnlinkOperation(richMenuId);
    }

    /// <summary>
    /// 解除所有使用者的圖文選單連結。
    /// Unlinks every user from whatever rich menu they have.
    /// </summary>
    /// <returns>作業。The operation.</returns>
    public static LineRichMenuBatchOperation UnlinkAll() => new LineRichMenuBatchUnlinkAllOperation();

    /// <summary>
    /// 寫出這個作業的 JSON。
    /// Writes this operation as JSON.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    internal abstract void WriteTo(Utf8JsonWriter writer);
}

/// <summary>
/// 替換連結:<c>{"type":"link","from":…,"to":…}</c>。
/// Replace a link: <c>{"type":"link","from":…,"to":…}</c>.
/// </summary>
public sealed class LineRichMenuBatchLinkOperation : LineRichMenuBatchOperation
{
    internal LineRichMenuBatchLinkOperation(string from, string to)
    {
        From = from;
        To = to;
    }

    /// <inheritdoc />
    public override string Type => "link";

    /// <summary>
    /// 原本的選單識別碼。
    /// The menu users are linked to now.
    /// </summary>
    public string From { get; }

    /// <summary>
    /// 新的選單識別碼。
    /// The menu to link them to.
    /// </summary>
    public string To { get; }

    /// <inheritdoc />
    internal override void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", Type);
        writer.WriteString("from", From);
        writer.WriteString("to", To);
        writer.WriteEndObject();
    }
}

/// <summary>
/// 解除連結:<c>{"type":"unlink","from":…}</c>。
/// Unlink: <c>{"type":"unlink","from":…}</c>.
/// </summary>
public sealed class LineRichMenuBatchUnlinkOperation : LineRichMenuBatchOperation
{
    internal LineRichMenuBatchUnlinkOperation(string from) => From = from;

    /// <inheritdoc />
    public override string Type => "unlink";

    /// <summary>
    /// 要解除連結的選單識別碼。
    /// The menu to unlink.
    /// </summary>
    public string From { get; }

    /// <inheritdoc />
    internal override void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", Type);
        writer.WriteString("from", From);
        writer.WriteEndObject();
    }
}

/// <summary>
/// 全部解除:<c>{"type":"unlinkAll"}</c>。
/// Unlink everything: <c>{"type":"unlinkAll"}</c>.
/// </summary>
public sealed class LineRichMenuBatchUnlinkAllOperation : LineRichMenuBatchOperation
{
    internal LineRichMenuBatchUnlinkAllOperation()
    {
    }

    /// <inheritdoc />
    public override string Type => "unlinkAll";

    /// <inheritdoc />
    internal override void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", Type);
        writer.WriteEndObject();
    }
}
