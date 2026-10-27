using System.Text.Json.Serialization;
using System.Text.Json;
using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Messaging.Messages;
using Ozakboy.Line.Postback;

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
[JsonConverter(typeof(LineActionJsonConverterFactory))]
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

    /// <summary>
    /// 把(可能超過 300 字的)資料存進暫存儲存體,用代碼建立回傳動作。
    /// Stages data that may exceed 300 characters in a store and creates a postback action carrying the token.
    /// </summary>
    /// <param name="store">暫存儲存體。The staging store.</param>
    /// <param name="label">動作標籤。The action label.</param>
    /// <param name="longData">要暫存的資料。The data to stage.</param>
    /// <param name="displayText">要不要在聊天室顯示一段文字。Text to show in the chat, or <see langword="null"/>.</param>
    /// <param name="timeToLive">存活時間;<see langword="null"/> 時用儲存體的預設值。The time to live, or <see langword="null"/> for the store's default.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>
    /// 成功時為 <see cref="Data"/> 是 <see cref="LinePostbackDataToken"/> 代碼的動作;儲存失敗(例如
    /// <see cref="LineErrorCodes.PostbackDataTooLong"/>)時原樣回傳那個失敗。
    /// On success, an action whose <see cref="Data"/> is a <see cref="LinePostbackDataToken"/> token; a storage
    /// failure, such as <see cref="LineErrorCodes.PostbackDataTooLong"/>, is returned as is.
    /// </returns>
    /// <remarks>
    /// <paramref name="longData"/> 即使不超過 300 字<b>也照存</b>:行為一致、可預測 —— 同一段程式碼不會因為
    /// 資料剛好變長一點就從「直接帶資料」變成「帶代碼」,webhook 端的處理(<see cref="LinePostbackDataResolver"/>)
    /// 也就不必猜。代價是短資料也會過期;不想要這個代價的就直接用建構子。
    /// <paramref name="longData"/> is staged <b>even when it is within 300 characters</b>: the behaviour stays
    /// consistent and predictable — the same code does not flip from "carry the data" to "carry a token" because
    /// the data grew slightly, so the webhook side (<see cref="LinePostbackDataResolver"/>) never has to guess. The
    /// price is that short data expires too; use the constructor to avoid it.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> 為 <see langword="null"/>。<paramref name="store"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="longData"/> 為 <see langword="null"/> 或空白。<paramref name="longData"/> is <see langword="null"/> or blank.</exception>
    public static async Task<Result<PostbackAction>> CreateStoredAsync(
        ILinePostbackDataStore store,
        string label,
        string longData,
        string? displayText = null,
        TimeSpan? timeToLive = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(longData);

        var token = await store.StoreAsync(longData, timeToLive, cancellationToken).ConfigureAwait(false);
        if (token.IsFailure)
        {
            return Result.Failure<PostbackAction>(token.Error!);
        }

        return Result.Success(new PostbackAction(token.GetValueOrThrow(), displayText) { Label = label });
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
    internal override Result ValidateBody(string location) =>
        LineMessageValidation.IsTooLong(Data, LineMessagingLimits.MaxPostbackDataLength)
            ? LineMessageValidation.InvalidAction(
                LineMessageValidation.TooLong($"{location}(type=postback)的 data", Data, LineMessagingLimits.MaxPostbackDataLength))
            : Result.Success();

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
