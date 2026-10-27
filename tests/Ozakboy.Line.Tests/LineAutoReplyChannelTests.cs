using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ozakboy.Line.AutoReply;
using Ozakboy.Line.Messaging;
using Ozakboy.Line.Tests.TestSupport;
using Ozakboy.Line.Webhook;

namespace Ozakboy.Line.Tests;

/// <summary>
/// 自動回覆的多 channel 行為:規則的適用範圍、各 channel 用自己的用戶端回覆、舊建構子與舊資料的相容。
/// Multi-channel auto reply: the scope of a rule, each channel replying through its own client, and compatibility
/// with the old constructor and old data.
/// </summary>
[TestClass]
public sealed class LineAutoReplyChannelTests
{
    private const string Reply = """[{"type":"text","text":"收到:{{text}}"}]""";

    [TestMethod]
    public void Rule_ChannelName_DefaultsToNull_MeaningEveryChannel()
    {
        Assert.IsNull(new LineAutoReplyRule().ChannelName);
    }

    [TestMethod]
    public void ForChannel_KeepsEmptyAndMatchingRules_InTheirOriginalOrder()
    {
        var rules = new List<LineAutoReplyRule>
        {
            Rule("全部-null", channel: null),
            Rule("a", channel: "a"),
            Rule("全部-空字串", channel: string.Empty),
            Rule("b", channel: "b"),
            Rule("A 大寫", channel: "A"),
        };

        var forA = LineAutoReplyMatcher.ForChannel(rules, "a");
        var forDefault = LineAutoReplyMatcher.ForChannel(rules, string.Empty);

        Assert.AreEqual("全部-null,a,全部-空字串", string.Join(",", forA.Select(rule => rule.Name)), "channel 名稱是序數比較。");
        Assert.AreEqual("全部-null,全部-空字串", string.Join(",", forDefault.Select(rule => rule.Name)), "預設 channel 只看到「所有 channel」的規則。");
    }

    [TestMethod]
    public async Task HandleAsync_OnlyRulesOfThatChannel_AndRepliesThroughThatChannelsClient()
    {
        var harness = Harness.Create("a", "b");
        await harness.Store.UpsertAsync(Rule("給 a", channel: "a"));
        await harness.Store.UpsertAsync(Rule("給 b", channel: "b", messages: """[{"type":"text","text":"B 說:{{text}}"}]"""));

        var outcomeA = await harness.Service.HandleAsync(TextEvent("嗨"), "a");
        var outcomeB = await harness.Service.HandleAsync(TextEvent("嗨"), "b");

        Assert.AreEqual(LineAutoReplyOutcomeKind.Replied, outcomeA.GetValueOrThrow().Kind);
        Assert.AreEqual(LineAutoReplyOutcomeKind.Replied, outcomeB.GetValueOrThrow().Kind);
        Assert.AreEqual(1, harness.Clients["a"].Replies.Count);
        StringAssert.Contains(harness.Clients["a"].Replies[0].MessagesJson, "收到:嗨");
        Assert.AreEqual(1, harness.Clients["b"].Replies.Count);
        StringAssert.Contains(harness.Clients["b"].Replies[0].MessagesJson, "B 說:嗨");
    }

    [TestMethod]
    public async Task HandleAsync_AHigherPriorityRuleOfAnotherChannel_DoesNotMaskThisChannelsRule()
    {
        // 挑選順序契約不變:只是比對前先排除不適用的規則,b 的高優先規則不能蓋掉 a 的規則。
        var harness = Harness.Create("a", "b");
        await harness.Store.UpsertAsync(Rule("b 的高優先", channel: "b", priority: 1));
        await harness.Store.UpsertAsync(Rule("a 的低優先", channel: "a", priority: 500));

        var outcome = await harness.Service.HandleAsync(TextEvent("嗨"), "a");

        Assert.AreEqual(LineAutoReplyOutcomeKind.Replied, outcome.GetValueOrThrow().Kind);
        Assert.AreEqual(1, harness.Clients["a"].Replies.Count);
        Assert.AreEqual(0, harness.Clients["b"].Replies.Count);
    }

    [TestMethod]
    public async Task HandleAsync_RuleWithoutChannel_AppliesToEveryChannel()
    {
        var harness = Harness.Create("a", "b");
        await harness.Store.UpsertAsync(Rule("通用", channel: null));

        (await harness.Service.HandleAsync(TextEvent("嗨"), "a")).GetValueOrThrow();
        (await harness.Service.HandleAsync(TextEvent("嗨"), "b")).GetValueOrThrow();

        Assert.AreEqual(1, harness.Clients["a"].Replies.Count);
        Assert.AreEqual(1, harness.Clients["b"].Replies.Count);
    }

    [TestMethod]
    public async Task HandleAsync_FollowAndFallbackAreScopedToTheChannelToo()
    {
        var harness = Harness.Create("a", "b");
        await harness.Store.UpsertAsync(Rule("a 歡迎", LineAutoReplyMatchMode.Follow, channel: "a"));
        await harness.Store.UpsertAsync(Rule("b 備援", LineAutoReplyMatchMode.Fallback, channel: "b"));

        var followOnB = await harness.Service.HandleAsync(FollowEvent(), "b");
        var followOnA = await harness.Service.HandleAsync(FollowEvent(), "a");
        var textOnA = await harness.Service.HandleAsync(TextEvent("沒人認得的話"), "a");
        var textOnB = await harness.Service.HandleAsync(TextEvent("沒人認得的話"), "b");

        Assert.AreEqual(LineAutoReplyOutcomeKind.Skipped, followOnB.GetValueOrThrow().Kind);
        Assert.AreEqual(LineAutoReplyOutcomeKind.Replied, followOnA.GetValueOrThrow().Kind);
        Assert.AreEqual(LineAutoReplyOutcomeKind.NoMatch, textOnA.GetValueOrThrow().Kind);
        Assert.AreEqual(LineAutoReplyOutcomeKind.Replied, textOnB.GetValueOrThrow().Kind);
    }

    [TestMethod]
    public async Task HandleAsync_DisplayNameIsLookedUpWithThatChannelsClient()
    {
        var harness = Harness.Create("a", "b");
        harness.Clients["a"].ProfileDisplayName = "甲店店長";
        harness.Clients["b"].ProfileDisplayName = "乙店店長";
        await harness.Store.UpsertAsync(Rule("問候", messages: """[{"type":"text","text":"{{displayName}} 您好"}]"""));

        (await harness.Service.HandleAsync(TextEvent("嗨"), "b")).GetValueOrThrow();

        StringAssert.Contains(harness.Clients["b"].Replies.Single().MessagesJson, "乙店店長 您好");
        Assert.AreEqual(0, harness.Clients["a"].Replies.Count);
    }

    [TestMethod]
    public async Task HandleAsync_UnknownChannel_FailsWithNotConfigured_AndSendsNothing()
    {
        var harness = Harness.Create("a");
        await harness.Store.UpsertAsync(Rule("通用"));

        var outcome = await harness.Service.HandleAsync(TextEvent("嗨"), "ghost");

        Assert.IsTrue(outcome.IsFailure);
        Assert.AreEqual(LineErrorCodes.NotConfigured, outcome.Error!.Code);
        Assert.AreEqual(0, harness.Clients["a"].Replies.Count);
    }

    [TestMethod]
    public async Task HandleAsync_EventsNobodyHandles_StaySkippedEvenForAnUnknownChannel()
    {
        // 不是文字也不是加好友的事件本來就不處理,不該因為 channel 名稱而變成錯誤。
        var harness = Harness.Create("a");

        var outcome = await harness.Service.HandleAsync(
            new LineWebhookEvent
            {
                Type = LineWebhookEventTypes.Postback,
                Mode = "active",
                ReplyToken = "rt-1",
                Source = new LineWebhookSource { Type = "user", UserId = "U1" },
            },
            "ghost");

        Assert.AreEqual(LineAutoReplyOutcomeKind.Skipped, outcome.GetValueOrThrow().Kind);
    }

    [TestMethod]
    public async Task SingleArgumentOverload_MeansTheDefaultChannel()
    {
        var harness = Harness.Create(string.Empty, "a");
        await harness.Store.UpsertAsync(Rule("給 a", channel: "a"));
        await harness.Store.UpsertAsync(Rule("通用", channel: null, priority: 200));

        (await harness.Service.HandleAsync(TextEvent("嗨"))).GetValueOrThrow();

        Assert.AreEqual(1, harness.Clients[string.Empty].Replies.Count);
        Assert.AreEqual(0, harness.Clients["a"].Replies.Count);
    }

    [TestMethod]
    public async Task LegacyConstructor_ServesTheDefaultChannelOnly()
    {
        var messaging = new FakeLineMessagingClient();
        var store = new InMemoryLineAutoReplyStore();
        var service = new LineAutoReplyService(store, messaging, Options.Create(new LineAutoReplyOptions()));
        await store.UpsertAsync(Rule("通用"));

        var onDefault = await service.HandleAsync(TextEvent("嗨"));
        var onNamed = await service.HandleAsync(TextEvent("嗨"), "a");

        Assert.AreEqual(LineAutoReplyOutcomeKind.Replied, onDefault.GetValueOrThrow().Kind);
        Assert.AreEqual(LineErrorCodes.NotConfigured, onNamed.Error!.Code);
        Assert.AreEqual(1, messaging.Replies.Count, "具名 channel 不會被悄悄當成預設 channel 處理。");
    }

    [TestMethod]
    public void Constructors_RejectNulls()
    {
        var store = new InMemoryLineAutoReplyStore();
        var options = Options.Create(new LineAutoReplyOptions());

        Assert.Throws<ArgumentNullException>(() => new LineAutoReplyService(store, (ILineMessagingClientFactory)null!, options));
        Assert.Throws<ArgumentNullException>(() => new LineAutoReplyService(store, (ILineMessagingClient)null!, options));
    }

    [TestMethod]
    public async Task InterfaceDefault_ForAnImplementationThatKnowsOnlyTheOldMethod()
    {
        ILineAutoReplyService old = new OldStyleService();

        var onDefault = await old.HandleAsync(TextEvent("嗨"), string.Empty);
        var onNamed = await old.HandleAsync(TextEvent("嗨"), "a");

        Assert.AreEqual(LineAutoReplyOutcomeKind.NoMatch, onDefault.GetValueOrThrow().Kind, "空字串轉給單參數多載。");
        Assert.AreEqual(LineErrorCodes.NotConfigured, onNamed.Error!.Code, "其他名稱不會被悄悄當成預設 channel。");
    }

    [TestMethod]
    public async Task Di_ServiceUsesTheFactory_AndAnUnknownChannelFailsWithoutAnyRequest()
    {
        var services = new ServiceCollection();
        services.AddLineMessaging("a", o => o.ChannelAccessToken = "token-a");
        services.AddLineAutoReply();
        await using var provider = services.BuildServiceProvider();

        var service = provider.GetRequiredService<ILineAutoReplyService>();
        var outcome = await service.HandleAsync(TextEvent("嗨"), "ghost");

        Assert.AreEqual(LineErrorCodes.NotConfigured, outcome.Error!.Code);
    }

    [TestMethod]
    public async Task JsonFileStore_RoundTripsChannelName_AndReadsOldDataAsEveryChannel()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ozakboy-line-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var path = Path.Combine(directory, "rules.json");

            // 0.4.0 之前存下的檔案沒有 channelName 欄位。
            await File.WriteAllTextAsync(
                path,
                """[{"id":"old-1","name":"舊規則","enabled":true,"match":0,"pattern":"嗨","ignoreCase":true,"priority":100,"messagesJson":"[{\"type\":\"text\",\"text\":\"hi\"}]"}]""");

            using var store = new JsonFileLineAutoReplyStore(path);
            var old = (await store.ListAsync()).Single();
            Assert.IsNull(old.ChannelName, "舊資料缺省 = 所有 channel。");

            await store.UpsertAsync(Rule("新規則", channel: "shop"));
            var shop = (await store.ListAsync()).Single(rule => rule.Name == "新規則");
            Assert.AreEqual("shop", shop.ChannelName);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task InMemoryStore_PreservesChannelName_OnCopies()
    {
        var store = new InMemoryLineAutoReplyStore();
        var created = await store.UpsertAsync(Rule("a", channel: "a"));

        Assert.AreEqual("a", created.ChannelName);
        Assert.AreEqual("a", (await store.GetAsync(created.Id))!.ChannelName);
    }

    private static LineAutoReplyRule Rule(
        string name,
        LineAutoReplyMatchMode mode = LineAutoReplyMatchMode.Exact,
        string? channel = null,
        int priority = 100,
        string messages = Reply) =>
        new()
        {
            Name = name,
            Match = mode,
            Pattern = mode is LineAutoReplyMatchMode.Follow or LineAutoReplyMatchMode.Fallback ? string.Empty : "嗨",
            Priority = priority,
            ChannelName = channel,
            MessagesJson = messages,
        };

    private static LineWebhookEvent TextEvent(string text) => new()
    {
        Type = LineWebhookEventTypes.Message,
        Mode = "active",
        ReplyToken = "rt-1",
        Message = new LineWebhookMessage { Type = LineWebhookMessageTypes.Text, Id = "m-1", Text = text },
        Source = new LineWebhookSource { Type = "user", UserId = "U1" },
    };

    private static LineWebhookEvent FollowEvent() => new()
    {
        Type = LineWebhookEventTypes.Follow,
        Mode = "active",
        ReplyToken = "rt-1",
        Source = new LineWebhookSource { Type = "user", UserId = "U1" },
    };

    /// <summary>
    /// 只認得單參數多載的舊式實作。
    /// An old-style implementation that knows only the one-argument overload.
    /// </summary>
    private sealed class OldStyleService : ILineAutoReplyService
    {
        public Task<Result<LineAutoReplyOutcome>> HandleAsync(LineWebhookEvent webhookEvent, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(LineAutoReplyOutcome.NoMatch));
    }

    /// <summary>
    /// 以字典回傳各 channel 假用戶端的工廠。
    /// A factory returning a fake client per channel from a dictionary.
    /// </summary>
    private sealed class FakeFactory(IReadOnlyDictionary<string, FakeLineMessagingClient> clients) : ILineMessagingClientFactory
    {
        public IReadOnlyCollection<string> ChannelNames => [.. clients.Keys];

        public bool Contains(string channelName) => clients.ContainsKey(channelName);

        public ILineMessagingClient Get(string channelName) => clients[channelName];
    }

    /// <summary>
    /// 服務、儲存體與各 channel 的假用戶端。
    /// The service, its store, and a fake client per channel.
    /// </summary>
    private sealed record Harness(
        ILineAutoReplyService Service,
        ILineAutoReplyStore Store,
        Dictionary<string, FakeLineMessagingClient> Clients)
    {
        internal static Harness Create(params string[] channels)
        {
            var clients = channels.ToDictionary(name => name, _ => new FakeLineMessagingClient(), StringComparer.Ordinal);
            var store = new InMemoryLineAutoReplyStore();
            var service = new LineAutoReplyService(store, new FakeFactory(clients), Options.Create(new LineAutoReplyOptions()));

            return new Harness(service, store, clients);
        }
    }
}
