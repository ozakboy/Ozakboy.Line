using System.Net;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Ozakboy.Line.AspNetCore.Tests.TestSupport;
using Ozakboy.Line.AspNetCore.Webhook;
using Ozakboy.Line.AutoReply;
using Ozakboy.Line.Tests.TestSupport;
using Ozakboy.Line.Webhook.Handling;

namespace Ozakboy.Line.AspNetCore.Tests;

/// <summary>
/// <c>MapLineWebhook&lt;THandler&gt;</c> 的整合測試。
/// Integration tests for <c>MapLineWebhook&lt;THandler&gt;</c>.
/// </summary>
[TestClass]
public sealed class LineWebhookHandlerEndpointTests
{
    private static readonly string[] MessageThenTextCalls = ["message", "text:你好"];
    private static readonly string[] OnlyGoodCalls = ["好"];
    private static readonly string[] OutcomeRuleThenNoneCalls = ["outcome:rule-1", "outcome:none"];
    private static readonly string[] OutcomeNoneCalls = ["outcome:none"];

    [TestMethod]
    public async Task Handler_IsResolvedFromDi_AndDispatchesWithAWorkingContext()
    {
        var (client, recorder) = RecordingLineMessagingClient.Create();
        var probe = new Probe();
        using var host = await WebhookTestHost.StartAsync(
            client,
            services =>
            {
                services.AddSingleton(probe);
                services.AddLineWebhookHandler<EchoHandler>();
            },
            endpoints => endpoints.MapLineWebhook<EchoHandler>("/line/webhook"));
        using var http = host.GetTestClient();

        using var response = await WebhookTestHost.PostAsync(http, WebhookTestHost.TextEventBody(text: "你好"));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        CollectionAssert.AreEqual(MessageThenTextCalls, probe.Calls);
        var reply = recorder.Sends.Single();
        Assert.AreEqual("ReplyTextAsync", reply.Method);
        Assert.AreEqual("rt-1", reply.Target);
        Assert.AreEqual("收到:你好", reply.Text);
    }

    [TestMethod]
    public async Task Handler_IsScopedPerRequest()
    {
        var (client, _) = RecordingLineMessagingClient.Create();
        var probe = new Probe();
        using var host = await WebhookTestHost.StartAsync(
            client,
            services =>
            {
                services.AddSingleton(probe);
                services.AddLineWebhookHandler<EchoHandler>();
            },
            endpoints => endpoints.MapLineWebhook<EchoHandler>("/line/webhook"));
        using var http = host.GetTestClient();

        (await WebhookTestHost.PostAsync(http, WebhookTestHost.TextEventBody())).Dispose();
        (await WebhookTestHost.PostAsync(http, WebhookTestHost.TextEventBody())).Dispose();

        Assert.AreEqual(2, probe.InstanceIds.Distinct().Count(), "每個請求一個 scope,因此各自取得新的 scoped 處理常式。");
    }

    [TestMethod]
    public async Task HandlerThrows_StillAnswers200_AndLaterEventsAreHandled()
    {
        const string TwoEvents = """
        {"destination":"Ubot","events":[
          {"type":"message","timestamp":1,"webhookEventId":"a","replyToken":"rt-1","source":{"type":"user","userId":"U1"},"message":{"id":"m-1","type":"text","text":"炸"}},
          {"type":"message","timestamp":2,"webhookEventId":"b","replyToken":"rt-2","source":{"type":"user","userId":"U2"},"message":{"id":"m-2","type":"text","text":"好"}}
        ]}
        """;
        var (client, _) = RecordingLineMessagingClient.Create();
        var probe = new Probe();
        using var host = await WebhookTestHost.StartAsync(
            client,
            services =>
            {
                services.AddSingleton(probe);
                services.AddLineWebhookHandler<ThrowOnBoomHandler>();
            },
            endpoints => endpoints.MapLineWebhook<ThrowOnBoomHandler>("/line/webhook"));
        using var http = host.GetTestClient();

        using var response = await WebhookTestHost.PostAsync(http, TwoEvents);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "例外隔離與狀態碼契約不變。");
        CollectionAssert.AreEqual(OnlyGoodCalls, probe.Calls);
    }

    [TestMethod]
    public async Task WrongSignature_Answers401_AndNeverReachesTheHandler()
    {
        var (client, _) = RecordingLineMessagingClient.Create();
        var probe = new Probe();
        using var host = await WebhookTestHost.StartAsync(
            client,
            services =>
            {
                services.AddSingleton(probe);
                services.AddLineWebhookHandler<EchoHandler>();
            },
            endpoints => endpoints.MapLineWebhook<EchoHandler>("/line/webhook"));
        using var http = host.GetTestClient();

        using var unsigned = await WebhookTestHost.PostAsync(http, WebhookTestHost.TextEventBody(), sign: false);
        using var broken = await WebhookTestHost.PostAsync(http, "{not json");

        Assert.AreEqual(HttpStatusCode.Unauthorized, unsigned.StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, broken.StatusCode);
        Assert.AreEqual(0, probe.Calls.Count);
    }

    [TestMethod]
    public async Task AutoReplyOutcome_ReachesTheContext_AndDoesNotLeakIntoTheNextEvent()
    {
        const string TwoEvents = """
        {"destination":"Ubot","events":[
          {"type":"message","timestamp":1,"webhookEventId":"a","replyToken":"rt-1","source":{"type":"user","userId":"U1"},"message":{"id":"m-1","type":"text","text":"一"}},
          {"type":"message","timestamp":2,"webhookEventId":"b","replyToken":"rt-2","source":{"type":"user","userId":"U2"},"message":{"id":"m-2","type":"text","text":"二"}}
        ]}
        """;
        var (client, _) = RecordingLineMessagingClient.Create();
        var probe = new Probe();
        var autoReply = new SequenceAutoReplyService(
            Result.Success(LineAutoReplyOutcome.Replied("rule-1")),
            Result.Failure<LineAutoReplyOutcome>(Error.Validation("x.failed", "第二個失敗")));
        var options = new LineWebhookEndpointOptions { AutoReply = true };
        using var host = await WebhookTestHost.StartAsync(
            client,
            services =>
            {
                services.AddSingleton(probe);
                services.AddSingleton<ILineAutoReplyService>(autoReply);
                services.AddLineWebhookHandler<OutcomeHandler>();
            },
            endpoints => endpoints.MapLineWebhook<OutcomeHandler>("/line/webhook", options));
        using var http = host.GetTestClient();

        using var response = await WebhookTestHost.PostAsync(http, TwoEvents);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        CollectionAssert.AreEqual(OutcomeRuleThenNoneCalls, probe.Calls, "自動回覆失敗時不放值,不能讀到上一個事件留下的結果。");
    }

    [TestMethod]
    public async Task AutoReplyDisabled_ContextOutcomeIsNull()
    {
        var (client, _) = RecordingLineMessagingClient.Create();
        var probe = new Probe();
        using var host = await WebhookTestHost.StartAsync(
            client,
            services =>
            {
                services.AddSingleton(probe);
                services.AddLineWebhookHandler<OutcomeHandler>();
            },
            endpoints => endpoints.MapLineWebhook<OutcomeHandler>("/line/webhook"));
        using var http = host.GetTestClient();

        (await WebhookTestHost.PostAsync(http, WebhookTestHost.TextEventBody())).Dispose();

        CollectionAssert.AreEqual(OutcomeNoneCalls, probe.Calls);
    }

    [TestMethod]
    public async Task UnregisteredHandler_ThrowsWhileMapping()
    {
        var (client, _) = RecordingLineMessagingClient.Create();

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            WebhookTestHost.StartAsync(
                client,
                registerServices: null,
                endpoints => endpoints.MapLineWebhook<EchoHandler>("/line/webhook")));

        StringAssert.Contains(exception.Message, "AddLineWebhookHandler", "訊息要直接說出該呼叫哪一個方法。");
    }

    [TestMethod]
    public void AddLineWebhookHandler_UsesTryAdd_AndRegistersScoped()
    {
        var services = new ServiceCollection();
        services.AddSingleton<EchoHandler>();

        services.AddLineWebhookHandler<EchoHandler>();
        services.AddLineWebhookHandler<ThrowOnBoomHandler>();

        Assert.AreEqual(ServiceLifetime.Singleton, services.Single(d => d.ServiceType == typeof(EchoHandler)).Lifetime, "宿主先註冊的要被尊重。");
        Assert.AreEqual(ServiceLifetime.Scoped, services.Single(d => d.ServiceType == typeof(ThrowOnBoomHandler)).Lifetime);
        Assert.ThrowsExactly<ArgumentNullException>(() => LineWebhookServiceCollectionExtensions.AddLineWebhookHandler<EchoHandler>(null!));
    }

    [TestMethod]
    public async Task NullArguments_Throw()
    {
        var (client, _) = RecordingLineMessagingClient.Create();
        using var host = await WebhookTestHost.StartAsync(
            client,
            services => services.AddLineWebhookHandler<EchoHandler>(),
            endpoints =>
            {
                Assert.ThrowsExactly<ArgumentNullException>(() => endpoints.MapLineWebhook<EchoHandler>("/x", null!));
                Assert.ThrowsExactly<ArgumentNullException>(() => endpoints.MapLineWebhook<EchoHandler>(null!, new LineWebhookEndpointOptions()));
                endpoints.MapLineWebhook<EchoHandler>("/line/webhook");
            });
    }

    /// <summary>
    /// 在測試與處理常式之間傳遞觀察結果。
    /// Carries observations between the test and the handlers.
    /// </summary>
    private sealed class Probe
    {
        private readonly object _gate = new();
        private readonly List<string> _calls = [];
        private readonly List<Guid> _instanceIds = [];

        internal List<string> Calls
        {
            get
            {
                lock (_gate)
                {
                    return [.. _calls];
                }
            }
        }

        internal IReadOnlyList<Guid> InstanceIds
        {
            get
            {
                lock (_gate)
                {
                    return [.. _instanceIds];
                }
            }
        }

        internal void Add(string call)
        {
            lock (_gate)
            {
                _calls.Add(call);
            }
        }

        internal void AddInstance(Guid id)
        {
            lock (_gate)
            {
                _instanceIds.Add(id);
            }
        }
    }

    /// <summary>
    /// 記錄呼叫順序並回覆收到的文字。
    /// Records call order and replies with the received text.
    /// </summary>
    private sealed class EchoHandler(Probe probe) : LineWebhookHandler
    {
        private readonly Guid _id = Guid.NewGuid();

        protected override Task OnMessageAsync(LineWebhookEvent webhookEvent, LineWebhookContext context, CancellationToken cancellationToken)
        {
            probe.Add("message");
            probe.AddInstance(_id);
            return Task.CompletedTask;
        }

        protected override async Task OnTextMessageAsync(LineWebhookEvent webhookEvent, LineWebhookContext context, CancellationToken cancellationToken)
        {
            probe.Add("text:" + webhookEvent.Message!.Text);
            await context.ReplyTextAsync("收到:" + webhookEvent.Message.Text, cancellationToken);
        }
    }

    /// <summary>
    /// 遇到「炸」就擲例外。
    /// Throws on the text 炸.
    /// </summary>
    private sealed class ThrowOnBoomHandler(Probe probe) : LineWebhookHandler
    {
        protected override Task OnTextMessageAsync(LineWebhookEvent webhookEvent, LineWebhookContext context, CancellationToken cancellationToken)
        {
            if (webhookEvent.Message!.Text == "炸")
            {
                throw new InvalidOperationException("這個事件壞了");
            }

            probe.Add(webhookEvent.Message.Text!);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 記錄脈絡上的自動回覆結果。
    /// Records the auto reply outcome seen on the context.
    /// </summary>
    private sealed class OutcomeHandler(Probe probe) : LineWebhookHandler
    {
        protected override Task OnMessageAsync(LineWebhookEvent webhookEvent, LineWebhookContext context, CancellationToken cancellationToken)
        {
            probe.Add(context.AutoReplyOutcome is { } outcome ? "outcome:" + outcome.RuleId : "outcome:none");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 依序回傳安排好的結果。
    /// Answers with arranged results in order.
    /// </summary>
    private sealed class SequenceAutoReplyService(params Result<LineAutoReplyOutcome>[] results) : ILineAutoReplyService
    {
        private int _next;

        public Task<Result<LineAutoReplyOutcome>> HandleAsync(LineWebhookEvent webhookEvent, CancellationToken cancellationToken = default) =>
            Task.FromResult(results[Math.Min(_next++, results.Length - 1)]);
    }
}
