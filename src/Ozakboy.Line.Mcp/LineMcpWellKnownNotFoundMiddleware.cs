using Microsoft.AspNetCore.Http;

namespace Ozakboy.Line.Mcp;

/// <summary>
/// 讓 <c>/.well-known/</c> 底下一律回乾淨的 JSON 404。
/// Answers a clean JSON 404 for everything under <c>/.well-known/</c>.
/// </summary>
/// <remarks>
/// <para>
/// 本套件的 MCP 走「金鑰在路徑上」的無 OAuth 模式,但 MCP 連接器(例如 claude.ai)在連線前會先探測
/// <c>/.well-known/oauth-protected-resource</c> 之類的路徑。那些請求若落到 MVC 而拿到 HTML 登入頁或 302,
/// 連接器會判定這個站需要 OAuth 授權流程而<b>連不上</b>——而錯誤訊息只會說授權失敗,不會說是探測請求的問題。
/// 明確的 404 才是「這裡沒有 OAuth」的正確訊號。
/// This package's MCP uses the no-OAuth arrangement with the key in the path, but an MCP connector — claude.ai,
/// for one — probes paths like <c>/.well-known/oauth-protected-resource</c> before connecting. When those land in
/// MVC and come back as an HTML sign-in page or a 302, the connector decides the site needs an OAuth flow and
/// <b>cannot connect</b>, while the error says only that authorisation failed and nothing about a probe. A plain
/// 404 is the correct signal for "there is no OAuth here".
/// </para>
/// <para>
/// ⚠ 這道閘攔的是<b>整個 <c>/.well-known</c> 子樹</b>。日後要放 <c>security.txt</c>、
/// <c>assetlinks.json</c> 這類 well-known 資源的話,得在這個中介層<b>之前</b>把它們處理掉,
/// 否則會被無聲吃掉 —— 而檔案明明在磁碟上、網址卻回 404,是個很難聯想到中介層的症狀。
/// ⚠ This gate covers the <b>whole <c>/.well-known</c> subtree</b>. Serving a <c>security.txt</c>, an
/// <c>assetlinks.json</c>, or another well-known resource later means handling it <b>before</b> this middleware,
/// or it is swallowed silently — and a file that plainly exists on disk while its URL answers 404 is a symptom
/// nobody traces back to a middleware.
/// </para>
/// </remarks>
internal sealed class LineMcpWellKnownNotFoundMiddleware
{
    /// <summary>
    /// 要攔的路徑前綴。
    /// The path prefix being intercepted.
    /// </summary>
    private const string WellKnownPrefix = "/.well-known";

    private readonly RequestDelegate _next;

    /// <summary>
    /// 建立中介層。
    /// Creates the middleware.
    /// </summary>
    /// <param name="next">下一個中介層。The next middleware.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="next"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="next"/> is <see langword="null"/>.
    /// </exception>
    public LineMcpWellKnownNotFoundMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    /// <summary>
    /// 處理一個請求。
    /// Handles one request.
    /// </summary>
    /// <param name="context">請求內容。The HTTP context.</param>
    /// <returns>處理完成的工作。The task that completes when it is handled.</returns>
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Request.Path.StartsWithSegments(WellKnownPrefix)
            ? LineMcpKeyGateMiddleware.WriteNotFoundAsync(context)
            : _next(context);
    }
}
