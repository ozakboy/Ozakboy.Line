using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ozakboy.Line.AspNetCore.Tests.TestSupport;
using Ozakboy.Line.Login;

namespace Ozakboy.Line.AspNetCore.Tests;

/// <summary>
/// LINE Login 認證方案的整合測試,在記憶體內跑完整的 ASP.NET Core 管線。
/// Integration tests for the LINE Login scheme, running the full ASP.NET Core pipeline in memory.
/// </summary>
/// <remarks>
/// 挑戰網址與回呼流程沒辦法用單元測試驗到:state 的加密、關聯 cookie 的往返、PKCE 的 verifier 保存,
/// 全都發生在框架內部。真的起一個伺服器走一遍,才驗得到「實際送出的 302 位址」與「cookie 帶回來之後會怎樣」。
/// The challenge URL and the callback cannot be reached by unit tests: state encryption, the correlation cookie's
/// round trip, and where PKCE keeps its verifier all happen inside the framework. Only a real server exercises
/// the 302 that actually goes out and what happens when the cookie comes back.
/// </remarks>
[TestClass]
public sealed class LineLoginAuthenticationTests
{
    [TestMethod]
    public async Task Challenge_RedirectsToLineWithEveryParameter()
    {
        using var host = await CreateHostAsync(FakeBackchannelHandler.ForLogin(null), options =>
        {
            options.BotPrompt = LineBotPrompt.Aggressive;
            options.UiLocales = "zh-TW";
        });
        using var client = host.GetTestClient();

        using var response = await client.GetAsync(new Uri("/protected", UriKind.Relative));

        Assert.AreEqual(HttpStatusCode.Found, response.StatusCode);
        var location = response.Headers.Location!.AbsoluteUri;

        StringAssert.StartsWith(location, LineEndpoints.Authorization);
        StringAssert.Contains(location, "response_type=code");
        StringAssert.Contains(location, "client_id=test-channel-id");
        StringAssert.Contains(location, "scope=profile%20openid", "權限範圍以空白分隔,編碼後為 %20。");
        StringAssert.Contains(location, "code_challenge=", "PKCE 預設開啟。");
        StringAssert.Contains(location, "code_challenge_method=S256");
        StringAssert.Contains(location, "nonce=");
        StringAssert.Contains(location, "bot_prompt=aggressive");
        StringAssert.Contains(location, "ui_locales=zh-TW");
        Assert.IsFalse(location.Contains("prompt=consent", StringComparison.Ordinal), "沒有要求時不強制同意畫面。");
    }

    [TestMethod]
    public async Task Challenge_ForceConsent_AddsPromptParameter()
    {
        using var host = await CreateHostAsync(FakeBackchannelHandler.ForLogin(null), options => options.ForceConsent = true);
        using var client = host.GetTestClient();

        using var response = await client.GetAsync(new Uri("/protected", UriKind.Relative));

        StringAssert.Contains(response.Headers.Location!.AbsoluteUri, "prompt=consent");
    }

    [TestMethod]
    public async Task Challenge_BotPromptNone_OmitsTheParameter()
    {
        using var host = await CreateHostAsync(FakeBackchannelHandler.ForLogin(null));
        using var client = host.GetTestClient();

        using var response = await client.GetAsync(new Uri("/protected", UriKind.Relative));

        Assert.IsFalse(response.Headers.Location!.AbsoluteUri.Contains("bot_prompt", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Callback_HappyPath_SignsTheUserInWithEveryClaim()
    {
        var backchannel = FakeBackchannelHandler.ForLogin(null, friendFlag: true);
        using var host = await CreateHostAsync(backchannel);
        using var client = host.GetTestClient();

        var claims = await SignInAsync(
            client,
            backchannel,
            nonce => TestIdToken.Create(nonce: nonce, email: "user@example.com"));

        Assert.AreEqual("U1", claims[ClaimTypes.NameIdentifier]);
        Assert.AreEqual("U1", claims[LineClaimTypes.UserId]);
        Assert.AreEqual("阿明", claims[ClaimTypes.Name]);
        Assert.AreEqual("https://example.com/p.jpg", claims[LineClaimTypes.Picture]);
        Assert.AreEqual("今天也很好", claims[LineClaimTypes.StatusMessage]);
        Assert.AreEqual("true", claims[LineClaimTypes.IsFriend]);
        Assert.AreEqual("user@example.com", claims[ClaimTypes.Email], "id_token 驗過之後才補上電子郵件。");
    }

    [TestMethod]
    public async Task Callback_NotAFriend_SetsIsFriendToFalse()
    {
        var backchannel = FakeBackchannelHandler.ForLogin(null, friendFlag: false);
        using var host = await CreateHostAsync(backchannel);
        using var client = host.GetTestClient();

        var claims = await SignInAsync(client, backchannel, nonce => TestIdToken.Create(nonce: nonce));

        Assert.AreEqual("false", claims[LineClaimTypes.IsFriend]);
    }

    [TestMethod]
    public async Task Callback_FriendshipUnavailable_OmitsTheClaimButStillSignsIn()
    {
        // Login channel 沒有設定 Linked OA 時 LINE 回 4xx。這不該讓登入失敗,而「查不到」與「沒加好友」
        // 也不能混為一談 —— 混為一談的結果是加好友引導對已經是好友的人一直跳。
        var backchannel = FakeBackchannelHandler.ForLogin(null, friendshipStatusCode: HttpStatusCode.Forbidden);
        using var host = await CreateHostAsync(backchannel);
        using var client = host.GetTestClient();

        var claims = await SignInAsync(client, backchannel, nonce => TestIdToken.Create(nonce: nonce));

        Assert.AreEqual("U1", claims[ClaimTypes.NameIdentifier], "好友狀態查不到仍然登入成功。");
        Assert.IsFalse(claims.ContainsKey(LineClaimTypes.IsFriend), "查不到時不發這個宣告,而不是發一個 false。");
    }

    [TestMethod]
    public async Task Callback_QueryFriendshipDisabled_DoesNotCallTheEndpoint()
    {
        var backchannel = FakeBackchannelHandler.ForLogin(null);
        using var host = await CreateHostAsync(backchannel, options => options.QueryFriendship = false);
        using var client = host.GetTestClient();

        var claims = await SignInAsync(client, backchannel, nonce => TestIdToken.Create(nonce: nonce));

        Assert.IsFalse(claims.ContainsKey(LineClaimTypes.IsFriend));
        Assert.IsFalse(
            backchannel.Requests.Exists(uri => uri.Contains("/friendship/", StringComparison.Ordinal)),
            "關掉之後就不該多打這一次請求。");
    }

    [TestMethod]
    public async Task Callback_TamperedIdToken_FailsTheWholeAuthentication()
    {
        var backchannel = FakeBackchannelHandler.ForLogin(null);
        using var host = await CreateHostAsync(backchannel);
        using var client = host.GetTestClient();

        var response = await CallbackAsync(
            client,
            backchannel,
            nonce => TestIdToken.WithBrokenSignature(TestIdToken.Create(nonce: nonce)));

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode, "id_token 驗不過時絕不能簽發身分。");
        response.Dispose();
    }

    [TestMethod]
    public async Task Callback_IdTokenForAnotherChannel_FailsTheWholeAuthentication()
    {
        var backchannel = FakeBackchannelHandler.ForLogin(null);
        using var host = await CreateHostAsync(backchannel);
        using var client = host.GetTestClient();

        var response = await CallbackAsync(
            client,
            backchannel,
            nonce => TestIdToken.Create(nonce: nonce, audience: "some-other-channel"));

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        response.Dispose();
    }

    [TestMethod]
    public async Task Callback_ValidateIdTokenDisabled_AcceptsATamperedToken()
    {
        // 關掉驗證之後連被動過手腳的 token 都會過。這一條存在是為了讓「關掉的代價」寫在測試裡,
        // 而不是只寫在文件上。
        var tampered = TestIdToken.WithBrokenSignature(TestIdToken.Create(email: "user@example.com"));
        var backchannel = FakeBackchannelHandler.ForLogin(tampered);
        using var host = await CreateHostAsync(backchannel, options => options.ValidateIdToken = false);
        using var client = host.GetTestClient();

        var claims = await SignInAsync(client, backchannel);

        Assert.AreEqual("U1", claims[ClaimTypes.NameIdentifier]);
        Assert.IsFalse(claims.ContainsKey(ClaimTypes.Email), "沒有驗證就不會有電子郵件宣告。");
    }

    [TestMethod]
    public async Task Callback_NoIdToken_SignsInWithoutEmail()
    {
        var backchannel = FakeBackchannelHandler.ForLogin(null);
        using var host = await CreateHostAsync(backchannel);
        using var client = host.GetTestClient();

        var claims = await SignInAsync(client, backchannel);

        Assert.AreEqual("U1", claims[ClaimTypes.NameIdentifier]);
        Assert.IsFalse(claims.ContainsKey(ClaimTypes.Email));
    }

    /// <summary>
    /// 走完挑戰與回呼,回傳登入後的宣告。
    /// Runs the challenge and the callback, and returns the resulting claims.
    /// </summary>
    /// <param name="client">測試用戶端。The test client.</param>
    /// <param name="backchannel">假的後端通道。The fake backchannel.</param>
    /// <param name="idTokenForNonce">
    /// 拿到挑戰網址上的 nonce 之後,用它簽一個 id_token;不需要 id_token 時為 <see langword="null"/>。
    /// Signs an id_token once the challenge URL's nonce is known, or <see langword="null"/> when no id_token is
    /// wanted.
    /// </param>
    /// <returns>宣告型別對應值。The claims by type.</returns>
    private static async Task<IReadOnlyDictionary<string, string>> SignInAsync(
        HttpClient client,
        FakeBackchannelHandler backchannel,
        Func<string, string>? idTokenForNonce = null)
    {
        using var callback = await CallbackAsync(client, backchannel, idTokenForNonce);
        Assert.AreEqual(HttpStatusCode.Found, callback.StatusCode, "回呼成功時會導回原本要去的地方。");

        var authCookie = ReadCookies(callback)
            .FirstOrDefault(cookie => cookie.StartsWith(".AspNetCore.Cookies=", StringComparison.Ordinal));
        Assert.IsNotNull(authCookie, "登入成功必須簽發認證 cookie。");

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/claims", UriKind.Relative));
        request.Headers.Add("Cookie", authCookie);
        using var claimsResponse = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.OK, claimsResponse.StatusCode);
        var body = await claimsResponse.Content.ReadAsStringAsync();

        var claims = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf('\t', StringComparison.Ordinal);
            claims[line[..separator]] = line[(separator + 1)..];
        }

        return claims;
    }

    /// <summary>
    /// 發出挑戰、取出 state 與關聯 cookie,再打一次回呼。
    /// Issues the challenge, takes the state and correlation cookie, then calls back.
    /// </summary>
    /// <param name="client">測試用戶端。The test client.</param>
    /// <param name="backchannel">假的後端通道。The fake backchannel.</param>
    /// <param name="idTokenForNonce">以挑戰網址上的 nonce 簽 id_token 的委派。Signs an id_token from the challenge URL's nonce.</param>
    /// <returns>回呼的回應。The callback's response.</returns>
    private static async Task<HttpResponseMessage> CallbackAsync(
        HttpClient client,
        FakeBackchannelHandler backchannel,
        Func<string, string>? idTokenForNonce = null)
    {
        using var challenge = await client.GetAsync(new Uri("/protected", UriKind.Relative));
        var location = challenge.Headers.Location!;
        var query = System.Web.HttpUtility.ParseQueryString(location.Query);
        var state = query["state"]!;

        // id_token 裡的 nonce 必須等於挑戰時送出的那一個,而那個值是處理器當場產生的。
        // 真實世界裡是 LINE 把它原樣回填,這裡由測試代勞。
        if (idTokenForNonce is not null)
        {
            backchannel.IdToken = idTokenForNonce(query["nonce"]!);
        }

        var correlationCookie = ReadCookies(challenge)
            .First(cookie => cookie.StartsWith(".AspNetCore.Correlation.", StringComparison.Ordinal));

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri($"{LineLoginAuthenticationDefaults.CallbackPath}?code=code-1&state={Uri.EscapeDataString(state)}", UriKind.Relative));
        request.Headers.Add("Cookie", correlationCookie);

        return await client.SendAsync(request);
    }

    /// <summary>
    /// 取出回應裡的 cookie,只保留「名稱=值」的部分。
    /// Takes the cookies out of a response, keeping only the name and value.
    /// </summary>
    /// <param name="response">回應。The response.</param>
    /// <returns>cookie 字串。The cookie strings.</returns>
    private static IEnumerable<string> ReadCookies(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.Select(value => value.Split(';')[0])
            : [];

    /// <summary>
    /// 建立掛好 cookie 與 LINE Login 兩個認證方案的測試主機。
    /// Creates a test host with the cookie and LINE Login schemes wired up.
    /// </summary>
    /// <param name="backchannel">假的後端通道。The fake backchannel.</param>
    /// <param name="configure">額外的方案設定。Further scheme configuration.</param>
    /// <returns>已啟動的主機。The started host.</returns>
    private static async Task<IHost> CreateHostAsync(
        FakeBackchannelHandler backchannel,
        Action<LineLoginAuthenticationOptions>? configure = null)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(builder => builder
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                        .AddCookie()
                        .AddLineLogin(options =>
                        {
                            options.ClientId = TestIdToken.ChannelId;
                            options.ClientSecret = TestIdToken.ChannelSecret;
                            options.BackchannelHttpHandler = backchannel;

                            // 遠端認證失敗時,框架的預設行為是把例外往外丟。正式站一律會接住它導到自己的錯誤頁,
                            // 這裡也照做,測試才驗得到「回了什麼狀態碼」而不是「測試方法自己爆了」。
                            options.Events.OnRemoteFailure = context =>
                            {
                                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                                context.HandleResponse();
                                return Task.CompletedTask;
                            };

                            configure?.Invoke(options);
                        });
                    services.AddAuthorization();
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/protected", async context =>
                        {
                            if (context.User.Identity?.IsAuthenticated != true)
                            {
                                await context.ChallengeAsync(LineLoginAuthenticationDefaults.AuthenticationScheme);
                                return;
                            }

                            await context.Response.WriteAsync("ok");
                        });

                        endpoints.MapGet("/claims", async context =>
                        {
                            foreach (var claim in context.User.Claims)
                            {
                                await context.Response.WriteAsync($"{claim.Type}\t{claim.Value}\n");
                            }
                        });
                    });
                }))
            .Build();

        await host.StartAsync();
        return host;
    }
}
