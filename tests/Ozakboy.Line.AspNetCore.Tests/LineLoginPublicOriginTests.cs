using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ozakboy.Line.AspNetCore.Tests.TestSupport;

namespace Ozakboy.Line.AspNetCore.Tests;

/// <summary>
/// 明確指定對外網址時的回呼位址測試。
/// Tests for the callback address when the public address is named outright.
/// </summary>
/// <remarks>
/// 這件事在反向代理後面才會出事,而出事時的症狀是「使用者授權完回來就 400」,
/// LINE 的錯誤訊息一個字都不會說是哪裡不一樣。授權與換權杖兩個階段都要驗,
/// 因為只改其中一邊的結果是「授權成功但換權杖失敗」—— 比第一種更難查。
/// This only bites behind a reverse proxy, where the symptom is a 400 right after the user authorises and LINE's
/// error says nothing about what differs. Both the authorisation step and the token exchange are checked here,
/// because changing only one of them gives a sign-in that authorises and then fails at the exchange — harder to
/// track down than the first case.
/// </remarks>
[TestClass]
public sealed class LineLoginPublicOriginTests
{
    private const string PublicOrigin = "https://example.com";

    [TestMethod]
    public async Task Challenge_UsesThePublicOriginForRedirectUri()
    {
        using var host = await CreateHostAsync(FakeBackchannelHandler.ForLogin(null), PublicOrigin);
        using var client = host.GetTestClient();

        using var response = await client.GetAsync(new Uri("/protected", UriKind.Relative));

        var location = response.Headers.Location!.AbsoluteUri;
        var expected = Uri.EscapeDataString(PublicOrigin + LineLoginAuthenticationDefaults.CallbackPath);

        StringAssert.Contains(location, "redirect_uri=" + expected);
        Assert.IsFalse(location.Contains("localhost", StringComparison.OrdinalIgnoreCase), "設了公開網址就不從當前請求推導。");
    }

    [TestMethod]
    public async Task Challenge_TrailingSlashOnPublicOrigin_DoesNotDoubleTheSlash()
    {
        using var host = await CreateHostAsync(FakeBackchannelHandler.ForLogin(null), PublicOrigin + "/");
        using var client = host.GetTestClient();

        using var response = await client.GetAsync(new Uri("/protected", UriKind.Relative));

        // 多一條斜線就是另一個字串,而 LINE 是逐字比對的。
        StringAssert.Contains(
            response.Headers.Location!.AbsoluteUri,
            "redirect_uri=" + Uri.EscapeDataString(PublicOrigin + LineLoginAuthenticationDefaults.CallbackPath));
    }

    [TestMethod]
    public async Task TokenExchange_SendsTheSameRedirectUri()
    {
        var backchannel = FakeBackchannelHandler.ForLogin(null);
        using var host = await CreateHostAsync(backchannel, PublicOrigin);
        using var client = host.GetTestClient();

        using var challenge = await client.GetAsync(new Uri("/protected", UriKind.Relative));
        var query = System.Web.HttpUtility.ParseQueryString(challenge.Headers.Location!.Query);
        var state = query["state"]!;
        var correlationCookie = ReadCookies(challenge).First(cookie => cookie.StartsWith(".AspNetCore.Correlation.", StringComparison.Ordinal));

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri($"{LineLoginAuthenticationDefaults.CallbackPath}?code=code-1&state={Uri.EscapeDataString(state)}", UriKind.Relative));
        request.Headers.Add("Cookie", correlationCookie);
        using var callback = await client.SendAsync(request);

        var tokenIndex = backchannel.Requests.FindIndex(uri => uri.Contains("/oauth2/v2.1/token", StringComparison.Ordinal));
        Assert.IsTrue(tokenIndex >= 0, "回呼必須打過權杖端點。");

        var body = backchannel.Bodies[tokenIndex]!;
        StringAssert.Contains(
            body,
            "redirect_uri=" + Uri.EscapeDataString(PublicOrigin + LineLoginAuthenticationDefaults.CallbackPath),
            "OAuth 要求兩個階段的 redirect_uri 逐字相同,只改授權那一邊會在換權杖時失敗。");
    }

    [TestMethod]
    public async Task WithoutPublicOrigin_DerivesTheRedirectUriFromTheRequest()
    {
        using var host = await CreateHostAsync(FakeBackchannelHandler.ForLogin(null), publicOrigin: null);
        using var client = host.GetTestClient();

        using var response = await client.GetAsync(new Uri("/protected", UriKind.Relative));

        StringAssert.Contains(
            Uri.UnescapeDataString(response.Headers.Location!.AbsoluteUri),
            "redirect_uri=http://localhost",
            "沒設定時維持原本的行為。");
    }

    [TestMethod]
    public void Validate_RelativePublicOrigin_Throws()
    {
        // 一個寫錯的公開網址組出來的 redirect_uri 一樣會被 LINE 拒絕,而症狀與「沒設這個值」完全相同。
        var options = new LineLoginAuthenticationOptions
        {
            ClientId = TestIdToken.ChannelId,
            ClientSecret = TestIdToken.ChannelSecret,
            PublicOrigin = "/line",
        };

        var exception = Assert.ThrowsExactly<ArgumentException>(options.Validate);
        StringAssert.Contains(exception.Message, "PublicOrigin");
    }

    [TestMethod]
    public void Validate_NonHttpScheme_Throws()
    {
        var options = new LineLoginAuthenticationOptions
        {
            ClientId = TestIdToken.ChannelId,
            ClientSecret = TestIdToken.ChannelSecret,
            PublicOrigin = "ftp://example.com",
        };

        Assert.ThrowsExactly<ArgumentException>(options.Validate);
    }

    [TestMethod]
    public void Validate_AbsoluteHttpsOrigin_Passes()
    {
        var options = new LineLoginAuthenticationOptions
        {
            ClientId = TestIdToken.ChannelId,
            ClientSecret = TestIdToken.ChannelSecret,
            PublicOrigin = PublicOrigin,
        };

        options.Validate();
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
    /// 建立掛好兩個認證方案的測試主機。
    /// Creates a test host with both authentication schemes wired up.
    /// </summary>
    /// <param name="backchannel">假的後端通道。The fake backchannel.</param>
    /// <param name="publicOrigin">對外網址;不設定時為 <see langword="null"/>。The public address, or <see langword="null"/>.</param>
    /// <returns>已啟動的主機。The started host.</returns>
    private static async Task<IHost> CreateHostAsync(FakeBackchannelHandler backchannel, string? publicOrigin)
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
                            options.PublicOrigin = publicOrigin;
                            options.ValidateIdToken = false;

                            options.Events.OnRemoteFailure = context =>
                            {
                                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                                context.HandleResponse();
                                return Task.CompletedTask;
                            };
                        });
                    services.AddAuthorization();
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapGet("/protected", async context =>
                    {
                        if (context.User.Identity?.IsAuthenticated != true)
                        {
                            await context.ChallengeAsync(LineLoginAuthenticationDefaults.AuthenticationScheme);
                            return;
                        }

                        await context.Response.WriteAsync("ok");
                    }));
                }))
            .Build();

        await host.StartAsync();
        return host;
    }
}
