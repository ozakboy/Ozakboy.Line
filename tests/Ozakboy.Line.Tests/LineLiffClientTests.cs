using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ozakboy.Core.Abstractions;
using Ozakboy.Http;
using Ozakboy.Line.Liff;
using Ozakboy.Line.Tests.TestSupport;

namespace Ozakboy.Line.Tests;

/// <summary>
/// LIFF server API 用戶端的測試。全部離線。
/// Tests for the LIFF server API client. Entirely offline.
/// </summary>
[TestClass]
public sealed class LineLiffClientTests
{
    private const string AccessToken = "test-channel-access-token";

    private static LineLiffApp ValidApp() => new()
    {
        View = new LineLiffView { Type = LineLiffViewType.Tall, Url = "https://example.com/liff", ModuleMode = false },
        Description = "測試用 LIFF",
        Features = new LineLiffFeatures { Ble = true, QrCode = false },
        PermanentLinkPattern = "concat",
        Scope = ["profile", "chat_message.write"],
        BotPrompt = "none",
    };

    [TestMethod]
    public async Task AddAsync_SendsAppAndReturnsLiffId()
    {
        var handler = FakeHttpMessageHandler.Json(Samples.Body("liff-add"));
        var client = CreateClient(handler);

        var result = await client.AddAsync(ValidApp());

        Assert.IsTrue(result.TryGetValue(out var liffId));
        Assert.AreEqual("1234567890-AbcdEfgh", liffId);
        Assert.AreEqual(HttpMethod.Post, handler.Last.Method);
        Assert.AreEqual(LineEndpoints.LiffApps, handler.Last.Uri.AbsoluteUri);
        Assert.AreEqual("Bearer " + AccessToken, handler.Last.Headers["Authorization"]);

        var body = handler.Last.Json();
        var view = body.GetProperty("view");
        Assert.AreEqual("tall", view.GetProperty("type").GetString(), "視窗大小要寫成小寫字串。");
        Assert.AreEqual("https://example.com/liff", view.GetProperty("url").GetString());
        Assert.IsFalse(view.GetProperty("moduleMode").GetBoolean());
        Assert.AreEqual("測試用 LIFF", body.GetProperty("description").GetString());
        Assert.IsTrue(body.GetProperty("features").GetProperty("ble").GetBoolean());
        Assert.IsFalse(body.GetProperty("features").GetProperty("qrCode").GetBoolean());
        Assert.AreEqual("concat", body.GetProperty("permanentLinkPattern").GetString());
        Assert.AreEqual(2, body.GetProperty("scope").GetArrayLength());
        Assert.AreEqual("none", body.GetProperty("botPrompt").GetString());
        Assert.IsFalse(body.TryGetProperty("liffId", out _), "liffId 絕不出現在請求裡。");
    }

    [TestMethod]
    public async Task AddAsync_MissingLiffIdInResponse_FailsAsInvalidResponse()
    {
        var client = CreateClient(FakeHttpMessageHandler.Json("{}"));

        var result = await client.AddAsync(ValidApp());

        Assert.AreEqual(LineErrorCodes.ApiInvalidResponse, result.Error!.Code);
    }

    [TestMethod]
    public async Task AddAsync_HttpFailure_KeepsCategory()
    {
        var client = CreateClient(Failing(HttpStatusCode.TooManyRequests));

        var result = await client.AddAsync(ValidApp());

        Assert.AreEqual(ErrorCategory.RateLimited, result.Error!.Category);
        Assert.AreEqual(LineErrorCodes.ApiError, result.Error.Code);
    }

    [TestMethod]
    public async Task AddAsync_InvalidApps_FailWithoutSending()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        var noView = new LineLiffApp { Description = "沒有檢視" };
        var noUrl = new LineLiffApp { View = new LineLiffView { Type = LineLiffViewType.Full } };
        var noType = new LineLiffApp { View = new LineLiffView { Url = "https://example.com" } };
        var httpUrl = new LineLiffApp { View = new LineLiffView { Type = LineLiffViewType.Full, Url = "http://example.com" } };
        var longDescription = new LineLiffApp
        {
            View = new LineLiffView { Type = LineLiffViewType.Full, Url = "https://example.com" },
            Description = new string('字', 101),
        };

        foreach (var app in new[] { noView, noUrl, noType, httpUrl, longDescription })
        {
            var result = await client.AddAsync(app);
            Assert.AreEqual(LineErrorCodes.InvalidLiffApp, result.Error!.Code);
        }

        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task AddAsync_Description100Chars_IsAccepted()
    {
        var handler = FakeHttpMessageHandler.Json(Samples.Body("liff-add"));
        var client = CreateClient(handler);
        var app = new LineLiffApp
        {
            View = new LineLiffView { Type = LineLiffViewType.Compact, Url = "https://example.com" },
            Description = new string('字', 100),
        };

        Assert.IsTrue((await client.AddAsync(app)).IsSuccess);
    }

    [TestMethod]
    public async Task UpdateAsync_SendsOnlyFieldsWithValue_AndNeverLiffId()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        var result = await client.UpdateAsync(
            "1234567890-AbcdEfgh",
            new LineLiffApp
            {
                LiffId = "should-not-be-sent",
                View = new LineLiffView { Url = "https://example.com/new" },
                BotPrompt = "aggressive",
            });

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(HttpMethod.Put, handler.Last.Method);
        Assert.AreEqual(LineEndpoints.LiffApps + "/1234567890-AbcdEfgh", handler.Last.Uri.AbsoluteUri);

        var body = handler.Last.Json();
        Assert.AreEqual("https://example.com/new", body.GetProperty("view").GetProperty("url").GetString());
        Assert.IsFalse(body.GetProperty("view").TryGetProperty("type", out _));
        Assert.IsFalse(body.GetProperty("view").TryGetProperty("moduleMode", out _));
        Assert.AreEqual("aggressive", body.GetProperty("botPrompt").GetString());
        Assert.IsFalse(body.TryGetProperty("liffId", out _));
        Assert.IsFalse(body.TryGetProperty("description", out _));
        Assert.IsFalse(body.TryGetProperty("features", out _));
        Assert.IsFalse(body.TryGetProperty("scope", out _));
        Assert.IsFalse(body.TryGetProperty("permanentLinkPattern", out _));
    }

    [TestMethod]
    public async Task UpdateAsync_InvalidUrl_FailsWithoutSending()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        var result = await client.UpdateAsync("id-1", new LineLiffApp { View = new LineLiffView { Url = "http://example.com" } });

        Assert.AreEqual(LineErrorCodes.InvalidLiffApp, result.Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task UpdateAsync_HttpFailure_KeepsCategory()
    {
        var client = CreateClient(Failing(HttpStatusCode.NotFound));

        var result = await client.UpdateAsync("id-1", new LineLiffApp { Description = "x" });

        Assert.AreEqual(ErrorCategory.NotFound, result.Error!.Category);
    }

    [TestMethod]
    public async Task GetAllAsync_ParsesSample()
    {
        var handler = FakeHttpMessageHandler.Json(Samples.Body("liff-apps"));
        var client = CreateClient(handler);

        var result = await client.GetAllAsync();

        Assert.IsTrue(result.TryGetValue(out var apps));
        Assert.AreEqual(2, apps.Count);
        Assert.AreEqual(HttpMethod.Get, handler.Last.Method);
        Assert.AreEqual(LineEndpoints.LiffApps, handler.Last.Uri.AbsoluteUri);

        var first = apps[0];
        Assert.AreEqual("1234567890-AbcdEfgh", first.LiffId);
        Assert.AreEqual(LineLiffViewType.Full, first.View!.Type);
        Assert.AreEqual("https://example.com/myservice", first.View.Url);
        Assert.AreEqual(false, first.View.ModuleMode);
        Assert.AreEqual("Happy New York", first.Description);
        Assert.AreEqual(true, first.Features!.Ble);
        Assert.AreEqual(true, first.Features.QrCode);
        Assert.AreEqual("concat", first.PermanentLinkPattern);
        string[] expectedScope = ["profile", "chat_message.write"];
        CollectionAssert.AreEqual(expectedScope, first.Scope!.ToArray());
        Assert.AreEqual("none", first.BotPrompt);

        var second = apps[1];
        Assert.AreEqual(LineLiffViewType.Tall, second.View!.Type);
        Assert.IsNull(second.Description);
        Assert.IsNull(second.Features);
        Assert.IsNull(second.Scope);
    }

    [TestMethod]
    public async Task GetAllAsync_NotFound_IsSuccessWithEmptyList()
    {
        var client = CreateClient(Failing(HttpStatusCode.NotFound));

        var result = await client.GetAllAsync();

        Assert.IsTrue(result.TryGetValue(out var apps), "404 代表沒有任何 LIFF,不是失敗。");
        Assert.AreEqual(0, apps.Count);
    }

    [TestMethod]
    public async Task GetAllAsync_OtherHttpFailure_StillFailsAndKeepsCategory()
    {
        var client = CreateClient(Failing(HttpStatusCode.TooManyRequests));

        var result = await client.GetAllAsync();

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(ErrorCategory.RateLimited, result.Error.Category);
    }

    [TestMethod]
    public async Task GetAllAsync_EmptyAppsArray_IsEmptyList()
    {
        var client = CreateClient(FakeHttpMessageHandler.Json("""{"apps":[]}"""));

        var result = await client.GetAllAsync();

        Assert.IsTrue(result.TryGetValue(out var apps));
        Assert.AreEqual(0, apps.Count);
    }

    [TestMethod]
    public async Task GetAllAsync_UnknownViewType_FailsAsInvalidResponse()
    {
        var client = CreateClient(FakeHttpMessageHandler.Json("""{"apps":[{"liffId":"a","view":{"type":"gigantic","url":"https://example.com"}}]}"""));

        var result = await client.GetAllAsync();

        Assert.AreEqual(LineErrorCodes.ApiInvalidResponse, result.Error!.Code);
    }

    [TestMethod]
    public async Task DeleteAsync_SendsDelete()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        var result = await client.DeleteAsync("1234567890-AbcdEfgh");

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(HttpMethod.Delete, handler.Last.Method);
        Assert.AreEqual(LineEndpoints.LiffApps + "/1234567890-AbcdEfgh", handler.Last.Uri.AbsoluteUri);
    }

    [TestMethod]
    public async Task DeleteAsync_HttpFailure_KeepsCategory()
    {
        var client = CreateClient(Failing(HttpStatusCode.NotFound));

        var result = await client.DeleteAsync("id-1");

        Assert.AreEqual(ErrorCategory.NotFound, result.Error!.Category);
        Assert.AreEqual(LineErrorCodes.ApiError, result.Error.Code);
    }

    [TestMethod]
    public async Task AllMethods_NotConfigured_FailWithoutSending()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = new LineLiffClient(
            new HttpPipelineClient(new HttpClient(handler)),
            Options.Create(new LineLiffOptions()));

        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.AddAsync(ValidApp())).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.UpdateAsync("id-1", ValidApp())).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.GetAllAsync()).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.DeleteAsync("id-1")).Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public void Options_Validate_ReflectsConfiguration()
    {
        Assert.AreEqual(LineErrorCodes.NotConfigured, new LineLiffOptions().Validate().Error!.Code);
        Assert.IsTrue(new LineLiffOptions { ChannelAccessToken = AccessToken }.Validate().IsSuccess);
    }

    [TestMethod]
    public async Task AddLineLiff_ResolvesClient_AndUsesLiffNamedHttpClient()
    {
        var handler = FakeHttpMessageHandler.Json(Samples.Body("liff-add"));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLineLiff(options => options.ChannelAccessToken = AccessToken);
        services.AddHttpClient(LineHttpClientNames.Liff).ConfigurePrimaryHttpMessageHandler(() => handler);

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ILineLiffClient>();

        var result = await client.AddAsync(ValidApp());

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("Bearer " + AccessToken, handler.Last.Headers["Authorization"]);
    }

    private static FakeHttpMessageHandler Failing(HttpStatusCode status) =>
        new((_, _) => new HttpResponseMessage(status)
        {
            Content = new StringContent("""{"message":"boom"}""", System.Text.Encoding.UTF8, "application/json"),
        });

    private static LineLiffClient CreateClient(FakeHttpMessageHandler handler) =>
        new(
            new HttpPipelineClient(new HttpClient(handler)),
            Options.Create(new LineLiffOptions { ChannelAccessToken = AccessToken }));
}
