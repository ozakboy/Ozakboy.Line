using System.Text.Json;
using Microsoft.Extensions.Options;
using Ozakboy.Line.AutoReply;
using Ozakboy.Line.Tests.TestSupport;

namespace Ozakboy.Line.Tests;

/// <summary>
/// 關鍵字自動回覆的比對、驗證與處理流程測試。
/// Tests for auto reply matching, validation, and the handling flow.
/// </summary>
[TestClass]
public sealed class LineAutoReplyTests
{
    private const string Reply = """[{"type":"text","text":"收到:{{text}}"}]""";

    [TestMethod]
    public void Match_ExactBeatsContains_AtTheSamePriority()
    {
        // 規則多起來之後,一條寫得寬鬆的 Contains 很容易把精準的 Exact 全部吃掉,
        // 而那種問題從規則列表上看不出來 —— 每一條單獨看都是對的。
        var rules = new List<LineAutoReplyRule>
        {
            Rule("寬鬆", LineAutoReplyMatchMode.Contains, "營業"),
            Rule("精準", LineAutoReplyMatchMode.Exact, "營業時間"),
        };

        Assert.AreEqual("精準", LineAutoReplyMatcher.Match(rules, "營業時間")!.Name);
    }

    [TestMethod]
    public void Match_SpecificityOrderIsExactStartsWithContainsRegex()
    {
        var rules = new List<LineAutoReplyRule>
        {
            Rule("regex", LineAutoReplyMatchMode.Regex, "^營業"),
            Rule("contains", LineAutoReplyMatchMode.Contains, "營業"),
            Rule("startsWith", LineAutoReplyMatchMode.StartsWith, "營業"),
        };

        Assert.AreEqual("startsWith", LineAutoReplyMatcher.Match(rules, "營業時間是?")!.Name);

        rules.RemoveAll(rule => rule.Name == "startsWith");
        Assert.AreEqual("contains", LineAutoReplyMatcher.Match(rules, "營業時間是?")!.Name);
    }

    [TestMethod]
    public void Match_LowerPriorityWinsOverSpecificity()
    {
        var rules = new List<LineAutoReplyRule>
        {
            Rule("精準但排後面", LineAutoReplyMatchMode.Exact, "營業時間", priority: 200),
            Rule("寬鬆但排前面", LineAutoReplyMatchMode.Contains, "營業", priority: 10),
        };

        Assert.AreEqual("寬鬆但排前面", LineAutoReplyMatcher.Match(rules, "營業時間")!.Name, "優先序是第一層,明確程度只在同分時才比。");
    }

    [TestMethod]
    public void Match_SkipsDisabledRules()
    {
        var rules = new List<LineAutoReplyRule> { Rule("停用中", LineAutoReplyMatchMode.Exact, "嗨", enabled: false) };

        Assert.IsNull(LineAutoReplyMatcher.Match(rules, "嗨"));
    }

    [TestMethod]
    public void Match_TrimsTheIncomingText()
    {
        // 手機鍵盤的自動空格與複製貼上帶進來的換行,都會讓一條本該命中的 Exact 悄悄失效。
        var rules = new List<LineAutoReplyRule> { Rule("精準", LineAutoReplyMatchMode.Exact, "嗨") };

        Assert.IsNotNull(LineAutoReplyMatcher.Match(rules, "  嗨\n"));
    }

    [TestMethod]
    public void Match_IgnoreCaseIsHonouredBothWays()
    {
        var insensitive = new List<LineAutoReplyRule> { Rule("不分大小寫", LineAutoReplyMatchMode.Exact, "Hello") };
        var sensitive = new List<LineAutoReplyRule> { Rule("分大小寫", LineAutoReplyMatchMode.Exact, "Hello", ignoreCase: false) };

        Assert.IsNotNull(LineAutoReplyMatcher.Match(insensitive, "HELLO"));
        Assert.IsNull(LineAutoReplyMatcher.Match(sensitive, "HELLO"));
    }

    [TestMethod]
    public void Match_RegexThatBacktracksCatastrophically_DoesNotThrow()
    {
        // 逾時視為不符而不是往外擲:一條寫壞的規則不該讓整個 webhook 回 500,那會讓 LINE 重送整批事件。
        var rules = new List<LineAutoReplyRule> { Rule("炸裂", LineAutoReplyMatchMode.Regex, "^(a+)+$") };
        var text = new string('a', 40) + "!";

        Assert.IsNull(LineAutoReplyMatcher.Match(rules, text));
    }

    [TestMethod]
    public void Match_InvalidRegex_DoesNotThrow()
    {
        var rules = new List<LineAutoReplyRule> { Rule("壞的", LineAutoReplyMatchMode.Regex, "([") };

        Assert.IsNull(LineAutoReplyMatcher.Match(rules, "任何文字"));
    }

    [TestMethod]
    public void Match_IgnoresFollowAndFallbackModes()
    {
        var rules = new List<LineAutoReplyRule>
        {
            Rule("歡迎", LineAutoReplyMatchMode.Follow, string.Empty),
            Rule("聽不懂", LineAutoReplyMatchMode.Fallback, string.Empty),
        };

        Assert.IsNull(LineAutoReplyMatcher.Match(rules, "隨便一句話"), "這兩種模式不看文字,不參與一般比對。");
    }

    [TestMethod]
    public void FindFirst_TakesTheSmallestPriority()
    {
        var rules = new List<LineAutoReplyRule>
        {
            Rule("備援乙", LineAutoReplyMatchMode.Fallback, string.Empty, priority: 50),
            Rule("備援甲", LineAutoReplyMatchMode.Fallback, string.Empty, priority: 10),
            Rule("停用的備援", LineAutoReplyMatchMode.Fallback, string.Empty, priority: 1, enabled: false),
        };

        Assert.AreEqual("備援甲", LineAutoReplyMatcher.FindFirst(rules, LineAutoReplyMatchMode.Fallback)!.Name);
    }

    [TestMethod]
    public void Validate_BlankPattern_FailsForTextModes()
    {
        var result = LineAutoReplyMatcher.Validate(Rule("空樣式", LineAutoReplyMatchMode.Exact, string.Empty));

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.InvalidAutoReplyRule, result.Error!.Code);
    }

    [TestMethod]
    public void Validate_BlankPattern_PassesForFollowAndFallback()
    {
        // 這兩種模式的觸發條件是事件型別而不是文字,硬要求填一個用不到的樣式只會讓人填假值。
        Assert.IsTrue(LineAutoReplyMatcher.Validate(Rule("歡迎", LineAutoReplyMatchMode.Follow, string.Empty)).IsSuccess);
        Assert.IsTrue(LineAutoReplyMatcher.Validate(Rule("備援", LineAutoReplyMatchMode.Fallback, string.Empty)).IsSuccess);
    }

    [TestMethod]
    public void Validate_UncompilableRegex_Fails()
    {
        var result = LineAutoReplyMatcher.Validate(Rule("壞的", LineAutoReplyMatchMode.Regex, "(["));

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.InvalidAutoReplyRule, result.Error!.Code);
    }

    [TestMethod]
    public void Validate_BrokenMessagesJson_Fails()
    {
        var rule = Rule("壞內容", LineAutoReplyMatchMode.Exact, "嗨");
        rule.MessagesJson = "{ 不是陣列";

        Assert.IsTrue(LineAutoReplyMatcher.Validate(rule).IsFailure);
    }

    [TestMethod]
    public async Task HandleAsync_TextMessage_RepliesWithTheRenderedRule()
    {
        var (service, messaging, store) = CreateService();
        await store.UpsertAsync(Rule("精準", LineAutoReplyMatchMode.Exact, "嗨"));

        var outcome = await service.HandleAsync(TextEvent("嗨"));

        Assert.IsTrue(outcome.IsSuccess);
        Assert.AreEqual(LineAutoReplyOutcomeKind.Replied, outcome.GetValueOrThrow().Kind);
        Assert.AreEqual(1, messaging.Replies.Count);
        Assert.AreEqual("rt-1", messaging.Replies[0].ReplyToken);

        using var document = JsonDocument.Parse(messaging.Replies[0].MessagesJson);
        Assert.AreEqual("收到:嗨", document.RootElement[0].GetProperty("text").GetString());
    }

    [TestMethod]
    public async Task HandleAsync_NoRuleAndNoFallback_ReportsNoMatchWithoutSending()
    {
        var (service, messaging, _) = CreateService();

        var outcome = await service.HandleAsync(TextEvent("沒有規則對得上"));

        Assert.AreEqual(LineAutoReplyOutcomeKind.NoMatch, outcome.GetValueOrThrow().Kind);
        Assert.AreEqual(0, messaging.Replies.Count);
    }

    [TestMethod]
    public async Task HandleAsync_NoRuleButFallbackExists_RepliesWithTheFallback()
    {
        var (service, messaging, store) = CreateService();
        var fallback = await store.UpsertAsync(Rule("聽不懂", LineAutoReplyMatchMode.Fallback, string.Empty));

        var outcome = await service.HandleAsync(TextEvent("隨便一句"));

        Assert.AreEqual(LineAutoReplyOutcomeKind.Replied, outcome.GetValueOrThrow().Kind);
        Assert.AreEqual(fallback.Id, outcome.GetValueOrThrow().RuleId);
        Assert.AreEqual(1, messaging.Replies.Count);
    }

    [TestMethod]
    public async Task HandleAsync_OrdinaryRuleWins_OverFallback()
    {
        var (service, _, store) = CreateService();
        var exact = await store.UpsertAsync(Rule("精準", LineAutoReplyMatchMode.Exact, "嗨", priority: 500));
        await store.UpsertAsync(Rule("聽不懂", LineAutoReplyMatchMode.Fallback, string.Empty, priority: 1));

        var outcome = await service.HandleAsync(TextEvent("嗨"));

        Assert.AreEqual(exact.Id, outcome.GetValueOrThrow().RuleId, "備援規則只在沒有其他規則命中時才用,不參與優先序比較。");
    }

    [TestMethod]
    public async Task HandleAsync_FollowEvent_UsesTheFollowRuleAndSubstitutesEmptyText()
    {
        var (service, messaging, store) = CreateService();
        var welcome = Rule("歡迎", LineAutoReplyMatchMode.Follow, string.Empty);
        welcome.MessagesJson = """[{"type":"text","text":"歡迎加入!{{text}}"}]""";
        var stored = await store.UpsertAsync(welcome);

        var outcome = await service.HandleAsync(new Webhook.LineWebhookEvent
        {
            Type = Webhook.LineWebhookEventTypes.Follow,
            ReplyToken = "rt-follow",
            Source = new Webhook.LineWebhookSource { Type = "user", UserId = "U1" },
        });

        Assert.AreEqual(LineAutoReplyOutcomeKind.Replied, outcome.GetValueOrThrow().Kind);
        Assert.AreEqual(stored.Id, outcome.GetValueOrThrow().RuleId);

        using var document = JsonDocument.Parse(messaging.Replies[0].MessagesJson);
        Assert.AreEqual("歡迎加入!", document.RootElement[0].GetProperty("text").GetString(), "加好友事件沒有訊息文字,{{text}} 以空字串代入而不是算缺變數。");
    }

    [TestMethod]
    public async Task HandleAsync_FollowEventWithoutFollowRule_IsSkipped()
    {
        var (service, messaging, _) = CreateService();

        var outcome = await service.HandleAsync(new Webhook.LineWebhookEvent
        {
            Type = Webhook.LineWebhookEventTypes.Follow,
            ReplyToken = "rt-follow",
            Source = new Webhook.LineWebhookSource { Type = "user", UserId = "U1" },
        });

        Assert.AreEqual(LineAutoReplyOutcomeKind.Skipped, outcome.GetValueOrThrow().Kind);
        Assert.AreEqual(0, messaging.Replies.Count);
    }

    [TestMethod]
    public async Task HandleAsync_RuleUsingDisplayName_LooksTheProfileUp()
    {
        var (service, messaging, store) = CreateService();
        messaging.ProfileDisplayName = "阿明";
        var welcome = Rule("歡迎", LineAutoReplyMatchMode.Follow, string.Empty);
        welcome.MessagesJson = """[{"type":"text","text":"{{displayName}} 你好"}]""";
        await store.UpsertAsync(welcome);

        await service.HandleAsync(new Webhook.LineWebhookEvent
        {
            Type = Webhook.LineWebhookEventTypes.Follow,
            ReplyToken = "rt-follow",
            Source = new Webhook.LineWebhookSource { Type = "user", UserId = "U1" },
        });

        using var document = JsonDocument.Parse(messaging.Replies[0].MessagesJson);
        Assert.AreEqual("阿明 你好", document.RootElement[0].GetProperty("text").GetString());
    }

    [TestMethod]
    public async Task HandleAsync_ProfileUnavailable_StillReplies()
    {
        // 為了一個名字讓歡迎訊息整個發不出去,是把小事變成大事。
        var (service, messaging, store) = CreateService();
        messaging.ProfileDisplayName = null;
        var welcome = Rule("歡迎", LineAutoReplyMatchMode.Follow, string.Empty);
        welcome.MessagesJson = """[{"type":"text","text":"{{displayName}}你好"}]""";
        await store.UpsertAsync(welcome);

        var outcome = await service.HandleAsync(new Webhook.LineWebhookEvent
        {
            Type = Webhook.LineWebhookEventTypes.Follow,
            ReplyToken = "rt-follow",
            Source = new Webhook.LineWebhookSource { Type = "user", UserId = "U1" },
        });

        Assert.AreEqual(LineAutoReplyOutcomeKind.Replied, outcome.GetValueOrThrow().Kind);

        using var document = JsonDocument.Parse(messaging.Replies[0].MessagesJson);
        Assert.AreEqual("你好", document.RootElement[0].GetProperty("text").GetString());
    }

    [TestMethod]
    public async Task HandleAsync_NonTextEvent_IsSkipped()
    {
        var (service, messaging, store) = CreateService();
        await store.UpsertAsync(Rule("精準", LineAutoReplyMatchMode.Exact, "嗨"));

        var sticker = new Webhook.LineWebhookEvent
        {
            Type = Webhook.LineWebhookEventTypes.Message,
            ReplyToken = "rt-1",
            Message = new Webhook.LineWebhookMessage { Type = Webhook.LineWebhookMessageTypes.Sticker, Id = "m-1" },
            Source = new Webhook.LineWebhookSource { Type = "user", UserId = "U1" },
        };

        Assert.AreEqual(LineAutoReplyOutcomeKind.Skipped, (await service.HandleAsync(sticker)).GetValueOrThrow().Kind);
        Assert.AreEqual(0, messaging.Replies.Count);
    }

    [TestMethod]
    public async Task HandleAsync_NoReplyToken_IsSkipped()
    {
        var (service, _, store) = CreateService();
        await store.UpsertAsync(Rule("精準", LineAutoReplyMatchMode.Exact, "嗨"));

        var outcome = await service.HandleAsync(new Webhook.LineWebhookEvent
        {
            Type = Webhook.LineWebhookEventTypes.Message,
            Message = new Webhook.LineWebhookMessage { Type = Webhook.LineWebhookMessageTypes.Text, Text = "嗨" },
            Source = new Webhook.LineWebhookSource { Type = "user", UserId = "U1" },
        });

        Assert.AreEqual(LineAutoReplyOutcomeKind.Skipped, outcome.GetValueOrThrow().Kind);
    }

    [TestMethod]
    public async Task HandleAsync_StandbyMode_IsSkipped()
    {
        // standby 代表有真人客服接手了這段對話,這時回覆等於跟真人搶著講話。
        var (service, messaging, store) = CreateService();
        await store.UpsertAsync(Rule("精準", LineAutoReplyMatchMode.Exact, "嗨"));

        var standby = TextEvent("嗨");
        var outcome = await service.HandleAsync(new Webhook.LineWebhookEvent
        {
            Type = standby.Type,
            Mode = "standby",
            ReplyToken = standby.ReplyToken,
            Message = standby.Message,
            Source = standby.Source,
        });

        Assert.AreEqual(LineAutoReplyOutcomeKind.Skipped, outcome.GetValueOrThrow().Kind);
        Assert.AreEqual(0, messaging.Replies.Count);
    }

    [TestMethod]
    public async Task HandleAsync_Disabled_SkipsWithoutReadingTheStore()
    {
        var (service, messaging, store) = CreateService(enabled: false);
        await store.UpsertAsync(Rule("精準", LineAutoReplyMatchMode.Exact, "嗨"));

        var outcome = await service.HandleAsync(TextEvent("嗨"));

        Assert.AreEqual(LineAutoReplyOutcomeKind.Skipped, outcome.GetValueOrThrow().Kind);
        Assert.AreEqual(0, messaging.Replies.Count);
    }

    [TestMethod]
    public async Task HandleAsync_ReplyRejectedByLine_ReportsFailure()
    {
        var (service, messaging, store) = CreateService();
        messaging.SendFailure = Error.Validation(LineErrorCodes.ApiError, "LINE 拒絕了。");
        await store.UpsertAsync(Rule("精準", LineAutoReplyMatchMode.Exact, "嗨"));

        var outcome = await service.HandleAsync(TextEvent("嗨"));

        Assert.IsTrue(outcome.IsFailure);
        Assert.AreEqual(LineErrorCodes.ApiError, outcome.Error!.Code);
    }

    [TestMethod]
    public async Task JsonFileStore_RoundTripsRules()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ozakboy-line-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            using var store = new JsonFileLineAutoReplyStore(Path.Combine(directory, "rules.json"));
            var created = await store.UpsertAsync(Rule("精準", LineAutoReplyMatchMode.Exact, "嗨"));

            var listed = await store.ListAsync();

            Assert.AreEqual(1, listed.Count);
            Assert.AreEqual(created.Id, listed[0].Id);
            Assert.AreEqual(LineAutoReplyMatchMode.Exact, listed[0].Match);
            Assert.AreEqual("嗨", listed[0].Pattern);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// 建立一條規則。
    /// Builds a rule.
    /// </summary>
    /// <param name="name">名稱。The name.</param>
    /// <param name="mode">比對方式。The match mode.</param>
    /// <param name="pattern">樣式。The pattern.</param>
    /// <param name="priority">優先序。The priority.</param>
    /// <param name="enabled">是否啟用。Whether it is on.</param>
    /// <param name="ignoreCase">是否忽略大小寫。Whether case is ignored.</param>
    /// <returns>規則。The rule.</returns>
    private static LineAutoReplyRule Rule(
        string name,
        LineAutoReplyMatchMode mode,
        string pattern,
        int priority = 100,
        bool enabled = true,
        bool ignoreCase = true) =>
        new()
        {
            Name = name,
            Match = mode,
            Pattern = pattern,
            Priority = priority,
            Enabled = enabled,
            IgnoreCase = ignoreCase,
            MessagesJson = Reply,
        };

    /// <summary>
    /// 建立一個文字訊息事件。
    /// Builds a text message event.
    /// </summary>
    /// <param name="text">訊息文字。The message text.</param>
    /// <returns>事件。The event.</returns>
    private static Webhook.LineWebhookEvent TextEvent(string text) => new()
    {
        Type = Webhook.LineWebhookEventTypes.Message,
        Mode = "active",
        ReplyToken = "rt-1",
        Message = new Webhook.LineWebhookMessage { Type = Webhook.LineWebhookMessageTypes.Text, Id = "m-1", Text = text },
        Source = new Webhook.LineWebhookSource { Type = "user", UserId = "U1" },
    };

    /// <summary>
    /// 建立自動回覆服務與它的假相依。
    /// Creates the auto reply service and its fakes.
    /// </summary>
    /// <param name="enabled">是否啟用自動回覆。Whether auto reply is on.</param>
    /// <returns>服務、假用戶端與規則儲存體。The service, the fake client, and the rule store.</returns>
    private static (ILineAutoReplyService Service, FakeLineMessagingClient Messaging, ILineAutoReplyStore Store) CreateService(
        bool enabled = true)
    {
        var messaging = new FakeLineMessagingClient();
        var store = new InMemoryLineAutoReplyStore();
        var service = new LineAutoReplyService(store, messaging, Options.Create(new LineAutoReplyOptions { Enabled = enabled }));

        return (service, messaging, store);
    }
}
