using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ozakboy.Line.Mcp.Tests;

/// <summary>
/// 金鑰閘與 <c>/.well-known</c> 閘的整合測試。
/// Integration tests for the key gate and the <c>/.well-known</c> gate.
/// </summary>
/// <remarks>
/// 這裡不掛真的 MCP 端點,而是在同一個位置掛一個回 200 的假端點。要驗的是<b>閘本身</b> ——
/// 錯金鑰擋不擋得住、對金鑰之後路徑被改寫成什麼;MCP 協定本身是 SDK 的事。
/// No real MCP endpoint is mapped here; a stand-in answering 200 sits at the same place. What needs verifying is
/// the <b>gate</b>: whether a wrong key is stopped, and what path a right key rewrites to. The MCP protocol
/// itself is the SDK's business.
/// </remarks>
[TestClass]
public sealed class LineMcpKeyGateTests
{
    private const string Prefix = "/mcp/line";
    private const string ApiKey = "test-mcp-api-key-0123456789";

    [TestMethod]
    public async Task CorrectKey_RewritesThePathAndReachesTheEndpoint()
    {
        using var host = await CreateHostAsync(ApiKey);
        using var client = host.GetTestClient();

        using var response = await client.GetAsync(new Uri($"{Prefix}/{ApiKey}", UriKind.Relative));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(Prefix, await response.Content.ReadAsStringAsync(), "金鑰段被剝掉之後,端點看到的是不含金鑰的路徑。");
    }

    [TestMethod]
    public async Task CorrectKeyWithTail_KeepsTheTail()
    {
        using var host = await CreateHostAsync(ApiKey);
        using var client = host.GetTestClient();

        using var response = await client.GetAsync(new Uri($"{Prefix}/{ApiKey}/sub", UriKind.Relative));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual($"{Prefix}/sub", await response.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task WrongKey_Answers404()
    {
        // 401/403 等於告訴對方「這裡確實有東西,只是你沒權限」,而這個端點的存在本身就不必對外說。
        using var host = await CreateHostAsync(ApiKey);
        using var client = host.GetTestClient();

        using var response = await client.GetAsync(new Uri($"{Prefix}/wrong-key", UriKind.Relative));

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.AreEqual("application/json; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        Assert.AreEqual("""{"error":"not found"}""", await response.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task NoKeySegment_Answers404()
    {
        using var host = await CreateHostAsync(ApiKey);
        using var client = host.GetTestClient();

        using var response = await client.GetAsync(new Uri(Prefix, UriKind.Relative));

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task KeyNotConfigured_TheWholeSubtreeIsGone()
    {
        // 「沒設定就不驗」的話,任何一次忘了帶環境變數的部署都會把這組工具開在網路上。
        using var host = await CreateHostAsync(apiKey: null);
        using var client = host.GetTestClient();

        using var withKey = await client.GetAsync(new Uri($"{Prefix}/{ApiKey}", UriKind.Relative));
        using var withoutKey = await client.GetAsync(new Uri(Prefix, UriKind.Relative));

        Assert.AreEqual(HttpStatusCode.NotFound, withKey.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, withoutKey.StatusCode);
    }

    [TestMethod]
    public async Task PathsOutsideThePrefix_AreUntouched()
    {
        using var host = await CreateHostAsync(ApiKey);
        using var client = host.GetTestClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("healthy", await response.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task WellKnown_AnswersCleanJson404()
    {
        // MCP 連接器連線前會探測 /.well-known/oauth-protected-resource。那些請求若拿到 HTML 登入頁或 302,
        // 連接器會判定這個站需要 OAuth 而連不上,而錯誤訊息只會說授權失敗。
        using var host = await CreateHostAsync(ApiKey);
        using var client = host.GetTestClient();

        using var response = await client.GetAsync(new Uri("/.well-known/oauth-protected-resource", UriKind.Relative));

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.AreEqual("""{"error":"not found"}""", await response.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task WellKnown_CoversTheWholeSubtree()
    {
        // 這是文件裡標了警告的行為:日後要放 security.txt 得在這道閘之前處理掉。
        using var host = await CreateHostAsync(ApiKey);
        using var client = host.GetTestClient();

        using var response = await client.GetAsync(new Uri("/.well-known/security.txt", UriKind.Relative));

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// 建立掛好兩道閘的測試主機。
    /// Creates a test host with both gates mounted.
    /// </summary>
    /// <param name="apiKey">金鑰;為 <see langword="null"/> 代表沒設定。The key, or <see langword="null"/> for unset.</param>
    /// <returns>已啟動的主機。The started host.</returns>
    private static async Task<IHost> CreateHostAsync(string? apiKey)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(builder => builder
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddLogging();
                    services.AddLineMessaging(options => options.ChannelAccessToken = "test-channel-access-token");
                    services.AddLineMcp(options => options.ApiKey = apiKey);
                })
                .Configure(app =>
                {
                    // 兩道閘都必須在 UseRouting 之前:路由一旦比對完成,改寫路徑就沒有用了。
                    app.UseLineMcpWellKnownNotFound();
                    app.UseLineMcpKeyGate(Prefix);
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        // 代替真正的 MCP 端點:回寫自己收到的路徑,好驗證金鑰段確實被剝掉了。
                        endpoints.MapGet(Prefix, context => context.Response.WriteAsync(context.Request.Path.Value!));
                        endpoints.MapGet($"{Prefix}/sub", context => context.Response.WriteAsync(context.Request.Path.Value!));
                        endpoints.MapGet("/health", context => context.Response.WriteAsync("healthy"));
                    });
                }))
            .Build();

        await host.StartAsync();
        return host;
    }
}
