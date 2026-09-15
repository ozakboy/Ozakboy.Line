namespace Ozakboy.Line.Mcp;

/// <summary>
/// 本套件註冊的具名 <see cref="HttpClient"/> 名稱。
/// The names of the <see cref="HttpClient"/> instances this package registers.
/// </summary>
public static class LineMcpHttpClientNames
{
    /// <summary>
    /// 抓取圖文選單圖片用的用戶端。
    /// The client used to fetch a rich menu image.
    /// </summary>
    /// <remarks>
    /// 獨立一個具名用戶端,是為了讓宿主能對它單獨設限 —— 逾時、重試、以及最重要的:
    /// 這個用戶端會去連<b>外部 AI 指定的位址</b>,是整個套件裡唯一一條目的地不由自己決定的請求。
    /// 需要限制它只能連某些網域(避免打到內網)的宿主,就在這個名稱上掛自己的 handler。
    /// It is a separate named client so a host can constrain it on its own — timeouts, retries, and above all
    /// this: it connects to <b>an address the outside AI chose</b>, the one request in the package whose
    /// destination is not its own. A host that needs it restricted to certain domains, to keep it off the
    /// internal network, attaches its handler to this name.
    /// </remarks>
    public const string ImageFetch = "Ozakboy.Line.Mcp.Fetch";
}
