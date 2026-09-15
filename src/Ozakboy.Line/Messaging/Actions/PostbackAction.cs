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
    /// <summary>收起圖文選單。Closes the rich menu.</summary>
    public const string InputOptionCloseRichMenu = "closeRichMenu";

    /// <summary>展開圖文選單。Opens the rich menu.</summary>
    public const string InputOptionOpenRichMenu = "openRichMenu";

    /// <summary>打開鍵盤,可搭配 <see cref="FillInText"/> 預填文字。Opens the keyboard, optionally pre-filled with <see cref="FillInText"/>.</summary>
    public const string InputOptionOpenKeyboard = "openKeyboard";

    /// <summary>打開語音輸入。Opens voice input.</summary>
    public const string InputOptionOpenVoice = "openVoice";

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

    /// <summary>
    /// 按下之後鍵盤與圖文選單要怎麼反應;不設定時維持原狀。
    /// What the keyboard and the rich menu do after the tap; without it nothing changes.
    /// </summary>
    /// <remarks>
    /// 值必須是 <see cref="InputOptionCloseRichMenu"/>、<see cref="InputOptionOpenRichMenu"/>、
    /// <see cref="InputOptionOpenKeyboard"/> 或 <see cref="InputOptionOpenVoice"/> 其中之一。
    /// The value must be one of <see cref="InputOptionCloseRichMenu"/>, <see cref="InputOptionOpenRichMenu"/>,
    /// <see cref="InputOptionOpenKeyboard"/>, or <see cref="InputOptionOpenVoice"/>.
    /// </remarks>
    public string? InputOption { get; set; }

    /// <summary>
    /// 鍵盤打開時預先填入的文字;只有 <see cref="InputOption"/> 為
    /// <see cref="InputOptionOpenKeyboard"/> 時 LINE 才看這個欄位。
    /// The text pre-filled into the keyboard. LINE reads this only when <see cref="InputOption"/> is
    /// <see cref="InputOptionOpenKeyboard"/>.
    /// </summary>
    public string? FillInText { get; set; }

    /// <inheritdoc />
    internal override void WriteBody(Utf8JsonWriter writer)
    {
        writer.WriteString("data", Data);

        if (!string.IsNullOrWhiteSpace(DisplayText))
        {
            writer.WriteString("displayText", DisplayText);
        }

        if (!string.IsNullOrWhiteSpace(InputOption))
        {
            writer.WriteString("inputOption", InputOption);
        }

        if (!string.IsNullOrWhiteSpace(FillInText))
        {
            writer.WriteString("fillInText", FillInText);
        }
    }
}
