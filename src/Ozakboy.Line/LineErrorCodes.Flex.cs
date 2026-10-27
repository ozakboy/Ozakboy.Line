namespace Ozakboy.Line;

/// <summary>
/// 本套件回傳的錯誤代碼(Flex 訊息的本地驗證)。
/// The error codes this package returns — local validation of Flex messages.
/// </summary>
public static partial class LineErrorCodes
{
    /// <summary>
    /// Flex 訊息的版面不合規格:氣泡區塊全空、輪播氣泡數超出範圍、影片不在 hero、尺寸 / 顏色 / 間距格式不符、
    /// baseline 方塊放了不允許的元件、<c>text</c> 與 <c>contents</c> 沒有二選一、版面 JSON 超過大小上限等。
    /// 請求沒有送出;訊息會帶出版面路徑,例如 <c>contents[2].body.contents[0].url</c>。
    /// A Flex message's layout breaks the specification — a bubble with every area empty, a carousel with too many or
    /// too few bubbles, a video outside the hero, a malformed size, colour or spacing, a baseline box holding a
    /// component it may not, <c>text</c> and <c>contents</c> not exclusive, layout JSON over the size limit, and so on.
    /// No request was sent; the message carries the layout path, such as <c>contents[2].body.contents[0].url</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="Messaging.Messages.Flex.RawFlexContainer"/> 與 <see cref="Messaging.Messages.Flex.RawFlexComponent"/> 是逃生口,
    /// 內容不驗(只計入整體大小);Flex 內動作的問題仍是 <see cref="InvalidAction"/>,替代文字過長仍是 <see cref="InvalidMessage"/>。
    /// <see cref="Messaging.Messages.Flex.RawFlexContainer"/> and <see cref="Messaging.Messages.Flex.RawFlexComponent"/> are the escape
    /// hatch and their content is not checked (it only counts towards the overall size); a problem inside an action in a Flex
    /// layout is still <see cref="InvalidAction"/>, and an over-long alternative text is still <see cref="InvalidMessage"/>.
    /// </remarks>
    public const string InvalidFlex = "line.validation.invalid_flex";
}
