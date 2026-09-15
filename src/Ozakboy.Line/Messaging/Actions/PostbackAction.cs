using System.Text.Json;

namespace Ozakboy.Line.Messaging.Actions;

/// <summary>
/// 把一段資料回傳給 webhook 的動作。
/// An action that posts data back to the webhook.
/// </summary>
/// <remarks>
/// 資料由 <see cref="Ozakboy.Line.Webhook.LineWebhookPostback.Data"/> 收到。這段資料使用者看不到,
/// 但它會原樣走一趟 LINE 再回來 —— 不要把祕密或未簽章的權限資訊放進去。
/// The data arrives as <see cref="Ozakboy.Line.Webhook.LineWebhookPostback.Data"/>. The user does not see it,
/// but it makes a round trip through LINE verbatim, so it is no place for a secret or for unsigned authority.
/// </remarks>
public sealed class PostbackAction : LineAction
{
    /// <summary>
    /// 建立回傳資料的動作。
    /// Creates a postback action.
    /// </summary>
    /// <param name="data">要回傳的資料。The data to post back.</param>
    /// <param name="displayText">
    /// 要不要在聊天室顯示一段文字;不顯示時為 <see langword="null"/>。
    /// Text to show in the chat, or <see langword="null"/> to show none.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="data"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="data"/> is <see langword="null"/> or blank.
    /// </exception>
    public PostbackAction(string data, string? displayText = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(data);

        Data = data;
        DisplayText = displayText;
    }

    /// <inheritdoc />
    public override string Type => "postback";

    /// <summary>
    /// 要回傳的資料。
    /// The data to post back.
    /// </summary>
    public string Data { get; }

    /// <summary>
    /// 在聊天室顯示的文字。
    /// The text shown in the chat.
    /// </summary>
    public string? DisplayText { get; }

    /// <inheritdoc />
    internal override void WriteBody(Utf8JsonWriter writer)
    {
        writer.WriteString("data", Data);

        if (!string.IsNullOrWhiteSpace(DisplayText))
        {
            writer.WriteString("displayText", DisplayText);
        }
    }
}
