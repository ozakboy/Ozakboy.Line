using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ozakboy.Line.AspNetCore.Webhook;
using Ozakboy.Line.AutoReply;
using Ozakboy.Line.Messaging;

namespace Ozakboy.Line.AspNetCore.Tests;

/// <summary>
/// webhook 端點開啟自動回覆時的整合測試。
/// Integration tests for the webhook endpoint with auto reply turned on.
/// </summary>
[TestClass]
public sealed class LineWebhookAutoReplyTests
{
    private const string ChannelSecret = "test-webhook-channel-secret";

    private const string Body = """
    {"destination":"Ubot","events":[{"type":"message","mode":"active","timestamp":1799999999000,"webhookEventId":"01ABC","deliveryContext":{"isRedelivery":false},"replyToken":"rt-1","source":{"type":"user","userId":"U1"},"message":{"id":"m-1","type":"text","text":"嗨"}}]}
    """;

    [TestMethod]
    public async Task AutoReplyEnabled_PutsTheOutcomeInItemsAndStillCallsTheHandler()
    {
        // 自動回覆不取代處理常式,只是搶在它前面把規則回得了的回掉;宿主自己決定要不要再處理一次。
        var service = new StubAutoReplyService(LineAutoReplyOutcome.Replied("rule-1"));
        var handled = 0;
        object? outcome = null;

        using var host = await CreateHostAsync(service, options => options.AutoReply = true, (_, context, _) =>
        {
            handled++;
            context.Items.TryGetValue(LineWebhookItems.AutoReplyOutcome, out outcome);
            return Task.CompletedTask;
        });
        using var client = host.GetTestClient();

        using var response = await PostAsync(client, Body, Sign(Body));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(1, service.Handled, "每個事件都會先跑一次自動回覆。");
        Assert.AreEqual(1, handled, "處理常式照常被呼叫。");
        Assert.IsInstanceOfType<LineAutoReplyOutcome>(outcome);
        Assert.AreEqual(LineAutoReplyOutcomeKind.Replied, ((LineAutoReplyOutcome)outcome!).Kind);
        Assert.AreEqual("rule-1", ((LineAutoReplyOutcome)outcome).RuleId);
    }

    [TestMethod]
    public async Task AutoReplyDisabled_NeverCallsTheService()
    {
        var service = new StubAutoReplyService(LineAutoReplyOutcome.Replied("rule-1"));
        object? outcome = null;

        using var host = await CreateHostAsync(service, configure: null, (_, context, _) =>
        {
            context.Items.TryGetValue(LineWebhookItems.AutoReplyOutcome, out outcome);
            return Task.CompletedTask;
        });
        using var client = host.GetTestClient();

        using var response = await PostAsync(client, Body, Sign(Body));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(0, service.Handled, "預設關閉:升級套件不該讓機器人無聲地開始講話。");
        Assert.IsNull(outcome);
    }

    [TestMethod]
    public async Task AutoReplyFails_LogsAndStillAnswers200()
    {
        // 回非 2xx 會讓 LINE 重送整批,一則規則渲染不出來不該讓整批事件被反覆重做。
        var service = new StubAutoReplyService(Error.Validation(LineErrorCodes.MissingTemplateVariables, "缺變數"));
        var handled = 0;
        object? outcome = null;

        using var host = await CreateHostAsync(service, options => options.AutoReply = true, (_, context, _) =>
        {
            handled++;
            context.Items.TryGetValue(LineWebhookItems.AutoReplyOutcome, out outcome);
            return Task.CompletedTask;
        });
        using var client = host.GetTestClient();

        using var response = await PostAsync(client, Body, Sign(Body));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(1, handled, "自動回覆失敗不影響處理常式。");
        Assert.IsNull(outcome, "失敗時不放結果,免得宿主把失敗誤讀成「已經回過了」。");
    }

    [TestMethod]
    public async Task AutoReplyThrows_StillAnswers200()
    {
        var service = new StubAutoReplyService(new InvalidOperationException("服務壞了"));
        var handled = 0;

        using var host = await CreateHostAsync(service, options => options.AutoReply = true, (_, _, _) =>
        {
            handled++;
            return Task.CompletedTask;
        });
        using var client = host.GetTestClient();

        using var response = await PostAsync(client, Body, Sign(Body));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(1, handled);
    }

    [TestMethod]
    public async Task AutoReplyEnabledWithoutRegistration_ThrowsWhileMapping()
    {
        // 漏掉註冊在執行期沒有任何徵兆:端點回 200、log 一片乾淨,只是機器人不說話。
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            CreateHostAsync(service: null, options => options.AutoReply = true, (_, _, _) => Task.CompletedTask));

        StringAssert.Contains(exception.Message, "AddLineAutoReply", "訊息要直接說出該呼叫哪一個方法。");
    }

    /// <summary>
    /// 對 webhook 端點送出一次請求。
    /// Posts once to the webhook endpoint.
    /// </summary>
    /// <param name="client">測試用戶端。The test client.</param>
    /// <param name="body">請求內容。The body.</param>
    /// <param name="signature">簽章。The signature.</param>
    /// <returns>回應。The response.</returns>
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string body, string signature)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/line/webhook", UriKind.Relative))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(LineWebhookHttpRequestExtensions.SignatureHeaderName, signature);

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
    /// 建立測試主機。
    /// Creates the test host.
    /// </summary>
    /// <param name="service">自動回覆服務;為 <see langword="null"/> 時不註冊。The auto reply service, or <see langword="null"/> to leave it unregistered.</param>
    /// <param name="configure">端點設定。The endpoint settings.</param>
    /// <param name="handler">事件處理常式。The event handler.</param>
    /// <returns>已啟動的主機。The started host.</returns>
    private static async Task<IHost> CreateHostAsync(
        StubAutoReplyService? service,
        Action<LineWebhookEndpointOptions>? configure,
        Func<LineWebhookEvent, HttpContext, CancellationToken, Task> handler)
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

                    if (service is not null)
                    {
                        services.AddSingleton<ILineAutoReplyService>(service);
                    }
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                        endpoints.MapLineWebhook("/line/webhook", configure, handler));
                }))
            .Build();

        await host.StartAsync();
        return host;
    }

    /// <summary>
    /// 回傳事先安排好結果的自動回覆服務。
    /// An auto reply service answering with an arranged result.
    /// </summary>
    private sealed class StubAutoReplyService : ILineAutoReplyService
    {
        private readonly Result<LineAutoReplyOutcome> _result;
        private readonly Exception? _exception;

        /// <summary>
        /// 以成功結果建立。
        /// Creates one answering with a successful outcome.
        /// </summary>
        /// <param name="outcome">結果。The outcome.</param>
        internal StubAutoReplyService(LineAutoReplyOutcome outcome) => _result = Result.Success(outcome);

        /// <summary>
        /// 以失敗建立。
        /// Creates one answering with a failure.
        /// </summary>
        /// <param name="error">錯誤。The error.</param>
        internal StubAutoReplyService(Error error) => _result = Result.Failure<LineAutoReplyOutcome>(error);

        /// <summary>
        /// 以擲例外建立。
        /// Creates one that throws.
        /// </summary>
        /// <param name="exception">例外。The exception.</param>
        internal StubAutoReplyService(Exception exception)
        {
            _exception = exception;
            _result = Result.Success(LineAutoReplyOutcome.Skipped);
        }

        /// <summary>
        /// 被呼叫過幾次。
        /// How many times it was called.
        /// </summary>
        internal int Handled { get; private set; }

        /// <inheritdoc />
        public Task<Result<LineAutoReplyOutcome>> HandleAsync(
            LineWebhookEvent webhookEvent,
            CancellationToken cancellationToken = default)
        {
            Handled++;

            return _exception is not null
                ? throw _exception
                : Task.FromResult(_result);
        }
    }
}
