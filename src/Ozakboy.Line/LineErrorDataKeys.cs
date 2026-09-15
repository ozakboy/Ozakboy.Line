namespace Ozakboy.Line;

/// <summary>
/// 本套件放進 <see cref="Ozakboy.Core.Abstractions.Error.Data"/> 的鍵名。
/// The keys this package writes into <see cref="Ozakboy.Core.Abstractions.Error.Data"/>.
/// </summary>
/// <remarks>
/// <c>Ozakboy.Http</c> 原有的鍵(<c>statusCode</c>、<c>body</c>、<c>retryAfterSeconds</c>)一併保留,
/// 見 <see cref="Ozakboy.Http.HttpErrorDataKeys"/>;這裡只列本套件額外加上的。
/// The keys <c>Ozakboy.Http</c> already writes — <c>statusCode</c>, <c>body</c>, and <c>retryAfterSeconds</c> —
/// are preserved as well; see <see cref="Ozakboy.Http.HttpErrorDataKeys"/>. Only this package's additions are
/// listed here.
/// </remarks>
public static class LineErrorDataKeys
{
    /// <summary>
    /// LINE 錯誤內容裡的 <c>message</c> 欄位。
    /// The <c>message</c> field of LINE's error body.
    /// </summary>
    public const string LineMessage = "lineMessage";

    /// <summary>
    /// LINE 錯誤內容裡的 <c>details</c> 陣列,串成 <c>屬性: 訊息; 屬性: 訊息</c> 的單行字串。
    /// LINE's <c>details</c> array, flattened into one line as <c>property: message; property: message</c>.
    /// </summary>
    /// <remarks>
    /// 串成一行而不是保留結構,是因為 <see cref="Ozakboy.Core.Abstractions.Error.Data"/> 是
    /// 字串對字串的字典 —— 錯誤資料的用途是「讓人看懂發生什麼事」,不是讓程式再解析一次。
    /// It is flattened rather than kept structured because
    /// <see cref="Ozakboy.Core.Abstractions.Error.Data"/> is a string-to-string dictionary: error data exists so
    /// a person can see what went wrong, not so a program can parse it again.
    /// </remarks>
    public const string LineDetails = "lineDetails";
}
