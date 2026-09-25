using System.Text.Json;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// 改寫單一訊息顯示的傳送者:名稱與頭像。
/// The sender override shown on one message: a name and an icon.
/// </summary>
/// <remarks>
/// 對應 LINE 的 <c>sender</c> 物件,欄位為 <c>name</c>(最長 20 字元)與 <c>iconUrl</c>(HTTPS、PNG)。
/// 兩個都可以省略,但兩個都省略的物件不會被寫出 —— 空的 <c>sender</c> 對 LINE 沒有意義。
/// Maps to LINE's <c>sender</c> object, whose fields are <c>name</c> (up to 20 characters) and <c>iconUrl</c>
/// (HTTPS, PNG). Either may be left out, but an object with both left out is not written: an empty
/// <c>sender</c> means nothing to LINE.
/// </remarks>
public sealed class LineMessageSender
{
    /// <summary>
    /// 顯示的名稱;不改名稱時為 <see langword="null"/>。
    /// The name shown, or <see langword="null"/> to keep the account's own.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 顯示的頭像位址;不改頭像時為 <see langword="null"/>。
    /// The icon address shown, or <see langword="null"/> to keep the account's own.
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    /// 兩個欄位都沒有值。
    /// Neither field is set.
    /// </summary>
    internal bool IsEmpty => string.IsNullOrWhiteSpace(Name) && string.IsNullOrWhiteSpace(IconUrl);

    /// <summary>
    /// 寫出 <c>{ "name"?: …, "iconUrl"?: … }</c>。
    /// Writes <c>{ "name"?: …, "iconUrl"?: … }</c>.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    internal void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();

        if (!string.IsNullOrWhiteSpace(Name))
        {
            writer.WriteString("name", Name);
        }

        if (!string.IsNullOrWhiteSpace(IconUrl))
        {
            writer.WriteString("iconUrl", IconUrl);
        }

        writer.WriteEndObject();
    }
}
