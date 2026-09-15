using System.Text.Json;
using Microsoft.Extensions.Options;
using Ozakboy.Line.AutoReply;
using Ozakboy.Line.Templates;

namespace Ozakboy.Line.Mcp.Tests;

/// <summary>
/// 自動回覆與訊息範本工具的測試。
/// Tests for the auto reply and message template tools.
/// </summary>
[TestClass]
public sealed class LineMcpRuleAndTemplateToolsTests
{
    private const string Reply = """[{"type":"text","text":"收到:{{text}}"}]""";

    [TestMethod]
    public async Task AutoReplyTools_WithoutStore_SayTheFeatureIsOff()
    {
        // 工具照樣掛上,只是回一句「未啟用」—— 這比工具根本不出現清楚:
        // AI 讀得到「有這個功能,只是這個站沒開」,而不是自己猜為什麼做不到。
        var tools = new LineAutoReplyTools(Options.Create(new LineMcpOptions()));

        var json = Parse(await tools.ListAutoRepliesAsync());

        Assert.IsFalse(json.GetProperty("ok").GetBoolean());
        StringAssert.Contains(json.GetProperty("error").GetString(), "AddLineAutoReply");
    }

    [TestMethod]
    public async Task UpsertAutoReply_CreatesThenUpdates()
    {
        var store = new InMemoryLineAutoReplyStore();
        var tools = new LineAutoReplyTools(Options.Create(new LineMcpOptions()), store);

        var created = Parse(await tools.UpsertAutoReplyAsync("營業時間", "Exact", Reply, pattern: "營業時間"));
        Assert.IsTrue(created.GetProperty("ok").GetBoolean());

        var id = created.GetProperty("rule").GetProperty("id").GetString()!;
        var updated = Parse(await tools.UpsertAutoReplyAsync("營業時間(改)", "Exact", Reply, id, pattern: "營業時間"));

        Assert.AreEqual(id, updated.GetProperty("rule").GetProperty("id").GetString(), "給了 id 就是更新那一條,不是再新增一條。");
        Assert.AreEqual(1, (await store.ListAsync()).Count);
    }

    [TestMethod]
    public async Task UpsertAutoReply_FollowModeWithoutPattern_IsAccepted()
    {
        var store = new InMemoryLineAutoReplyStore();
        var tools = new LineAutoReplyTools(Options.Create(new LineMcpOptions()), store);

        var json = Parse(await tools.UpsertAutoReplyAsync("歡迎", "Follow", """[{"type":"text","text":"歡迎加入"}]"""));

        Assert.IsTrue(json.GetProperty("ok").GetBoolean(), "Follow 的觸發條件是事件型別,不是文字。");
        Assert.AreEqual("Follow", json.GetProperty("rule").GetProperty("match").GetString());
    }

    [TestMethod]
    public async Task UpsertAutoReply_UnknownMatchMode_ListsTheValidOnes()
    {
        var tools = new LineAutoReplyTools(Options.Create(new LineMcpOptions()), new InMemoryLineAutoReplyStore());

        var json = Parse(await tools.UpsertAutoReplyAsync("壞的", "Sometimes", Reply, pattern: "x"));

        Assert.IsFalse(json.GetProperty("ok").GetBoolean());
        StringAssert.Contains(json.GetProperty("error").GetString(), "Fallback", "錯誤訊息要列出可用值,AI 才改得對。");
    }

    [TestMethod]
    public async Task UpsertAutoReply_BrokenMessagesJson_IsRefusedBeforeStoring()
    {
        // 存進去之後才發現的話,症狀是某些使用者傳訊息完全沒有回應,而 log 裡只有一行渲染失敗。
        var store = new InMemoryLineAutoReplyStore();
        var tools = new LineAutoReplyTools(Options.Create(new LineMcpOptions()), store);

        var json = Parse(await tools.UpsertAutoReplyAsync("壞的", "Exact", "{ 不是陣列", pattern: "x"));

        Assert.IsFalse(json.GetProperty("ok").GetBoolean());
        Assert.AreEqual(0, (await store.ListAsync()).Count);
    }

    [TestMethod]
    public async Task UpsertAutoReply_RuleEditsDisallowed_IsRefused()
    {
        var store = new InMemoryLineAutoReplyStore();
        var tools = new LineAutoReplyTools(Options.Create(new LineMcpOptions { AllowRuleEdits = false }), store);

        var json = Parse(await tools.UpsertAutoReplyAsync("營業時間", "Exact", Reply, pattern: "營業時間"));

        Assert.IsFalse(json.GetProperty("ok").GetBoolean());
        Assert.AreEqual(0, (await store.ListAsync()).Count);
    }

    [TestMethod]
    public async Task ListAutoReplies_ReadingIsStillAllowedWhenEditsAreNot()
    {
        var store = new InMemoryLineAutoReplyStore();
        await store.UpsertAsync(new LineAutoReplyRule { Name = "營業時間", Match = LineAutoReplyMatchMode.Exact, Pattern = "營業時間", MessagesJson = Reply });
        var tools = new LineAutoReplyTools(Options.Create(new LineMcpOptions { AllowRuleEdits = false }), store);

        Assert.IsTrue(Parse(await tools.ListAutoRepliesAsync()).GetProperty("ok").GetBoolean(), "禁止編輯不等於禁止查看。");
    }

    [TestMethod]
    public async Task DeleteAutoReply_UnknownId_IsReportedAsAnError()
    {
        var tools = new LineAutoReplyTools(Options.Create(new LineMcpOptions()), new InMemoryLineAutoReplyStore());

        Assert.IsFalse(Parse(await tools.DeleteAutoReplyAsync("nope")).GetProperty("ok").GetBoolean());
    }

    [TestMethod]
    public async Task TestAutoReply_ReportsTheMatchWithoutSending()
    {
        var store = new InMemoryLineAutoReplyStore();
        await store.UpsertAsync(new LineAutoReplyRule { Name = "精準", Match = LineAutoReplyMatchMode.Exact, Pattern = "嗨", MessagesJson = Reply });
        var tools = new LineAutoReplyTools(Options.Create(new LineMcpOptions()), store);

        var json = Parse(await tools.TestAutoReplyAsync("嗨"));

        Assert.IsTrue(json.GetProperty("matched").GetBoolean());
        Assert.AreEqual("精準", json.GetProperty("rule").GetProperty("name").GetString());

        using var rendered = JsonDocument.Parse(json.GetProperty("renderedMessagesJson").GetString()!);
        Assert.AreEqual("收到:嗨", rendered.RootElement[0].GetProperty("text").GetString());
    }

    [TestMethod]
    public async Task TestAutoReply_NoMatchAndNoFallback_SaysSo()
    {
        var tools = new LineAutoReplyTools(Options.Create(new LineMcpOptions()), new InMemoryLineAutoReplyStore());

        var json = Parse(await tools.TestAutoReplyAsync("沒有規則對得上"));

        Assert.IsTrue(json.GetProperty("ok").GetBoolean());
        Assert.IsFalse(json.GetProperty("matched").GetBoolean());
    }

    [TestMethod]
    public async Task TestAutoReply_FallsBackToTheFallbackRule()
    {
        var store = new InMemoryLineAutoReplyStore();
        await store.UpsertAsync(new LineAutoReplyRule { Name = "聽不懂", Match = LineAutoReplyMatchMode.Fallback, MessagesJson = Reply });
        var tools = new LineAutoReplyTools(Options.Create(new LineMcpOptions()), store);

        var json = Parse(await tools.TestAutoReplyAsync("隨便一句"));

        Assert.IsTrue(json.GetProperty("matched").GetBoolean());
        Assert.AreEqual("聽不懂", json.GetProperty("rule").GetProperty("name").GetString());
    }

    [TestMethod]
    public async Task TemplateTools_WithoutStore_SayTheFeatureIsOff()
    {
        var tools = new LineTemplateTools(Options.Create(new LineMcpOptions()));

        var json = Parse(await tools.ListTemplatesAsync());

        Assert.IsFalse(json.GetProperty("ok").GetBoolean());
        StringAssert.Contains(json.GetProperty("error").GetString(), "AddLineTemplates");
    }

    [TestMethod]
    public async Task UpsertTemplate_ReportsTheVariablesItFound()
    {
        var store = new InMemoryLineTemplateStore();
        var tools = new LineTemplateTools(Options.Create(new LineMcpOptions()), store);

        var json = Parse(await tools.UpsertTemplateAsync("提醒", """[{"type":"text","text":"{{name}} 的 {{item}} 到了"}]"""));

        var variables = json.GetProperty("template").GetProperty("variables").EnumerateArray().Select(v => v.GetString()).ToList();
        CollectionAssert.AreEqual(new List<string?> { "name", "item" }, variables);
    }

    [TestMethod]
    public async Task UpsertTemplate_TemplateEditsDisallowed_IsRefused()
    {
        var store = new InMemoryLineTemplateStore();
        var tools = new LineTemplateTools(Options.Create(new LineMcpOptions { AllowTemplateEdits = false }), store);

        Assert.IsFalse(Parse(await tools.UpsertTemplateAsync("提醒", """[{"type":"text","text":"x"}]""")).GetProperty("ok").GetBoolean());
        Assert.AreEqual(0, (await store.ListAsync()).Count);
    }

    [TestMethod]
    public async Task RenderTemplate_SubstitutesAndSaysItWasNotSent()
    {
        var store = new InMemoryLineTemplateStore();
        var template = await store.UpsertAsync(new LineMessageTemplate
        {
            Name = "提醒",
            MessagesJson = """[{"type":"text","text":"{{name}} 你好"}]""",
        });
        var tools = new LineTemplateTools(Options.Create(new LineMcpOptions()), store);

        var json = Parse(await tools.RenderTemplateAsync(template.Id, """{"name":"阿明"}"""));

        Assert.IsTrue(json.GetProperty("ok").GetBoolean());
        StringAssert.Contains(json.GetProperty("note").GetString(), "尚未送出", "渲染不等於送出,回傳值要說清楚。");

        using var rendered = JsonDocument.Parse(json.GetProperty("messagesJson").GetString()!);
        Assert.AreEqual("阿明 你好", rendered.RootElement[0].GetProperty("text").GetString());
    }

    [TestMethod]
    public async Task RenderTemplate_MissingVariable_IsRefused()
    {
        var store = new InMemoryLineTemplateStore();
        var template = await store.UpsertAsync(new LineMessageTemplate
        {
            Name = "提醒",
            MessagesJson = """[{"type":"text","text":"{{name}} 你好"}]""",
        });
        var tools = new LineTemplateTools(Options.Create(new LineMcpOptions()), store);

        var json = Parse(await tools.RenderTemplateAsync(template.Id, "{}"));

        Assert.IsFalse(json.GetProperty("ok").GetBoolean());
        StringAssert.Contains(json.GetProperty("error").GetString(), "name", "不會把 {{name}} 原樣留在內容裡。");
    }

    [TestMethod]
    public async Task RenderTemplate_NumericVariable_IsAccepted()
    {
        // 「AI 把 3 寫成數字而不是字串」很常見,為此退回一次呼叫不划算。
        var store = new InMemoryLineTemplateStore();
        var template = await store.UpsertAsync(new LineMessageTemplate
        {
            Name = "提醒",
            MessagesJson = """[{"type":"text","text":"還有 {{count}} 天"}]""",
        });
        var tools = new LineTemplateTools(Options.Create(new LineMcpOptions()), store);

        var json = Parse(await tools.RenderTemplateAsync(template.Id, """{"count":3}"""));

        using var rendered = JsonDocument.Parse(json.GetProperty("messagesJson").GetString()!);
        Assert.AreEqual("還有 3 天", rendered.RootElement[0].GetProperty("text").GetString());
    }

    [TestMethod]
    public async Task RenderTemplate_VariablesJsonIsNotAnObject_IsRefused()
    {
        var store = new InMemoryLineTemplateStore();
        var template = await store.UpsertAsync(new LineMessageTemplate { Name = "提醒", MessagesJson = """[{"type":"text","text":"x"}]""" });
        var tools = new LineTemplateTools(Options.Create(new LineMcpOptions()), store);

        Assert.IsFalse(Parse(await tools.RenderTemplateAsync(template.Id, """["不是物件"]""")).GetProperty("ok").GetBoolean());
    }

    /// <summary>
    /// 解析工具回傳的 JSON。
    /// Parses a tool's JSON result.
    /// </summary>
    /// <param name="json">JSON 字串。The JSON string.</param>
    /// <returns>根元素。The root element.</returns>
    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
