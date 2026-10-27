using System.Globalization;
using Ozakboy.Core.Abstractions;

namespace Ozakboy.Line.Messaging.Messages;

/// <summary>
/// 訊息與動作共用的本地驗證小工具。
/// Small local-validation helpers shared by messages and actions.
/// </summary>
/// <remarks>
/// 所有失敗訊息都寫出「哪個欄位、實際值、上限」:LINE 自己對這類錯誤只回一個說「請求內容有 1 個錯誤」的 400,
/// 在本地失敗的唯一好處就是能把話說清楚。
/// Every failure message names the field, the actual value and the limit: LINE answers these mistakes with a
/// 400 that says only that the body has one error, so being specific is the whole point of failing locally.
/// </remarks>
internal static class LineMessageValidation
{
    /// <summary>
    /// 錯誤訊息裡引用的欄位值最多顯示幾個字元。
    /// How many characters of a field value an error message quotes at most.
    /// </summary>
    private const int QuoteLimit = 80;

    /// <summary>
    /// 產生 <see cref="LineErrorCodes.InvalidMessage"/> 失敗。
    /// Builds an <see cref="LineErrorCodes.InvalidMessage"/> failure.
    /// </summary>
    /// <param name="message">錯誤訊息。The message.</param>
    /// <returns>失敗。The failure.</returns>
    internal static Error InvalidMessage(string message) => Error.Validation(LineErrorCodes.InvalidMessage, message);

    /// <summary>
    /// 產生 <see cref="LineErrorCodes.InvalidAction"/> 失敗。
    /// Builds an <see cref="LineErrorCodes.InvalidAction"/> failure.
    /// </summary>
    /// <param name="message">錯誤訊息。The message.</param>
    /// <returns>失敗。The failure.</returns>
    internal static Error InvalidAction(string message) => Error.Validation(LineErrorCodes.InvalidAction, message);

    /// <summary>
    /// 檢查字串是否超過長度上限。
    /// Checks whether a string is longer than its limit.
    /// </summary>
    /// <param name="value">值;<see langword="null"/> 視為沒超過。The value; <see langword="null"/> is not over.</param>
    /// <param name="max">上限。The limit.</param>
    /// <returns>超過上限時為 <see langword="true"/>。<see langword="true"/> when over the limit.</returns>
    internal static bool IsTooLong(string? value, int max) => value is not null && value.Length > max;

    /// <summary>
    /// 產生「字串過長」的訊息。
    /// Builds the too-long message.
    /// </summary>
    /// <param name="field">欄位描述。The field description.</param>
    /// <param name="value">實際值。The actual value.</param>
    /// <param name="max">上限。The limit.</param>
    /// <returns>雙語訊息。The bilingual message.</returns>
    internal static string TooLong(string field, string value, int max) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{field} 長度 {value.Length} 超過上限 {max}(開頭:{Quote(value)})。{field} is {value.Length} characters long, over the limit of {max} (starts with: {Quote(value)}).");

    /// <summary>
    /// 檢查位址是否為 HTTPS。
    /// Checks that an address is HTTPS.
    /// </summary>
    /// <param name="value">位址。The address.</param>
    /// <returns>以 <c>https://</c> 開頭(不分大小寫)時為 <see langword="true"/>。<see langword="true"/> when it starts with <c>https://</c>, ignoring case.</returns>
    internal static bool IsHttps(string value) => value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 驗證一個媒體位址:必須是 HTTPS 而且不超過 <see cref="LineMessagingLimits.MaxMediaUrlLength"/>。
    /// Validates a media address: it must be HTTPS and no longer than <see cref="LineMessagingLimits.MaxMediaUrlLength"/>.
    /// </summary>
    /// <param name="field">欄位描述。The field description.</param>
    /// <param name="value">位址。The address.</param>
    /// <returns>通過時為 <see langword="null"/>,否則為錯誤訊息。<see langword="null"/> when it passes, otherwise the error message.</returns>
    internal static string? CheckMediaUrl(string field, string value)
    {
        if (!IsHttps(value))
        {
            return $"{field} 必須是 https:// 開頭的位址,實際是:{Quote(value)}。{field} must be an https:// address, but is: {Quote(value)}.";
        }

        return IsTooLong(value, LineMessagingLimits.MaxMediaUrlLength)
            ? TooLong(field, value, LineMessagingLimits.MaxMediaUrlLength)
            : null;
    }

    /// <summary>
    /// 驗證動作的開啟位址:協定必須是 https、http、line、tel 或 mailto,而且不超過
    /// <see cref="LineMessagingLimits.MaxActionUriLength"/>。
    /// Validates the address an action opens: the scheme must be https, http, line, tel or mailto, and the length
    /// no more than <see cref="LineMessagingLimits.MaxActionUriLength"/>.
    /// </summary>
    /// <param name="field">欄位描述。The field description.</param>
    /// <param name="value">位址。The address.</param>
    /// <returns>通過時為 <see langword="null"/>,否則為錯誤訊息。<see langword="null"/> when it passes, otherwise the error message.</returns>
    internal static string? CheckActionUri(string field, string value)
    {
        var allowed = value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("line://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);

        if (!allowed)
        {
            return $"{field} 的協定必須是 https://、http://、line://、tel: 或 mailto:,實際是:{Quote(value)}。The scheme of {field} must be https://, http://, line://, tel: or mailto:, but it is: {Quote(value)}.";
        }

        return IsTooLong(value, LineMessagingLimits.MaxActionUriLength)
            ? TooLong(field, value, LineMessagingLimits.MaxActionUriLength)
            : null;
    }

    /// <summary>
    /// 把值截短,放進錯誤訊息裡引用。
    /// Shortens a value for quoting in an error message.
    /// </summary>
    /// <param name="value">值。The value.</param>
    /// <returns>最多 80 個字元,超過的以刪節號結尾。At most 80 characters, with an ellipsis when cut.</returns>
    internal static string Quote(string value) =>
        value.Length <= QuoteLimit ? value : string.Concat(value.AsSpan(0, QuoteLimit), "…");
}
