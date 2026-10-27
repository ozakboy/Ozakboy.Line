using System.Globalization;

namespace Ozakboy.Line.Postback;

/// <summary>
/// 暫存 postback 資料用的代碼格式:<c>ozl:</c> 加 32 個十六進位字元,共 36 字。
/// The token format for staged postback data: <c>ozl:</c> plus 32 hex characters, 36 in all.
/// </summary>
/// <remarks>
/// 前綴 <c>ozl:</c> 是<b>契約</b>:webhook 端靠它分辨「這是代碼」還是「這是原樣的資料」,
/// 前綴一改,已經發出去、還躺在使用者聊天室裡的按鈕就全部認不得了。
/// The <c>ozl:</c> prefix is a <b>contract</b>: the webhook side tells "this is a token" from "this is the data
/// as sent" by it, and changing it would leave every button already sitting in users' chats unrecognised.
/// </remarks>
public static class LinePostbackDataToken
{
    /// <summary>
    /// 代碼前綴。
    /// The token prefix.
    /// </summary>
    public const string Prefix = "ozl:";

    private const int HexLength = 32;

    /// <summary>
    /// 產生一個新的代碼。
    /// Creates a new token.
    /// </summary>
    /// <returns><c>ozl:</c> 加 <c>Guid.NewGuid().ToString("N")</c>。<c>ozl:</c> followed by <c>Guid.NewGuid().ToString("N")</c>.</returns>
    public static string Create() => Prefix + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

    /// <summary>
    /// 判斷一段文字是不是代碼:前綴、總長 36、其後全是小寫十六進位。
    /// Tells whether a piece of text is a token: the prefix, a total length of 36, and lower-case hex after it.
    /// </summary>
    /// <param name="value">要檢查的文字。The text to check.</param>
    /// <returns>是代碼時為 <see langword="true"/>;<see langword="null"/> 或其他情況為 <see langword="false"/>。<see langword="true"/> for a token; <see langword="false"/> for <see langword="null"/> or anything else.</returns>
    public static bool IsToken(string? value)
    {
        if (value is null
            || value.Length != Prefix.Length + HexLength
            || !value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var character in value.AsSpan(Prefix.Length))
        {
            if (character is not ((>= '0' and <= '9') or (>= 'a' and <= 'f')))
            {
                return false;
            }
        }

        return true;
    }
}
