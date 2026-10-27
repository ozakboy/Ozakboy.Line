using System.Net;
using Microsoft.Extensions.Options;
using Ozakboy.Core.Abstractions;
using Ozakboy.Http;
using Ozakboy.Line.Messaging;
using Ozakboy.Line.Tests.TestSupport;

namespace Ozakboy.Line.Tests;

/// <summary>
/// webhook 位址查 / 設 / 測試、帳號連結權杖、訊息內容縮圖與轉檔狀態端點的測試。全部離線。
/// Tests for the webhook endpoint get / set / test, account link token, message content preview and transcoding
/// status endpoints. Entirely offline.
/// </summary>
[TestClass]
public sealed class LineMessagingClientChannelTests
{
    private const string AccessToken = "test-channel-access-token";

    // ---------- A1:webhook endpoint ----------

    [TestMethod]
    public async Task GetWebhookEndpointAsync_ParsesSample()
    {
        var handler = FakeHttpMessageHandler.Json(Samples.Body("webhook-endpoint"));
        var client = CreateClient(handler);

        var result = await client.GetWebhookEndpointAsync();

        Assert.IsTrue(result.TryGetValue(out var info));
        Assert.AreEqual("https://example.com/test", info.Endpoint);
        Assert.IsTrue(info.Active);
        Assert.AreEqual(HttpMethod.Get, handler.Last.Method);
        Assert.AreEqual(LineEndpoints.WebhookEndpoint, handler.Last.Uri.AbsoluteUri);
        Assert.AreEqual("Bearer " + AccessToken, handler.Last.Headers["Authorization"]);
    }

    [TestMethod]
    public async Task GetWebhookEndpointAsync_HttpFailure_KeepsCategory()
    {
        var client = CreateClient(Failing(HttpStatusCode.TooManyRequests));

        var result = await client.GetWebhookEndpointAsync();

        Assert.AreEqual(ErrorCategory.RateLimited, result.Error!.Category);
        Assert.AreEqual(LineErrorCodes.ApiError, result.Error.Code);
    }

    [TestMethod]
    public async Task SetWebhookEndpointAsync_SendsPutWithEndpoint()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        var result = await client.SetWebhookEndpointAsync("https://example.com/hook");

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(HttpMethod.Put, handler.Last.Method);
        Assert.AreEqual(LineEndpoints.WebhookEndpoint, handler.Last.Uri.AbsoluteUri);
        Assert.AreEqual("https://example.com/hook", handler.Last.Json().GetProperty("endpoint").GetString());
    }

    [TestMethod]
    public async Task SetWebhookEndpointAsync_HttpFailure_KeepsCategory()
    {
        var client = CreateClient(Failing(HttpStatusCode.BadRequest));

        var result = await client.SetWebhookEndpointAsync("https://example.com/hook");

        Assert.AreEqual(ErrorCategory.Validation, result.Error!.Category);
        Assert.AreEqual(LineErrorCodes.ApiError, result.Error.Code);
    }

    [TestMethod]
    [DataRow("http://example.com/hook")]
    [DataRow("example.com/hook")]
    [DataRow("")]
    [DataRow("   ")]
    public async Task SetWebhookEndpointAsync_NotHttps_FailsWithoutSending(string endpoint)
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        var result = await client.SetWebhookEndpointAsync(endpoint);

        Assert.AreEqual(LineErrorCodes.InvalidWebhookEndpoint, result.Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task SetWebhookEndpointAsync_Length500_IsAcceptedAndOver500Fails()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);
        var atLimit = "https://example.com/" + new string('a', 500 - "https://example.com/".Length);
        var overLimit = atLimit + "a";

        Assert.AreEqual(500, atLimit.Length);
        Assert.IsTrue((await client.SetWebhookEndpointAsync(atLimit)).IsSuccess);
        Assert.AreEqual(1, handler.Requests.Count);

        var result = await client.SetWebhookEndpointAsync(overLimit);

        Assert.AreEqual(LineErrorCodes.InvalidWebhookEndpoint, result.Error!.Code);
        Assert.AreEqual(1, handler.Requests.Count, "超過 500 字元不得送出。");
    }

    [TestMethod]
    public async Task TestWebhookEndpointAsync_WithEndpoint_SendsItAndParsesSample()
    {
        var handler = FakeHttpMessageHandler.Json(Samples.Body("webhook-test-result"));
        var client = CreateClient(handler);

        var result = await client.TestWebhookEndpointAsync("https://example.com/test");

        Assert.IsTrue(result.TryGetValue(out var test));
        Assert.IsTrue(test.Success);
        Assert.AreEqual(new DateTimeOffset(2020, 9, 30, 5, 38, 20, 31, TimeSpan.Zero), test.Timestamp);
        Assert.AreEqual(200, test.StatusCode);
        Assert.AreEqual("OK", test.Reason);
        Assert.AreEqual("200", test.Detail);
        Assert.AreEqual(HttpMethod.Post, handler.Last.Method);
        Assert.AreEqual(LineEndpoints.WebhookTest, handler.Last.Uri.AbsoluteUri);
        Assert.AreEqual("https://example.com/test", handler.Last.Json().GetProperty("endpoint").GetString());
    }

    [TestMethod]
    public async Task TestWebhookEndpointAsync_WithoutEndpoint_SendsEmptyObject()
    {
        var handler = FakeHttpMessageHandler.Json(Samples.Body("webhook-test-result"));
        var client = CreateClient(handler);

        var result = await client.TestWebhookEndpointAsync();

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("{}", handler.Last.Body);
    }

    [TestMethod]
    public async Task TestWebhookEndpointAsync_InvalidEndpoint_FailsWithoutSending()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        var result = await client.TestWebhookEndpointAsync("http://example.com/test");

        Assert.AreEqual(LineErrorCodes.InvalidWebhookEndpoint, result.Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task TestWebhookEndpointAsync_HttpFailure_KeepsCategory()
    {
        var client = CreateClient(Failing(HttpStatusCode.NotFound));

        var result = await client.TestWebhookEndpointAsync();

        Assert.AreEqual(ErrorCategory.NotFound, result.Error!.Category);
    }

    // ---------- A3:link token ----------

    [TestMethod]
    public async Task IssueLinkTokenAsync_ParsesSampleAndPostsWithoutBody()
    {
        var handler = FakeHttpMessageHandler.Json(Samples.Body("link-token"));
        var client = CreateClient(handler);

        var result = await client.IssueLinkTokenAsync("U 1");

        Assert.IsTrue(result.TryGetValue(out var token));
        Assert.AreEqual("NMZTNuVrPTqlr2IF8Bnymkb7rXfYv5EY", token);
        Assert.AreEqual(HttpMethod.Post, handler.Last.Method);
        Assert.AreEqual("https://api.line.me/v2/bot/user/U%201/linkToken", handler.Last.Uri.AbsoluteUri, "userId 要編碼;驗證編碼比對 AbsoluteUri。");
        Assert.IsNull(handler.Last.Body);
    }

    [TestMethod]
    public async Task IssueLinkTokenAsync_MissingToken_FailsAsInvalidResponse()
    {
        var client = CreateClient(FakeHttpMessageHandler.Json("{}"));

        var result = await client.IssueLinkTokenAsync("U1");

        Assert.AreEqual(LineErrorCodes.ApiInvalidResponse, result.Error!.Code);
    }

    [TestMethod]
    public async Task IssueLinkTokenAsync_HttpFailure_KeepsCategory()
    {
        var client = CreateClient(Failing(HttpStatusCode.NotFound));

        var result = await client.IssueLinkTokenAsync("U1");

        Assert.AreEqual(ErrorCategory.NotFound, result.Error!.Category);
        Assert.AreEqual(LineErrorCodes.ApiError, result.Error.Code);
    }

    // ---------- A8:preview 與 transcoding ----------

    [TestMethod]
    public async Task GetMessageContentPreviewAsync_ReturnsBytesAndContentType()
    {
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        var handler = new FakeHttpMessageHandler((_, _) =>
        {
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });
        var client = CreateClient(handler);

        var result = await client.GetMessageContentPreviewAsync("m-1");

        Assert.IsTrue(result.TryGetValue(out var content));
        CollectionAssert.AreEqual(bytes, content.Bytes, "二進位內容不能經過字串轉換。");
        Assert.AreEqual("image/jpeg", content.ContentType);
        Assert.AreEqual(HttpMethod.Get, handler.Last.Method);
        Assert.AreEqual("https://api-data.line.me/v2/bot/message/m-1/content/preview", handler.Last.Uri.AbsoluteUri);
    }

    [TestMethod]
    public async Task GetMessageContentPreviewAsync_HttpFailure_KeepsCategory()
    {
        var client = CreateClient(Failing(HttpStatusCode.NotFound));

        var result = await client.GetMessageContentPreviewAsync("m-1");

        Assert.AreEqual(ErrorCategory.NotFound, result.Error!.Category);
        Assert.AreEqual(LineErrorCodes.ApiError, result.Error.Code);
        Assert.IsTrue(result.Error.TryGetData(LineErrorDataKeys.LineMessage, out var message));
        Assert.AreEqual("boom", message);
    }

    [TestMethod]
    public async Task GetMessageContentTranscodingStatusAsync_ParsesSample()
    {
        var handler = FakeHttpMessageHandler.Json(Samples.Body("transcoding-status"));
        var client = CreateClient(handler);

        var result = await client.GetMessageContentTranscodingStatusAsync("m-1");

        Assert.IsTrue(result.TryGetValue(out var status));
        Assert.AreEqual(LineContentTranscodingStatus.Succeeded, status);
        Assert.AreEqual(HttpMethod.Get, handler.Last.Method);
        Assert.AreEqual("https://api-data.line.me/v2/bot/message/m-1/content/transcoding", handler.Last.Uri.AbsoluteUri);
    }

    [TestMethod]
    [DataRow("processing", LineContentTranscodingStatus.Processing)]
    [DataRow("succeeded", LineContentTranscodingStatus.Succeeded)]
    [DataRow("failed", LineContentTranscodingStatus.Failed)]
    public async Task GetMessageContentTranscodingStatusAsync_MapsEveryStatus(string wire, LineContentTranscodingStatus expected)
    {
        var client = CreateClient(FakeHttpMessageHandler.Json($$"""{"status":"{{wire}}"}"""));

        var result = await client.GetMessageContentTranscodingStatusAsync("m-1");

        Assert.IsTrue(result.TryGetValue(out var status));
        Assert.AreEqual(expected, status);
    }

    [TestMethod]
    [DataRow("""{"status":"exploded"}""")]
    [DataRow("{}")]
    public async Task GetMessageContentTranscodingStatusAsync_UnknownStatus_FailsAsInvalidResponse(string body)
    {
        var client = CreateClient(FakeHttpMessageHandler.Json(body));

        var result = await client.GetMessageContentTranscodingStatusAsync("m-1");

        Assert.AreEqual(LineErrorCodes.ApiInvalidResponse, result.Error!.Code);
    }

    [TestMethod]
    public async Task GetMessageContentTranscodingStatusAsync_HttpFailure_KeepsCategory()
    {
        var client = CreateClient(Failing(HttpStatusCode.TooManyRequests));

        var result = await client.GetMessageContentTranscodingStatusAsync("m-1");

        Assert.AreEqual(ErrorCategory.RateLimited, result.Error!.Category);
    }

    // ---------- not_configured ----------

    [TestMethod]
    public async Task ChannelEndpoints_NotConfigured_FailWithoutSending()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = new LineMessagingClient(
            new HttpPipelineClient(new HttpClient(handler)),
            Options.Create(new LineMessagingOptions()));

        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.GetWebhookEndpointAsync()).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.SetWebhookEndpointAsync("https://example.com/hook")).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.TestWebhookEndpointAsync()).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.IssueLinkTokenAsync("U1")).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.GetMessageContentPreviewAsync("m-1")).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.GetMessageContentTranscodingStatusAsync("m-1")).Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    private static FakeHttpMessageHandler Failing(HttpStatusCode status) =>
        new((_, _) => new HttpResponseMessage(status)
        {
            Content = new StringContent("""{"message":"boom"}""", System.Text.Encoding.UTF8, "application/json"),
        });

    private static LineMessagingClient CreateClient(FakeHttpMessageHandler handler) =>
        new(
            new HttpPipelineClient(new HttpClient(handler)),
            Options.Create(new LineMessagingOptions { ChannelAccessToken = AccessToken }));
}
