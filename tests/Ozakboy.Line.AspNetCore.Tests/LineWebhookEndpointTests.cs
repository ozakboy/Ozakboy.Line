using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ozakboy.Line.AspNetCore.Webhook;
using Ozakboy.Line.Messaging;

namespace Ozakboy.Line.AspNetCore.Tests;

/// <summary>
/// webhook 端點的整合測試。
/// Integration tests for the webhook endpoint.
/// </summary>
[TestClass]
public sealed class LineWebhookEndpointTests
{
    private const string ChannelSecret = "test-webhook-channel-secret";

    private const string Body = """
    {"destination":"Ubot","events":[{"type":"message","mode":"active","timestamp":1799999999000,"webhookEventId":"01ABC","deliveryContext":{"isRedelivery":false},"replyToken":"rt-1","source":{"type":"user","userId":"U1"},"message":{"id":"m-1","type":"text","text":"哈囉"}}]}
    """;

    [TestMethod]
    public async Task ValidSignature_InvokesHandlerAndAnswers200()
    {
        var received = new List<LineWebhookEvent>();
        using var host = await CreateHostAsync((webhookEvent, _, _) =>
        {
            received.Add(webhookEvent);
            return Task.CompletedTask;
        });
        using var client = host.GetTestClient();

        using var response = await PostAsync(client, Body, Sign(Body));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(1, received.Count);
        Assert.AreEqual(LineWebhookEventTypes.Message, received[0].Type);
        Assert.AreEqual("哈囉", received[0].Message!.Text);
        Assert.AreEqual("rt-1", received[0].ReplyToken);
        Assert.AreEqual("U1", received[0].Source.UserId);
    }

    [TestMethod]
    public async Task WrongSignature_Answers401AndNeverInvokesHandler()
    {
        var invoked = false;
        using var host = await CreateHostAsync((_, _, _) =>
        {
            invoked = true;
            return Task.CompletedTask;
        });
        using var client = host.GetTestClient();

        using var response = await PostAsync(client, Body, Convert.ToBase64String(new byte[32]));

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode, "沒有證明自己是 LINE 送的請求,回的是 401 而不是 400。");
        Assert.IsFalse(invoked, "驗簽沒過的內容一個位元組都不該交給處理常式。");
    }

    [TestMethod]
    public async Task MissingSignature_Answers401()
    {
        using var host = await CreateHostAsync((_, _, _) => Task.CompletedTask);
        using var client = host.GetTestClient();

        using var response = await PostAsync(client, Body, signature: null);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task TamperedBody_Answers401()
    {
        // 簽章是對著原始內容算的:改掉一個字就驗不過,這正是這道防線的作用。
        using var host = await CreateHostAsync((_, _, _) => Task.CompletedTask);
        using var client = host.GetTestClient();
        var signature = Sign(Body);

        using var response = await PostAsync(client, Body.Replace("哈囉", "轉帳", StringComparison.Ordinal), signature);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task UnparsableBodyWithValidSignature_Answers400()
    {
        const string Broken = "{not json";
        using var host = await CreateHostAsync((_, _, _) => Task.CompletedTask);
        using var client = host.GetTestClient();

        using var response = await PostAsync(client, Broken, Sign(Broken));

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode, "簽章對但內容讀不懂,那才是 400。");
    }

    [TestMethod]
    public async Task HandlerThrows_StillAnswers200()
    {
        // 回非 2xx 會讓 LINE 重送整批。一個處理常式壞掉就讓整批回 500,結果是其他本來成功的事件
        // 被反覆重做,而壞掉的那個仍然壞掉。
        using var host = await CreateHostAsync((_, _, _) => throw new InvalidOperationException("處理常式壞了"));
        using var client = host.GetTestClient();

        using var response = await PostAsync(client, Body, Sign(Body));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task OneFailingEvent_DoesNotStopTheRest()
    {
        const string TwoEvents = """
        {"destination":"Ubot","events":[
          {"type":"message","timestamp":1,"replyToken":"rt-1","source":{"type":"user","userId":"U1"},"message":{"id":"m-1","type":"text","text":"炸"}},
          {"type":"message","timestamp":2,"replyToken":"rt-2","source":{"type":"user","userId":"U2"},"message":{"id":"m-2","type":"text","text":"好"}}
        ]}
        """;

        var handled = new List<string>();
        using var host = await CreateHostAsync((webhookEvent, _, _) =>
        {
            if (webhookEvent.Message?.Text == "炸")
            {
                throw new InvalidOperationException("這一個事件壞了");
            }

            handled.Add(webhookEvent.Message!.Text!);
            return Task.CompletedTask;
        });
        using var client = host.GetTestClient();

        using var response = await PostAsync(client, TwoEvents, Sign(TwoEvents));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        CollectionAssert.AreEqual(new List<string> { "好" }, handled, "前一個事件壞掉不該讓後面的事件收不到。");
    }

    [TestMethod]
    public async Task VerificationRequestWithNoEvents_Answers200()
    {
        // LINE 後台按「驗證」時送的就是一個沒有事件的請求。回非 200 會讓後台顯示驗證失敗。
        const string Empty = """{"destination":"Ubot","events":[]}""";
        using var host = await CreateHostAsync((_, _, _) => Task.CompletedTask);
        using var client = host.GetTestClient();

        using var response = await PostAsync(client, Empty, Sign(Empty));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task PayloadOverload_ReceivesTheWholeBatch()
    {
        LineWebhookPayload? received = null;
        using var host = await CreatePayloadHostAsync((payload, _, _) =>
        {
            received = payload;
            return Task.CompletedTask;
        });
        using var client = host.GetTestClient();

        using var response = await PostAsync(client, Body, Sign(Body));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNotNull(received);
        Assert.AreEqual("Ubot", received.Destination);
        Assert.AreEqual(1, received.Events.Count);
    }

    /// <summary>
    /// 對 webhook 端點送出一次請求。
    /// Posts once to the webhook endpoint.
    /// </summary>
    /// <param name="client">測試用戶端。The test client.</param>
    /// <param name="body">請求內容。The body.</param>
    /// <param name="signature">簽章;不帶時為 <see langword="null"/>。The signature, or <see langword="null"/> to omit it.</param>
    /// <returns>回應。The response.</returns>
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string body, string? signature)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/line/webhook", UriKind.Relative))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        if (signature is not null)
        {
            request.Headers.Add(LineWebhookHttpRequestExtensions.SignatureHeaderName, signature);
        }

        return await client.SendAsync(request);
    }

    /// <summary>
    /// 以測試用的密鑰簽一份內容。
    /// Signs a body with the test secret.
    /// </summary>
    /// <param name="body">內容。The body.</param>
    /// <returns>簽章。The signature.</returns>
    private static string Sign(string body) =>
        LineWebhookSignature.Compute(ChannelSecret, Encoding.UTF8.GetBytes(body));

    /// <summary>
    /// 建立掛上逐事件處理常式的測試主機。
    /// Creates a test host with a per-event handler.
    /// </summary>
    /// <param name="handler">處理常式。The handler.</param>
    /// <returns>已啟動的主機。The started host.</returns>
    private static Task<IHost> CreateHostAsync(Func<LineWebhookEvent, HttpContext, CancellationToken, Task> handler) =>
        StartAsync(endpoints => endpoints.MapLineWebhook("/line/webhook", handler));

    /// <summary>
    /// 建立掛上整批處理常式的測試主機。
    /// Creates a test host with a whole-payload handler.
    /// </summary>
    /// <param name="handler">處理常式。The handler.</param>
    /// <returns>已啟動的主機。The started host.</returns>
    private static Task<IHost> CreatePayloadHostAsync(Func<LineWebhookPayload, HttpContext, CancellationToken, Task> handler) =>
        StartAsync(endpoints => endpoints.MapLineWebhook("/line/webhook", handler));

    /// <summary>
    /// 啟動測試主機。
    /// Starts the test host.
    /// </summary>
    /// <param name="map">掛端點的委派。The delegate that maps the endpoint.</param>
    /// <returns>已啟動的主機。The started host.</returns>
    private static async Task<IHost> StartAsync(Action<Microsoft.AspNetCore.Routing.IEndpointRouteBuilder> map)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(builder => builder
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddLogging();
                    services.AddLineMessaging(options =>
                    {
                        options.ChannelAccessToken = "test-channel-access-token";
                        options.ChannelSecret = ChannelSecret;
                    });
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => map(endpoints));
                }))
            .Build();

        await host.StartAsync();
        return host;
    }
}
