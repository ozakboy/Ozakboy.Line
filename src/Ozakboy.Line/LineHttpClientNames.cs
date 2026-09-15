namespace Ozakboy.Line;

/// <summary>
/// 本套件註冊的具名 <see cref="HttpClient"/> 名稱。
/// The names of the <see cref="HttpClient"/> instances this package registers.
/// </summary>
/// <remarks>
/// Login 與 Messaging 用兩個不同的具名用戶端,而不是共用一個:兩邊的祕密不同,而
/// <c>Ozakboy.Http</c> 的遮罩器是<b>依用戶端名稱</b>註冊的 —— 共用一個用戶端,等於把兩組憑證
/// 放進同一份遮罩清單,也讓兩邊的限流與逾時再也分不開。
/// Login and Messaging use two separately named clients rather than sharing one: their secrets differ, and
/// <c>Ozakboy.Http</c> registers its masker <b>per client name</b>. Sharing one client would put both sets of
/// credentials on the same mask list, and would tie the two sides' rate limiting and timeouts together for good.
/// </remarks>
public static class LineHttpClientNames
{
    /// <summary>
    /// LINE Login 用的具名用戶端。
    /// The named client used for LINE Login.
    /// </summary>
    public const string Login = "Ozakboy.Line.Login";

    /// <summary>
    /// LINE Messaging API 用的具名用戶端。
    /// The named client used for the LINE Messaging API.
    /// </summary>
    public const string Messaging = "Ozakboy.Line.Messaging";
}
