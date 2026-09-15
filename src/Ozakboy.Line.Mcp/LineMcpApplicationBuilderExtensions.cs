using Microsoft.AspNetCore.Builder;

namespace Ozakboy.Line.Mcp;

/// <summary>
/// 把 MCP 的前置閘掛進請求管線。
/// Puts the MCP gates into the request pipeline.
/// </summary>
public static class LineMcpApplicationBuilderExtensions
{
    /// <summary>
    /// MCP 端點的預設掛載前綴。
    /// The default mount prefix for the MCP endpoint.
    /// </summary>
    public const string DefaultPrefix = "/mcp/line";

    /// <summary>
    /// 掛上金鑰閘:<c>{prefix}</c> 底下的所有路徑都要先過金鑰。
    /// Mounts the key gate, so every path under <c>{prefix}</c> must pass the key check first.
    /// </summary>
    /// <param name="app">應用程式建構器。The application builder.</param>
    /// <param name="prefix">掛載前綴,預設 <see cref="DefaultPrefix"/>。The mount prefix; <see cref="DefaultPrefix"/> by default.</param>
    /// <returns>同一個建構器,方便串接。The same builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="app"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="app"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <b>必須在 <c>UseRouting</c> 之前呼叫。</b>路由一旦比對完成,這個中介層再改寫路徑也沒有用了 ——
    /// 端點早就選好了。放錯位置的症狀是「金鑰對不對都一樣,一律 404」,而那看起來像是金鑰設錯。
    /// <b>Call this before <c>UseRouting</c>.</b> Once routing has matched, rewriting the path here changes
    /// nothing: the endpoint has already been chosen. Placed wrongly, the symptom is a 404 whether the key is
    /// right or not, which looks exactly like a misconfigured key.
    /// </para>
    /// <para>
    /// 對外的網址是 <c>{prefix}/{ApiKey}</c>。金鑰輪替 = 改設定再重啟,舊金鑰在重啟的那一刻就失效。
    /// The public address is <c>{prefix}/{ApiKey}</c>. Rotating the key means changing the setting and
    /// restarting, and the old key stops working the moment it does.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// app.UseLineMcpWellKnownNotFound();
    /// app.UseLineMcpKeyGate();
    /// app.UseRouting();
    /// // …
    /// app.MapLineMcp();
    /// </code>
    /// </example>
    public static IApplicationBuilder UseLineMcpKeyGate(this IApplicationBuilder app, string prefix = DefaultPrefix)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        return app.UseMiddleware<LineMcpKeyGateMiddleware>(prefix);
    }

    /// <summary>
    /// 讓 <c>/.well-known/</c> 底下一律回乾淨的 JSON 404。
    /// Answers a clean JSON 404 for everything under <c>/.well-known/</c>.
    /// </summary>
    /// <param name="app">應用程式建構器。The application builder.</param>
    /// <returns>同一個建構器,方便串接。The same builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="app"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="app"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// ⚠ 這道閘攔的是<b>整個 <c>/.well-known</c> 子樹</b>,不只 OAuth 那幾個路徑。
    /// 站台若要提供 <c>security.txt</c> 這類 well-known 資源,請在這行<b>之前</b>處理掉它們。
    /// 理由與細節見 <see cref="LineMcpWellKnownNotFoundMiddleware"/>。
    /// ⚠ This gate covers the <b>whole <c>/.well-known</c> subtree</b>, not only the OAuth paths. A site serving
    /// a <c>security.txt</c> or another well-known resource must handle it <b>before</b> this line. The reasoning
    /// is in <see cref="LineMcpWellKnownNotFoundMiddleware"/>.
    /// </remarks>
    public static IApplicationBuilder UseLineMcpWellKnownNotFound(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<LineMcpWellKnownNotFoundMiddleware>();
    }
}
