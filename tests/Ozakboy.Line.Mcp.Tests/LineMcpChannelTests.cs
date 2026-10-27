using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ozakboy.Line.AutoReply;
using Ozakboy.Line.Liff;
using Ozakboy.Line.Mcp.Tests.TestSupport;
using Ozakboy.Line.Messaging;
using Ozakboy.Line.Tests.TestSupport;

namespace Ozakboy.Line.Mcp.Tests;

/// <summary>
/// MCP 工具指定 channel 的測試:<see cref="LineMcpOptions.ChannelName"/> 決定用哪個帳號的用戶端,
/// 找不到時一律是 <c>line.not_configured</c> 而不是例外,也不會退回別的帳號。
/// Tests for MCP tools pinned to a channel: <see cref="LineMcpOptions.ChannelName"/> decides whose client is
/// used, and a missing channel is always <c>line.not_configured</c> rather than an exception, never a fallback to
/// another account.
/// </summary>
[TestClass]
public sealed class LineMcpChannelTests
{
    [TestMethod]
    public void Options_ChannelName_DefaultsToTheEmptyString()
    {
        Assert.AreEqual(string.Empty, new LineMcpOptions().ChannelName);
    }

    [TestMethod]
    public async Task DirectMode_SendsThroughTheNamedChannelsClient()
    {
        var shop = new FakeLineMessagingClient();
        var care = new FakeLineMessagingClient();
        await using var provider = BuildProvider(
            channel: "shop",
            mode: LineMcpSendMode.Direct,
            keyed: new Dictionary<string, ILineMessagingClient> { ["shop"] = shop, ["care"] = care });

        var tools = ActivatorUtilities.CreateInstance<LineSendTools>(provider);
        var json = Parse(await tools.SendPushAsync("U1", "嗨"));

        Assert.IsTrue(json.GetProperty("ok").GetBoolean());
        Assert.AreEqual(1, shop.Pushes.Count);
        Assert.AreEqual(0, care.Pushes.Count, "只送到設定的 channel,不會送到另一個帳號。");
    }

    [TestMethod]
    public async Task ReviewMode_QueuesWithoutCallingAnyChannel()
    {
        var shop = new FakeLineMessagingClient();
        await using var provider = BuildProvider("shop", LineMcpSendMode.Review, new Dictionary<string, ILineMessagingClient> { ["shop"] = shop });

        var tools = ActivatorUtilities.CreateInstance<LineSendTools>(provider);
        var json = Parse(await tools.SendPushAsync("U1", "嗨"));

        Assert.AreEqual("review", json.GetProperty("mode").GetString());
        Assert.AreEqual(0, shop.Pushes.Count);
        Assert.AreEqual(1, (await provider.GetRequiredService<ILineMcpOutboxStore>().ListAsync()).Count);
    }

    [TestMethod]
    [DataRow(LineMcpSendMode.Review)]
    [DataRow(LineMcpSendMode.Direct)]
    public async Task MissingChannel_SendTools_AnswerNotConfigured_AndQueueNothing(LineMcpSendMode mode)
    {
        var other = new FakeLineMessagingClient();
        await using var provider = BuildProvider("ghost", mode, new Dictionary<string, ILineMessagingClient> { ["shop"] = other });
        var tools = ActivatorUtilities.CreateInstance<LineSendTools>(provider);

        var push = Parse(await tools.SendPushAsync("U1", "嗨"));
        var multicast = Parse(await tools.SendMulticastAsync(["U1", "U2"], "嗨"));
        var broadcast = Parse(await tools.SendBroadcastAsync("嗨"));

        foreach (var json in new[] { push, multicast, broadcast })
        {
            Assert.IsFalse(json.GetProperty("ok").GetBoolean());
            Assert.AreEqual(LineErrorCodes.NotConfigured, json.GetProperty("code").GetString());
        }

        Assert.AreEqual(0, other.Pushes.Count + other.Multicasts.Count + other.Broadcasts.Count, "不會退回別的 channel。");
        Assert.AreEqual(0, (await provider.GetRequiredService<ILineMcpOutboxStore>().ListAsync()).Count, "找不到 channel 就不排進待審佇列。");
    }

    [TestMethod]
    public async Task MissingChannel_InfoAndRichMenuTools_AnswerNotConfigured_InsteadOfThrowing()
    {
        await using var provider = BuildProvider("ghost", LineMcpSendMode.Review);
        var info = ActivatorUtilities.CreateInstance<LineInfoTools>(provider);
        var richMenu = ActivatorUtilities.CreateInstance<LineRichMenuTools>(provider);

        var results = new[]
        {
            Parse(await info.GetBotInfoAsync()),
            Parse(await info.GetQuotaAsync()),
            Parse(await info.GetProfileAsync("U1")),
            Parse(await richMenu.ListRichMenusAsync()),
            Parse(await richMenu.GetDefaultRichMenuAsync()),
        };

        foreach (var json in results)
        {
            Assert.IsFalse(json.GetProperty("ok").GetBoolean());
            Assert.AreEqual(LineErrorCodes.NotConfigured, json.GetProperty("code").GetString());
        }
    }

    [TestMethod]
    public async Task MissingChannel_ReplaceRichMenu_FailsAtProposalTime()
    {
        await using var provider = BuildProvider("ghost", LineMcpSendMode.Review);
        var richMenu = ActivatorUtilities.CreateInstance<LineRichMenuTools>(provider);
        const string Menu = """{"size":{"width":2500,"height":843},"selected":false,"name":"m","chatBarText":"選單","areas":[{"bounds":{"x":0,"y":0,"width":2500,"height":843},"action":{"type":"message","label":"a","text":"a"}}]}""";

        var json = Parse(await richMenu.ReplaceRichMenuAsync(Menu, "https://example.com/menu.png"));

        Assert.AreEqual(LineErrorCodes.NotConfigured, json.GetProperty("code").GetString());
        Assert.AreEqual(0, (await provider.GetRequiredService<ILineMcpOutboxStore>().ListAsync()).Count);
    }

    [TestMethod]
    public async Task BotInfo_ReportsTheChannelName()
    {
        var shop = new FakeLineMessagingClient();
        await using var provider = BuildProvider("shop", LineMcpSendMode.Review, new Dictionary<string, ILineMessagingClient> { ["shop"] = shop });

        var info = ActivatorUtilities.CreateInstance<LineInfoTools>(provider);
        var json = Parse(await info.GetBotInfoAsync());

        Assert.IsTrue(json.GetProperty("ok").GetBoolean());
        Assert.AreEqual("shop", json.GetProperty("channelName").GetString());
    }

    [TestMethod]
    public async Task BotInfo_DefaultChannel_ReportsTheEmptyName()
    {
        var direct = new FakeLineMessagingClient();
        var services = new ServiceCollection();
        services.AddSingleton<ILineMessagingClient>(direct);
        services.AddLineMcp(_ => { });
        await using var provider = services.BuildServiceProvider();

        var info = ActivatorUtilities.CreateInstance<LineInfoTools>(provider);
        var json = Parse(await info.GetBotInfoAsync());

        Assert.AreEqual(string.Empty, json.GetProperty("channelName").GetString());
    }

    [TestMethod]
    public async Task OldHostWithoutAFactory_StillWorksOnTheDefaultChannel()
    {
        // 0.4.0 之前的宿主只註冊了一個 ILineMessagingClient,沒有工廠。
        var direct = new FakeLineMessagingClient();
        var services = new ServiceCollection();
        services.AddSingleton<ILineMessagingClient>(direct);
        services.AddLineMcp(options => options.SendMode = LineMcpSendMode.Direct);
        await using var provider = services.BuildServiceProvider();

        var tools = ActivatorUtilities.CreateInstance<LineSendTools>(provider);
        var json = Parse(await tools.SendPushAsync("U1", "嗨"));

        Assert.IsTrue(json.GetProperty("ok").GetBoolean());
        Assert.AreEqual(1, direct.Pushes.Count);
    }

    [TestMethod]
    public async Task NamedChannelWithoutAFactory_DoesNotFallBackToTheDirectClient()
    {
        var direct = new FakeLineMessagingClient();
        var services = new ServiceCollection();
        services.AddSingleton<ILineMessagingClient>(direct);
        services.AddLineMcp(options =>
        {
            options.SendMode = LineMcpSendMode.Direct;
            options.ChannelName = "shop";
        });
        await using var provider = services.BuildServiceProvider();

        var tools = ActivatorUtilities.CreateInstance<LineSendTools>(provider);
        var json = Parse(await tools.SendPushAsync("U1", "嗨"));

        Assert.AreEqual(LineErrorCodes.NotConfigured, json.GetProperty("code").GetString());
        Assert.AreEqual(0, direct.Pushes.Count, "指定了 shop 卻悄悄用預設帳號送,比送不出去嚴重得多。");
    }

    [TestMethod]
    public async Task OutboxService_ApprovesThroughTheNamedChannel()
    {
        var shop = new FakeLineMessagingClient();
        var care = new FakeLineMessagingClient();
        await using var provider = BuildProvider(
            "shop",
            LineMcpSendMode.Review,
            new Dictionary<string, ILineMessagingClient> { ["shop"] = shop, ["care"] = care });
        var tools = ActivatorUtilities.CreateInstance<LineSendTools>(provider);
        var queued = Parse(await tools.SendPushAsync("U1", "嗨"));

        var approved = await provider.GetRequiredService<ILineMcpOutboxService>()
            .ApproveAndSendAsync(queued.GetProperty("outboxId").GetString()!);

        Assert.AreEqual(LineMcpOutboxStatus.Sent, approved.GetValueOrThrow().Status);
        Assert.AreEqual(1, shop.Pushes.Count);
        Assert.AreEqual(0, care.Pushes.Count);
    }

    [TestMethod]
    public async Task OutboxService_ChannelRemovedBeforeApproval_MarksTheItemFailedWithNotConfigured()
    {
        // 提出時 channel 存在、核准前設定被改掉:核准要誠實失敗,而不是擲例外或送到別處。
        var item = new LineMcpOutboxItem
        {
            Kind = LineMcpOutboxKind.Push,
            PayloadJson = """{"to":"U1","messages":[{"type":"text","text":"嗨"}]}""",
            Summary = "嗨",
            Status = LineMcpOutboxStatus.PendingReview,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await using var provider = BuildProvider("ghost", LineMcpSendMode.Review);
        var store = provider.GetRequiredService<ILineMcpOutboxStore>();
        var added = await store.AddAsync(item);

        var approved = await provider.GetRequiredService<ILineMcpOutboxService>().ApproveAndSendAsync(added.Id);

        var result = approved.GetValueOrThrow();
        Assert.AreEqual(LineMcpOutboxStatus.Failed, result.Status);
        StringAssert.Contains(result.Error, "ghost");
    }

    [TestMethod]
    public async Task LiffTools_UseTheNamedChannelsLiffClient_AndSayNotConfiguredForAMissingOne()
    {
        var liffShop = new FakeLineLiffClient();
        liffShop.Apps.Add(new LineLiffApp
        {
            LiffId = "111-shop",
            View = new LineLiffView { Type = LineLiffViewType.Tall, Url = "https://example.com/shop" },
            Description = "店",
        });

        await using var shopProvider = BuildProvider("shop", LineMcpSendMode.Review, liff: new Dictionary<string, ILineLiffClient> { ["shop"] = liffShop });
        var shopTools = ActivatorUtilities.CreateInstance<LineLiffTools>(shopProvider);
        var listed = Parse(await shopTools.ListLiffAppsAsync());
        Assert.AreEqual(1, listed.GetProperty("count").GetInt32());

        await using var ghostProvider = BuildProvider("ghost", LineMcpSendMode.Review, liff: new Dictionary<string, ILineLiffClient> { ["shop"] = liffShop });
        var ghostTools = ActivatorUtilities.CreateInstance<LineLiffTools>(ghostProvider);
        var missing = Parse(await ghostTools.ListLiffAppsAsync());
        Assert.IsFalse(missing.GetProperty("ok").GetBoolean());
        Assert.AreEqual(LineErrorCodes.NotConfigured, missing.GetProperty("code").GetString());
    }

    [TestMethod]
    public async Task Clients_Resolver_ReportsAvailabilityAndAStandInThatKnowsItsChannel()
    {
        await using var provider = BuildProvider("ghost", LineMcpSendMode.Review);
        var clients = provider.GetRequiredService<LineMcpClients>();

        Assert.AreEqual("ghost", clients.ChannelName);
        Assert.IsFalse(clients.IsMessagingAvailable);
        Assert.AreEqual(LineErrorCodes.NotConfigured, clients.MessagingUnavailableError!.Code);
        Assert.AreEqual("ghost", clients.Messaging.ChannelName);
        Assert.IsNull(clients.Liff);

        var failed = await clients.Messaging.GetQuotaAsync();
        Assert.AreEqual(LineErrorCodes.NotConfigured, failed.Error!.Code);
        var failedPlain = await clients.Messaging.DeleteRichMenuAsync("x");
        Assert.AreEqual(LineErrorCodes.NotConfigured, failedPlain.Error!.Code, "不帶值的 Result 也一樣。");
    }

    [TestMethod]
    public async Task AutoReplyUpsert_StoresTheChannelName_AndRejectsUnknownOnes()
    {
        await using var provider = BuildProvider("shop", LineMcpSendMode.Review, withAutoReply: true);
        var tools = ActivatorUtilities.CreateInstance<LineAutoReplyTools>(provider);
        const string Messages = """[{"type":"text","text":"嗨"}]""";

        var ok = Parse(await tools.UpsertAutoReplyAsync("店規則", "Exact", Messages, pattern: "嗨", channelName: "shop"));
        var all = Parse(await tools.UpsertAutoReplyAsync("通用規則", "Exact", Messages, pattern: "你好"));
        var bad = Parse(await tools.UpsertAutoReplyAsync("拼錯", "Exact", Messages, pattern: "哈", channelName: "shpo"));

        Assert.IsTrue(ok.GetProperty("ok").GetBoolean());
        Assert.AreEqual("shop", ok.GetProperty("rule").GetProperty("channelName").GetString());
        Assert.IsTrue(all.GetProperty("ok").GetBoolean());
        Assert.AreEqual(JsonValueKind.Null, all.GetProperty("rule").GetProperty("channelName").ValueKind);
        Assert.IsFalse(bad.GetProperty("ok").GetBoolean());
        Assert.AreEqual(2, (await provider.GetRequiredService<ILineAutoReplyStore>().ListAsync()).Count);
    }

    [TestMethod]
    public async Task AutoReplyTest_OnlyConsidersRulesOfTheChannel()
    {
        await using var provider = BuildProvider("shop", LineMcpSendMode.Review, withAutoReply: true);
        var tools = ActivatorUtilities.CreateInstance<LineAutoReplyTools>(provider);
        const string Messages = """[{"type":"text","text":"嗨"}]""";
        await tools.UpsertAutoReplyAsync("店規則", "Exact", Messages, pattern: "嗨", channelName: "shop");
        await tools.UpsertAutoReplyAsync("另一家", "Exact", Messages, pattern: "嗨", channelName: "care", priority: 1);

        var onDefaultChannel = Parse(await tools.TestAutoReplyAsync("嗨"));
        var onCare = Parse(await tools.TestAutoReplyAsync("嗨", "care"));

        Assert.AreEqual("店規則", onDefaultChannel.GetProperty("rule").GetProperty("name").GetString(), "省略時用 MCP 設定的 channel。");
        Assert.AreEqual("另一家", onCare.GetProperty("rule").GetProperty("name").GetString());
    }

    private static ServiceProvider BuildProvider(
        string channel,
        LineMcpSendMode mode,
        Dictionary<string, ILineMessagingClient>? keyed = null,
        Dictionary<string, ILineLiffClient>? liff = null,
        bool withAutoReply = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // 先走真正的註冊(登記名稱、工廠),再把 keyed 用戶端換成假的:最後註冊的贏。
        // Register for real first (names, factory), then swap the keyed clients for fakes: the last one wins.
        foreach (var name in new[] { "shop", "care" })
        {
            services.AddLineMessaging(name, o => o.ChannelAccessToken = "token-" + name);
            services.AddLineLiff(name, o => o.ChannelAccessToken = "login-token-" + name);
        }

        if (keyed is not null)
        {
            foreach (var (name, client) in keyed)
            {
                services.AddKeyedSingleton(name, client);
            }
        }

        if (liff is not null)
        {
            foreach (var (name, client) in liff)
            {
                services.AddKeyedSingleton(name, client);
            }
        }

        if (withAutoReply)
        {
            services.AddLineAutoReply();
        }

        services.AddLineMcp(options =>
        {
            options.SendMode = mode;
            options.ChannelName = channel;
            options.MaxSendRequestsPerDay = 100;
        });

        return services.BuildServiceProvider();
    }

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
