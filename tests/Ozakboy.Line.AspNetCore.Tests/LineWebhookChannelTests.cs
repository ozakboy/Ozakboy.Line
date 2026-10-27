using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Ozakboy.Line.AspNetCore.Webhook;
using Ozakboy.Line.AutoReply;
using Ozakboy.Line.Messaging;
using Ozakboy.Line.Postback;
using Ozakboy.Line.Tests.TestSupport;
using Ozakboy.Line.Webhook.Handling;

namespace Ozakboy.Line.AspNetCore.Tests;

/// <summary>
/// webhook 端點依 channel 運作的整合測試:各 channel 各自的密鑰、路由參數、固定名稱、未知 channel 與互斥檢查。
/// Integration tests for the webhook endpoint per channel: each channel's own secret, the route parameter, a fixed
/// name, an unknown channel, and the mutual-exclusion check.
/// </summary>
[TestClass]
public sealed class LineWebhookChannelTests
{
    private const string SecretA = "secret-of-channel-a";
    private const string SecretB = "secret-of-channel-b";

    [TestMethod]
    public async Task RouteParameter_SelectsTheChannel_AndVerifiesWithThatChannelsSecret()
    {
        var seen = new List<string>();
        using var host = await StartAsync(
            endpoints => endpoints.MapLineWebhook(
                "/line/webhook/{channel}",
                (_, context, _) =>
                {
                    seen.Add((string)context.Items[LineWebhookItems.ChannelName]!);
                    return Task.CompletedTask;
                },
                new LineWebhookEndpointOptions { ChannelRouteParameter = "channel" }));
        using var http = host.GetTestClient();

        using var a = await PostAsync(http, "/line/webhook/a", EventBody("A"), SecretA);
        using var b = await PostAsync(http, "/line/webhook/b", EventBody("B"), SecretB);

        Assert.AreEqual(HttpStatusCode.OK, a.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, b.StatusCode);
        Assert.AreEqual("a,b", string.Join(",", seen));
    }

    [TestMethod]
    public async Task RouteParameter_SignatureOfAnotherChannel_Is401_AndHandlerNotCalled()
    {
        var handled = 0;
        using var host = await StartAsync(
            endpoints => endpoints.MapLineWebhook(
                "/line/webhook/{channel}",
                (_, _, _) =>
                {
                    handled++;
                    return Task.CompletedTask;
                },
                new LineWebhookEndpointOptions { ChannelRouteParameter = "channel" }));
        using var http = host.GetTestClient();

        // 用 b 的密鑰簽,卻打到 a 的路徑:各 channel 的密鑰不能互通。
        using var response = await PostAsync(http, "/line/webhook/a", EventBody("X"), SecretB);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.AreEqual(0, handled);
    }

    [TestMethod]
    public async Task UnknownChannel_Is401_BeforeAnyStateIsTouched()
    {
        var handled = 0;
        var spy = new SpyOptionsMonitor(new Dictionary<string, string> { ["a"] = SecretA, ["b"] = SecretB });
        var bodyReads = -1;
        using var host = await StartAsync(
            endpoints => endpoints.MapLineWebhook(
                "/line/webhook/{channel}",
                (_, _, _) =>
                {
                    handled++;
                    return Task.CompletedTask;
                },
                new LineWebhookEndpointOptions { ChannelRouteParameter = "channel" }),
            services => services.AddSingleton<IOptionsMonitor<LineMessagingOptions>>(spy),
            observe: context =>
            {
                bodyReads = ((CountingStream)context.Request.Body).Reads;
                Assert.IsFalse(context.Items.ContainsKey(LineWebhookItems.ChannelName), "驗簽之前不放 channel 名稱。");
            });
        using var http = host.GetTestClient();

        // 連「用空密鑰簽」的請求也一樣被擋:未知的 channel 不能拿空密鑰去驗簽。
        var body = EventBody("X");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/line/webhook/ghost", UriKind.Relative))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(
            LineWebhookHttpRequestExtensions.SignatureHeaderName,
            Convert.ToBase64String(System.Security.Cryptography.HMACSHA256.HashData(ReadOnlySpan<byte>.Empty, Encoding.UTF8.GetBytes(body))));
        using var response = await http.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.AreEqual(0, handled);
        Assert.AreEqual(0, spy.Names.Count, "名稱不在清單內,連選項都不該去取。");
        Assert.AreEqual(0, bodyReads, "也不該讀請求內容。");
    }

    [TestMethod]
    public async Task FixedChannelName_UsesThatChannelsSecret_AndRejectsOthers()
    {
        var seen = new List<string>();
        using var host = await StartAsync(
            endpoints => endpoints.MapLineWebhook(
                "/line/webhook",
                (_, context, _) =>
                {
                    seen.Add((string)context.Items[LineWebhookItems.ChannelName]!);
                    return Task.CompletedTask;
                },
                new LineWebhookEndpointOptions { ChannelName = "b" }));
        using var http = host.GetTestClient();

        using var ok = await PostAsync(http, "/line/webhook", EventBody("B"), SecretB);
        using var wrong = await PostAsync(http, "/line/webhook", EventBody("A"), SecretA);

        Assert.AreEqual(HttpStatusCode.OK, ok.StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.AreEqual("b", string.Join(",", seen));
    }

    [TestMethod]
    public async Task FixedChannelName_NotRegistered_Is401()
    {
        var handled = 0;
        using var host = await StartAsync(
            endpoints => endpoints.MapLineWebhook(
                "/line/webhook",
                (_, _, _) =>
                {
                    handled++;
                    return Task.CompletedTask;
                },
                new LineWebhookEndpointOptions { ChannelName = "ghost" }));
        using var http = host.GetTestClient();

        using var response = await PostAsync(http, "/line/webhook", EventBody("X"), SecretA);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.AreEqual(0, handled);
    }

    [TestMethod]
    public async Task EventOverloads_PutTheChannelNameInItems_AndTheDelegateSeesIt()
    {
        var seen = new List<string>();
        using var host = await StartAsync(
            endpoints => endpoints.MapLineWebhook(
                "/line/webhook/{channel}",
                options => options.ChannelRouteParameter = "channel",
                (_, context, _) =>
                {
                    seen.Add((string)context.Items[LineWebhookItems.ChannelName]!);
                    return Task.CompletedTask;
                }));
        using var http = host.GetTestClient();

        (await PostAsync(http, "/line/webhook/a", EventBody("A"), SecretA)).Dispose();
        (await PostAsync(http, "/line/webhook/b", EventBody("B"), SecretB)).Dispose();

        Assert.AreEqual("a,b", string.Join(",", seen));
    }

    [TestMethod]
    public async Task GenericHandler_ContextCarriesTheChannelNameAndThatChannelsClient()
    {
        var probe = new ChannelProbe();
        using var host = await StartAsync(
            endpoints => endpoints.MapLineWebhook<ProbeHandler>(
                "/line/webhook/{channel}",
                new LineWebhookEndpointOptions { ChannelRouteParameter = "channel" }),
            services =>
            {
                services.AddSingleton(probe);
                services.AddLineWebhookHandler<ProbeHandler>();
            });
        using var http = host.GetTestClient();

        (await PostAsync(http, "/line/webhook/a", EventBody("A"), SecretA)).Dispose();
        (await PostAsync(http, "/line/webhook/b", EventBody("B"), SecretB)).Dispose();

        Assert.AreEqual("a|a,b|b", string.Join(",", probe.Seen));
    }

    [TestMethod]
    public async Task NoChannelOptions_BehavesAsBefore_DefaultChannelWithEmptyName()
    {
        var (client, _) = RecordingLineMessagingClient.Create();
        var probe = new ChannelProbe();
        string? itemName = null;
        using var host = await Tests.TestSupport.WebhookTestHost.StartAsync(
            client,
            services =>
            {
                services.AddSingleton(probe);
                services.AddLineWebhookHandler<NameOnlyHandler>();
            },
            endpoints => endpoints.MapLineWebhook<NameOnlyHandler>("/line/webhook"),
            items => itemName = items.TryGetValue(LineWebhookItems.ChannelName, out var value) ? (string?)value : null);
        using var http = host.GetTestClient();

        using var response = await Tests.TestSupport.WebhookTestHost.PostAsync(http, Tests.TestSupport.WebhookTestHost.TextEventBody());

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(string.Empty, itemName, "預設 channel 的名稱是空字串,而且仍然放進 Items。");
        Assert.AreEqual(1, probe.Seen.Count);
        Assert.AreEqual(string.Empty, probe.Seen[0], "脈絡的 ChannelName 是空字串。");
    }

    [TestMethod]
    public async Task OptionalRouteParameterLeftOut_MeansTheDefaultChannel()
    {
        var seen = new List<string>();
        using var host = await StartAsync(
            endpoints => endpoints.MapLineWebhook(
                "/line/webhook/{channel?}",
                (_, context, _) =>
                {
                    seen.Add((string)context.Items[LineWebhookItems.ChannelName]!);
                    return Task.CompletedTask;
                },
                new LineWebhookEndpointOptions { ChannelRouteParameter = "channel" }),
            services => services.AddLineMessaging(o =>
            {
                o.ChannelAccessToken = "default-token";
                o.ChannelSecret = "secret-of-default";
            }));
        using var http = host.GetTestClient();

        using var response = await PostAsync(http, "/line/webhook", EventBody("D"), "secret-of-default");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(string.Empty, string.Join(",", seen));
    }

    [TestMethod]
    public async Task BothChannelNameAndRouteParameter_ThrowWhileMapping()
    {
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => StartAsync(
            endpoints => endpoints.MapLineWebhook(
                "/line/webhook/{channel}",
                (_, _, _) => Task.CompletedTask,
                new LineWebhookEndpointOptions { ChannelName = "a", ChannelRouteParameter = "channel" })));

        StringAssert.Contains(thrown.Message, "ChannelName");
    }

    [TestMethod]
    public async Task BothChannelNameAndRouteParameter_AlsoThrowOnTheGenericAndEventOverloads()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => StartAsync(
            endpoints => endpoints.MapLineWebhook<ProbeHandler>(
                "/line/webhook/{channel}",
                new LineWebhookEndpointOptions { ChannelName = "a", ChannelRouteParameter = "channel" }),
            services => services.AddLineWebhookHandler<ProbeHandler>()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => StartAsync(
            endpoints => endpoints.MapLineWebhook(
                "/line/webhook/{channel}",
                options =>
                {
                    options.ChannelName = "a";
                    options.ChannelRouteParameter = "channel";
                },
                (_, _, _) => Task.CompletedTask)));
    }

    [TestMethod]
    public async Task BlankRouteParameter_ThrowsWhileMapping()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => StartAsync(
            endpoints => endpoints.MapLineWebhook(
                "/line/webhook",
                (_, _, _) => Task.CompletedTask,
                new LineWebhookEndpointOptions { ChannelRouteParameter = " " })));
    }

    [TestMethod]
    public async Task ChannelSelectedWithoutMessagingRegistered_ThrowsWhileMapping()
    {
        // 沒有呼叫 AddLineMessaging:指定了 channel 卻沒有工廠可查,在掛端點當下就說清楚。
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            using var host = await new HostBuilder()
                .ConfigureWebHost(web => web
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();
                        services.AddLogging();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints => endpoints.MapLineWebhook(
                            "/line/webhook",
                            (_, _, _) => Task.CompletedTask,
                            new LineWebhookEndpointOptions { ChannelName = "a" }));
                    }))
                .StartAsync();
        });

        StringAssert.Contains(thrown.Message, nameof(ILineMessagingClientFactory));
    }

    [TestMethod]
    public async Task AutoReply_ReceivesTheChannelName()
    {
        var service = new ChannelAutoReplyService();
        using var host = await StartAsync(
            endpoints => endpoints.MapLineWebhook(
                "/line/webhook/{channel}",
                options =>
                {
                    options.ChannelRouteParameter = "channel";
                    options.AutoReply = true;
                },
                (_, _, _) => Task.CompletedTask),
            services => services.AddSingleton<ILineAutoReplyService>(service));
        using var http = host.GetTestClient();

        (await PostAsync(http, "/line/webhook/a", EventBody("A"), SecretA)).Dispose();
        (await PostAsync(http, "/line/webhook/b", EventBody("B"), SecretB)).Dispose();

        Assert.AreEqual("a,b", string.Join(",", service.Channels));
    }

    [TestMethod]
    public async Task Maintenance_RepliesThroughThatChannelsClient()
    {
        var (clientA, recorderA) = RecordingLineMessagingClient.Create();
        var (clientB, recorderB) = RecordingLineMessagingClient.Create();
        var handled = 0;
        using var host = await StartAsync(
            endpoints => endpoints.MapLineWebhook(
                "/line/webhook/{channel}",
                options =>
                {
                    options.ChannelRouteParameter = "channel";
                    options.Maintenance.Enabled = true;
                    options.Maintenance.ReplyText = "維護中";
                    options.Maintenance.AdminUserIds.Add("U-admin");
                },
                (_, _, _) =>
                {
                    handled++;
                    return Task.CompletedTask;
                }),
            services =>
            {
                // 最後註冊的贏:把各 channel 的 keyed 用戶端換成記錄用的假用戶端。
                services.AddKeyedSingleton("a", clientA);
                services.AddKeyedSingleton("b", clientB);
            });
        using var http = host.GetTestClient();

        (await PostAsync(http, "/line/webhook/b", EventBody("B"), SecretB)).Dispose();

        Assert.AreEqual(0, handled);
        Assert.AreEqual(0, recorderA.Sends.Count);
        Assert.AreEqual("維護中", recorderB.Sends.Single().Text, "維護中的回覆要走該事件所屬 channel 的用戶端。");
    }

    [TestMethod]
    public async Task ErrorNotification_PushesThroughThatChannelsClient()
    {
        var (clientA, recorderA) = RecordingLineMessagingClient.Create();
        var (clientB, recorderB) = RecordingLineMessagingClient.Create();
        using var host = await StartAsync(
            endpoints => endpoints.MapLineWebhook(
                "/line/webhook/{channel}",
                options =>
                {
                    options.ChannelRouteParameter = "channel";
                    options.ErrorNotification.Enabled = true;
                    options.ErrorNotification.AdminUserIds.Add("U-admin");
                },
                (_, _, _) => throw new InvalidOperationException("炸了")),
            services =>
            {
                services.AddKeyedSingleton("a", clientA);
                services.AddKeyedSingleton("b", clientB);
            });
        using var http = host.GetTestClient();

        (await PostAsync(http, "/line/webhook/a", EventBody("A"), SecretA)).Dispose();

        Assert.AreEqual(1, recorderA.Sends.Count);
        Assert.AreEqual(0, recorderB.Sends.Count);
    }

    [TestMethod]
    public async Task Context_ResolvesPostbackData_ThroughTheStoreInDi()
    {
        var (client, _) = RecordingLineMessagingClient.Create();
        var probe = new ChannelProbe();
        var store = new InMemoryLinePostbackDataStore();
        var created = await store.StoreAsync("很長的資料", TimeSpan.FromMinutes(5));
        var token = created.GetValueOrThrow();

        using var host = await Tests.TestSupport.WebhookTestHost.StartAsync(
            client,
            services =>
            {
                services.AddSingleton(probe);
                services.AddSingleton<ILinePostbackDataStore>(store);
                services.AddLineWebhookHandler<PostbackHandler>();
            },
            endpoints => endpoints.MapLineWebhook<PostbackHandler>("/line/webhook"));
        using var http = host.GetTestClient();

        var body = PostbackBody(token);
        using var response = await Tests.TestSupport.WebhookTestHost.PostAsync(http, body);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("很長的資料", string.Join(",", probe.Seen));
    }

    [TestMethod]
    public async Task Context_PostbackTokenWithoutStore_FailsWithNotConfigured()
    {
        var (client, _) = RecordingLineMessagingClient.Create();
        var probe = new ChannelProbe();
        using var host = await Tests.TestSupport.WebhookTestHost.StartAsync(
            client,
            services =>
            {
                services.AddSingleton(probe);
                services.AddLineWebhookHandler<PostbackHandler>();
            },
            endpoints => endpoints.MapLineWebhook<PostbackHandler>("/line/webhook"));
        using var http = host.GetTestClient();

        using var response = await Tests.TestSupport.WebhookTestHost.PostAsync(
            http,
            PostbackBody("ozl:" + new string('a', 32)));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("failed:" + LineErrorCodes.NotConfigured, string.Join(",", probe.Seen));
    }

    private static string PostbackBody(string data) =>
        "{\"destination\":\"Ubot\",\"events\":[{\"type\":\"postback\",\"mode\":\"active\",\"timestamp\":1799999999000,\"webhookEventId\":\"01ABC\",\"deliveryContext\":{\"isRedelivery\":false},\"replyToken\":\"rt-1\",\"source\":{\"type\":\"user\",\"userId\":\"U1\"},\"postback\":{\"data\":\"" + data + "\"}}]}";

    private static string EventBody(string tag) =>
        "{\"destination\":\"Ubot\",\"events\":[{\"type\":\"message\",\"mode\":\"active\",\"timestamp\":1799999999000,\"webhookEventId\":\"ev-" + tag + "\",\"deliveryContext\":{\"isRedelivery\":false},\"replyToken\":\"rt-" + tag + "\",\"source\":{\"type\":\"user\",\"userId\":\"U1\"},\"message\":{\"id\":\"m-1\",\"type\":\"text\",\"text\":\"嗨\"}}]}";

    private static async Task<HttpResponseMessage> PostAsync(HttpClient http, string path, string body, string secret)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(
            LineWebhookHttpRequestExtensions.SignatureHeaderName,
            LineWebhookSignature.Compute(secret, Encoding.UTF8.GetBytes(body)));

        return await http.SendAsync(request);
    }

    private static async Task<IHost> StartAsync(
        Action<IEndpointRouteBuilder> map,
        Action<IServiceCollection>? register = null,
        Action<HttpContext>? observe = null)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddLogging();
                    services.AddLineMessaging("a", o =>
                    {
                        o.ChannelAccessToken = "token-a";
                        o.ChannelSecret = SecretA;
                    });
                    services.AddLineMessaging("b", o =>
                    {
                        o.ChannelAccessToken = "token-b";
                        o.ChannelSecret = SecretB;
                    });
                    register?.Invoke(services);
                })
                .Configure(app =>
                {
                    app.Use(async (context, next) =>
                    {
                        context.Request.Body = new CountingStream(context.Request.Body);
                        await next();
                        observe?.Invoke(context);
                    });
                    app.UseRouting();
                    app.UseEndpoints(endpoints => map(endpoints));
                }))
            .Build();

        await host.StartAsync();
        return host;
    }

    /// <summary>
    /// 計算被讀了幾次的串流。
    /// A stream that counts how many times it was read.
    /// </summary>
    private sealed class CountingStream(Stream inner) : Stream
    {
        internal int Reads { get; private set; }

        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Reads++;
            return inner.Read(buffer, offset, count);
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Reads++;
            return await inner.ReadAsync(buffer, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>
    /// 記錄「向它要過哪些名稱」的選項監看器。
    /// An options monitor that records which names were asked of it.
    /// </summary>
    private sealed class SpyOptionsMonitor(IReadOnlyDictionary<string, string> secrets) : IOptionsMonitor<LineMessagingOptions>
    {
        private readonly List<string> _names = [];

        internal List<string> Names => _names;

        public LineMessagingOptions CurrentValue => Get(string.Empty);

        public LineMessagingOptions Get(string? name)
        {
            _names.Add(name ?? string.Empty);
            return new LineMessagingOptions
            {
                ChannelAccessToken = "spy-token",
                ChannelSecret = secrets.TryGetValue(name ?? string.Empty, out var secret) ? secret : string.Empty,
            };
        }

        public IDisposable? OnChange(Action<LineMessagingOptions, string?> listener) => null;
    }

    /// <summary>
    /// 記錄處理常式看到的脈絡內容。
    /// Records what a handler saw on its context.
    /// </summary>
    private sealed class ChannelProbe
    {
        private readonly object _gate = new();
        private readonly List<string> _seen = [];

        internal List<string> Seen
        {
            get
            {
                lock (_gate)
                {
                    return [.. _seen];
                }
            }
        }

        internal void Add(string value)
        {
            lock (_gate)
            {
                _seen.Add(value);
            }
        }
    }

    private sealed class ProbeHandler(ChannelProbe probe) : LineWebhookHandler
    {
        protected override Task OnMessageAsync(LineWebhookEvent webhookEvent, LineWebhookContext context, CancellationToken cancellationToken)
        {
            probe.Add(context.ChannelName + "|" + context.Messaging.ChannelName);
            return Task.CompletedTask;
        }
    }

    private sealed class NameOnlyHandler(ChannelProbe probe) : LineWebhookHandler
    {
        protected override Task OnMessageAsync(LineWebhookEvent webhookEvent, LineWebhookContext context, CancellationToken cancellationToken)
        {
            probe.Add(context.ChannelName);
            return Task.CompletedTask;
        }
    }

    private sealed class PostbackHandler(ChannelProbe probe) : LineWebhookHandler
    {
        protected override async Task OnPostbackAsync(LineWebhookEvent webhookEvent, LineWebhookContext context, CancellationToken cancellationToken)
        {
            var resolved = await context.ResolvePostbackDataAsync(cancellationToken);
            probe.Add(resolved.IsSuccess ? resolved.GetValueOrThrow() : "failed:" + resolved.Error.Code);
        }
    }

    private sealed class ChannelAutoReplyService : ILineAutoReplyService
    {
        private readonly List<string> _channels = [];

        internal List<string> Channels => _channels;

        public Task<Result<LineAutoReplyOutcome>> HandleAsync(LineWebhookEvent webhookEvent, CancellationToken cancellationToken = default) =>
            HandleAsync(webhookEvent, string.Empty, cancellationToken);

        public Task<Result<LineAutoReplyOutcome>> HandleAsync(LineWebhookEvent webhookEvent, string channelName, CancellationToken cancellationToken = default)
        {
            _channels.Add(channelName);
            return Task.FromResult(Result.Success(LineAutoReplyOutcome.Skipped));
        }
    }
}
