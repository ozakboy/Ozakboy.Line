using System.Text.Json;

namespace Ozakboy.Line.Messaging.Narrowcast;

/// <summary>
/// 組出分眾推播 <c>recipient</c> 物件的輔助方法。
/// Helpers that build the <c>recipient</c> object of a narrowcast.
/// </summary>
/// <remarks>
/// LINE 的 <c>recipient</c> 是一棵運算樹:葉節點是 <c>{"type":"audience","audienceGroupId":…}</c> 或
/// <c>{"type":"redelivery","requestId":…}</c>,節點是 <c>{"type":"operator","and"|"or"|"not":…}</c>。
/// 這裡只組形狀,不驗證語意 —— 受眾存不存在、狀態是不是 READY,由 LINE 在送出時判定。
/// LINE's <c>recipient</c> is an operator tree: leaves are <c>{"type":"audience","audienceGroupId":…}</c> or
/// <c>{"type":"redelivery","requestId":…}</c>, nodes are <c>{"type":"operator","and"|"or"|"not":…}</c>. Only
/// the shape is built here, never the meaning: whether an audience exists or is READY is LINE's call at send
/// time.
/// </remarks>
public static class LineNarrowcastRecipient
{
    /// <summary>
    /// 某一個受眾的成員。
    /// The members of one audience.
    /// </summary>
    /// <param name="audienceGroupId">受眾識別碼。The audience identifier.</param>
    /// <returns><c>{"type":"audience","audienceGroupId":…}</c>。</returns>
    public static JsonElement Audience(long audienceGroupId) =>
        Build(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "audience");
            writer.WriteNumber("audienceGroupId", audienceGroupId);
            writer.WriteEndObject();
        });

    /// <summary>
    /// 先前某一次分眾推播的收件者(以它的請求識別碼指定)。
    /// The recipients of an earlier narrowcast, named by its request id.
    /// </summary>
    /// <param name="requestId">先前那次的請求識別碼。The earlier request's id.</param>
    /// <returns><c>{"type":"redelivery","requestId":…}</c>。</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="requestId"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="requestId"/> is <see langword="null"/> or blank.
    /// </exception>
    public static JsonElement Redelivery(string requestId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);

        return Build(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "redelivery");
            writer.WriteString("requestId", requestId);
            writer.WriteEndObject();
        });
    }

    /// <summary>
    /// 交集:同時屬於每一個運算元。
    /// The intersection: in every operand.
    /// </summary>
    /// <param name="operands">運算元,至少一個。The operands, at least one.</param>
    /// <returns><c>{"type":"operator","and":[…]}</c>。</returns>
    public static JsonElement And(params JsonElement[] operands) => Operator("and", operands);

    /// <summary>
    /// 聯集:屬於任一運算元。
    /// The union: in any operand.
    /// </summary>
    /// <param name="operands">運算元,至少一個。The operands, at least one.</param>
    /// <returns><c>{"type":"operator","or":[…]}</c>。</returns>
    public static JsonElement Or(params JsonElement[] operands) => Operator("or", operands);

    /// <summary>
    /// 補集:不屬於這個運算元。
    /// The complement: not in this operand.
    /// </summary>
    /// <param name="operand">運算元。The operand.</param>
    /// <returns><c>{"type":"operator","not":{…}}</c>。</returns>
    public static JsonElement Not(JsonElement operand) =>
        Build(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "operator");
            writer.WritePropertyName("not");
            operand.WriteTo(writer);
            writer.WriteEndObject();
        });

    /// <summary>
    /// 組出 <c>and</c> / <c>or</c> 節點。
    /// Builds an <c>and</c> / <c>or</c> node.
    /// </summary>
    /// <param name="name">運算子名稱。The operator name.</param>
    /// <param name="operands">運算元。The operands.</param>
    /// <returns>節點。The node.</returns>
    private static JsonElement Operator(string name, JsonElement[] operands)
    {
        ArgumentNullException.ThrowIfNull(operands);
        if (operands.Length == 0)
        {
            throw new ArgumentException(
                "運算子至少需要一個運算元。An operator needs at least one operand.",
                nameof(operands));
        }

        return Build(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "operator");
            writer.WriteStartArray(name);
            foreach (var operand in operands)
            {
                operand.WriteTo(writer);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });
    }

    /// <summary>
    /// 寫出一份 JSON 並以獨立的 <see cref="JsonElement"/> 回傳。
    /// Writes a JSON value and returns it as a standalone <see cref="JsonElement"/>.
    /// </summary>
    /// <param name="write">寫出內容的委派。The delegate writing the value.</param>
    /// <returns>Clone 過、不綁任何文件的元素。A cloned element tied to no document.</returns>
    private static JsonElement Build(Action<Utf8JsonWriter> write)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            write(writer);
        }

        using var document = JsonDocument.Parse(buffer.ToArray());
        return document.RootElement.Clone();
    }
}
