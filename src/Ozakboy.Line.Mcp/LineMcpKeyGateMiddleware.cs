using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Ozakboy.Line.Mcp;

/// <summary>
/// MCP 的前置金鑰閘:整個 <c>{prefix}</c> 子樹都要先過金鑰才看得到。
/// The MCP key gate: nothing under <c>{prefix}</c> is visible until the key checks out.
/// </summary>
/// <remarks>
/// <para>
/// 對外掛載的形式是 <c>{prefix}/{金鑰}</c>,金鑰驗過之後路徑會被改寫成 <c>{prefix}</c> 再交給
/// <c>MapMcp</c>。金鑰走路徑而不是標頭,是因為多數 MCP 連接器只給使用者一個填網址的欄位,
/// 沒有地方填自訂標頭。
/// The public form is <c>{prefix}/{key}</c>, and once the key verifies the path is rewritten to <c>{prefix}</c>
/// and handed to <c>MapMcp</c>. The key travels in the path rather than a header because most MCP connectors give
/// the user one field for a URL and nowhere for a custom header.
/// </para>
/// <para>
/// 驗證失敗一律回 <b>404</b> 而不是 401 或 403。401/403 等於告訴對方「這裡確實有東西,只是你沒權限」,
/// 而這個端點的存在本身就是不必對外說的事;404 讓掃描的人分不出「沒有這個路徑」與「金鑰不對」。
/// A failure answers <b>404</b> rather than 401 or 403. A 401 or 403 tells the caller that something is here and
/// they lack access, and this endpoint's existence is not worth announcing; a 404 leaves a scanner unable to tell
/// "no such path" from "wrong key".
/// </para>
/// <para>
/// 金鑰比對用 <see cref="CryptographicOperations.FixedTimeEquals(ReadOnlySpan{byte}, ReadOnlySpan{byte})"/>,
/// 不是 <c>==</c>。字串比較會在第一個不同的字元就結束,而回應時間的差異足以讓人逐字元把金鑰試出來。
/// The key is compared with
/// <see cref="CryptographicOperations.FixedTimeEquals(ReadOnlySpan{byte}, ReadOnlySpan{byte})"/> rather than
/// <c>==</c>. String comparison stops at the first differing character, and the difference in response time is
/// enough to recover the key one character at a time.
/// </para>
/// </remarks>
internal sealed class LineMcpKeyGateMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptions<LineMcpOptions> _options;
    private readonly string _prefix;

    /// <summary>
    /// 建立中介層。
    /// Creates the middleware.
    /// </summary>
    /// <param name="next">下一個中介層。The next middleware.</param>
    /// <param name="options">MCP 設定。The MCP settings.</param>
    /// <param name="prefix">掛載前綴。The mount prefix.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="next"/> 或 <paramref name="options"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="next"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    public LineMcpKeyGateMiddleware(RequestDelegate next, IOptions<LineMcpOptions> options, string prefix)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        _next = next;
        _options = options;
        _prefix = prefix;
    }

    /// <summary>
    /// 處理一個請求。
    /// Handles one request.
    /// </summary>
    /// <param name="context">請求內容。The HTTP context.</param>
    /// <returns>處理完成的工作。The task that completes when it is handled.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Request.Path.StartsWithSegments(_prefix, out var rest))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var expected = _options.Value.ApiKey;

        // 沒設金鑰 = 這個功能沒有開,整個子樹當作不存在。
        // 反過來(沒設就不驗)的話,任何一次忘了帶環境變數的部署都會把這組工具開在網路上。
        // No key means the feature is off and the whole subtree does not exist. The other way round — no key, no
        // check — turns any deployment that forgets an environment variable into these tools, open to the world.
        if (string.IsNullOrWhiteSpace(expected) || !rest.HasValue)
        {
            await WriteNotFoundAsync(context).ConfigureAwait(false);
            return;
        }

        // rest 的形狀是 "/{金鑰}" 或 "/{金鑰}/其餘路徑"。
        // rest looks like "/{key}" or "/{key}/the rest".
        var raw = rest.Value!;
        var slash = raw.IndexOf('/', 1);
        var supplied = slash < 0 ? raw[1..] : raw[1..slash];
        var tail = slash < 0 ? string.Empty : raw[slash..];

        if (!FixedEquals(Uri.UnescapeDataString(supplied), expected))
        {
            await WriteNotFoundAsync(context).ConfigureAwait(false);
            return;
        }

        context.Request.Path = new PathString(_prefix + tail);
        await _next(context).ConfigureAwait(false);
    }

    /// <summary>
    /// 回一個乾淨的 JSON 404。
    /// Writes a clean JSON 404.
    /// </summary>
    /// <param name="context">請求內容。The HTTP context.</param>
    /// <returns>寫完的工作。The task that completes when it is written.</returns>
    /// <remarks>
    /// 自己寫回應而不是往下交給 MVC 或例外頁:那些通常回的是 HTML 錯誤頁,而一份錯誤頁會透露
    /// 站台用什麼框架、有哪些路由,甚至是登入頁的樣子。
    /// It writes the response itself rather than passing down to MVC or an exception page: those answer with
    /// HTML, and an error page gives away what framework the site runs, what routes it has, and sometimes what
    /// its sign-in page looks like.
    /// </remarks>
    internal static Task WriteNotFoundAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        context.Response.ContentType = "application/json; charset=utf-8";
        return context.Response.WriteAsync("{\"error\":\"not found\"}");
    }

    /// <summary>
    /// 定時字串比較。
    /// Compares two strings in fixed time.
    /// </summary>
    /// <param name="left">一邊。One side.</param>
    /// <param name="right">另一邊。The other.</param>
    /// <returns>相同時為 <see langword="true"/>。<see langword="true"/> when they match.</returns>
    /// <remarks>
    /// 長度不同時 <see cref="CryptographicOperations.FixedTimeEquals(ReadOnlySpan{byte}, ReadOnlySpan{byte})"/>
    /// 直接回 <see langword="false"/>,不會洩漏長度以外的資訊。
    /// With differing lengths,
    /// <see cref="CryptographicOperations.FixedTimeEquals(ReadOnlySpan{byte}, ReadOnlySpan{byte})"/> returns
    /// <see langword="false"/> outright and gives away nothing beyond the length.
    /// </remarks>
    private static bool FixedEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
}
