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

    /// <summary>
    /// LIFF server API 用的具名用戶端(用 LINE Login channel 的權杖,所以與 Messaging 分開)。
    /// The named client used for the LIFF server API, kept apart from Messaging because it carries the LINE Login
    /// channel's token.
    /// </summary>
    public const string Liff = "Ozakboy.Line.Liff";

    /// <summary>
    /// 某個 Messaging channel 的具名用戶端名稱;空字串(預設 channel)回 <see cref="Messaging"/>。
    /// The named client for one Messaging channel; the empty string (the default channel) yields
    /// <see cref="Messaging"/>.
    /// </summary>
    /// <param name="channelName">channel 名稱。The channel name.</param>
    /// <returns>具名用戶端名稱。The named client's name.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="channelName"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="channelName"/> is <see langword="null"/>.
    /// </exception>
    public static string ForMessaging(string channelName)
    {
        ArgumentNullException.ThrowIfNull(channelName);
        return channelName.Length == 0 ? Messaging : Messaging + ":" + channelName;
    }

    /// <summary>
    /// 某個 LIFF channel 的具名用戶端名稱;空字串(預設 channel)回 <see cref="Liff"/>。
    /// The named client for one LIFF channel; the empty string (the default channel) yields <see cref="Liff"/>.
    /// </summary>
    /// <param name="channelName">channel 名稱。The channel name.</param>
    /// <returns>具名用戶端名稱。The named client's name.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="channelName"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="channelName"/> is <see langword="null"/>.
    /// </exception>
    public static string ForLiff(string channelName)
    {
        ArgumentNullException.ThrowIfNull(channelName);
        return channelName.Length == 0 ? Liff : Liff + ":" + channelName;
    }
}
