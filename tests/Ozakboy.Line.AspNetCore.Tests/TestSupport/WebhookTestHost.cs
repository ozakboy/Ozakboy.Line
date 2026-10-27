using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ozakboy.Line.AspNetCore.Webhook;
using Ozakboy.Line.Messaging;
using Ozakboy.Line.Tests.TestSupport;

namespace Ozakboy.Line.AspNetCore.Tests.TestSupport;

/// <summary>
/// webhook 端點測試共用的主機、請求與簽章工具。
/// Shared host, request and signature helpers for the webhook endpoint tests.
/// </summary>
internal static class WebhookTestHost
{
    /// <summary>測試用的頻道密鑰。The channel secret used by the tests.</summary>
    internal const string ChannelSecret = "test-webhook-channel-secret";

    /// <summary>
    /// 啟動測試主機;發訊用戶端換成記錄用的假用戶端。
    /// Starts a test host with the messaging client replaced by a recording fake.
    /// </summary>
    /// <param name="recorder">記錄器。The recorder.</param>
    /// <param name="client">與記錄器對應的介面。The interface for that recorder.</param>
    /// <param name="registerServices">額外的服務註冊。Extra service registrations.</param>
    /// <param name="map">掛端點的委派。The delegate that maps the endpoint.</param>
    /// <param name="observeItems">每個請求結束後拿到 <c>HttpContext.Items</c> 的委派。Receives <c>HttpContext.Items</c> after each request.</param>
    /// <returns>已啟動的主機。The started host.</returns>
    internal static async Task<IHost> StartAsync(
        ILineMessagingClient client,
        Action<IServiceCollection>? registerServices,
        Action<IEndpointRouteBuilder> map,
        Action<IDictionary<object, object?>>? observeItems = null)
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

                    // 最後註冊的贏:用記錄用的假用戶端取代真的。
                    // The last registration wins: swap the real client for the recording one.
                    services.AddSingleton(client);
                    registerServices?.Invoke(services);
                })
                .Configure(app =>
                {
                    app.Use(async (context, next) =>
                    {
                        await next();
                        observeItems?.Invoke(context.Items);
                    });
                    app.UseRouting();
                    app.UseEndpoints(endpoints => map(endpoints));
                }))
            .Build();

        await host.StartAsync();
        return host;
    }

    /// <summary>
    /// 對 webhook 端點送出一次已簽章的請求。
    /// Posts a signed request to the webhook endpoint.
    /// </summary>
    /// <param name="client">測試用戶端。The test client.</param>
    /// <param name="body">請求內容。The body.</param>
    /// <param name="sign">是否簽章。Whether to sign it.</param>
    /// <returns>回應。The response.</returns>
    internal static async Task<HttpResponseMessage> PostAsync(HttpClient client, string body, bool sign = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/line/webhook", UriKind.Relative))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        if (sign)
        {
            request.Headers.Add(
                LineWebhookHttpRequestExtensions.SignatureHeaderName,
                LineWebhookSignature.Compute(ChannelSecret, Encoding.UTF8.GetBytes(body)));
        }

        return await client.SendAsync(request);
    }

    /// <summary>
    /// 組出只有一個事件的請求內容。
    /// Builds a request body holding one event.
    /// </summary>
    /// <param name="source">來源物件的 JSON。The source object as JSON.</param>
    /// <param name="replyToken">回覆權杖;為 <see langword="null"/> 時不帶。The reply token, or <see langword="null"/> to omit it.</param>
    /// <param name="text">文字訊息內容。The text of the message.</param>
    /// <param name="mode">處理模式。The mode.</param>
    /// <returns>JSON 內容。The JSON body.</returns>
    internal static string TextEventBody(
        string source = """{"type":"user","userId":"U1"}""",
        string? replyToken = "rt-1",
        string text = "哈囉",
        string mode = "active")
    {
        var token = replyToken is null ? string.Empty : "\"replyToken\":\"" + replyToken + "\",";
        const string Template = """
        {"destination":"Ubot","events":[{"type":"message","mode":"@MODE@","timestamp":1799999999000,"webhookEventId":"01ABC","deliveryContext":{"isRedelivery":false},@TOKEN@"source":@SOURCE@,"message":{"id":"m-1","type":"text","text":"@TEXT@"}}]}
        """;

        return Template
            .Replace("@MODE@", mode, StringComparison.Ordinal)
            .Replace("@TOKEN@", token, StringComparison.Ordinal)
            .Replace("@SOURCE@", source, StringComparison.Ordinal)
            .Replace("@TEXT@", text, StringComparison.Ordinal);
    }
}
