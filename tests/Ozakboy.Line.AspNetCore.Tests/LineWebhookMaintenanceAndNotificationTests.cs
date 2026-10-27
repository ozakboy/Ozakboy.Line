using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Ozakboy.Line.AspNetCore.Tests.TestSupport;
using Ozakboy.Line.AspNetCore.Webhook;
using Ozakboy.Line.AutoReply;
using Ozakboy.Line.Tests.TestSupport;
using Ozakboy.Line.Webhook.Handling;

namespace Ozakboy.Line.AspNetCore.Tests;

/// <summary>
/// 維護模式與管理員錯誤通知的整合測試。
/// Integration tests for maintenance mode and administrator error notification.
/// </summary>
[TestClass]
public sealed class LineWebhookMaintenanceAndNotificationTests
{
    private const string GroupSource = """{"type":"group","groupId":"G1","userId":"U-member"}""";

    [TestMethod]
    public async Task Maintenance_NonAdmin_GetsTheFixedText_AndNeitherHandlerNorAutoReplyRuns()
    {
        var (client, recorder) = RecordingLineMessagingClient.Create();
        var handled = 0;
        var autoReply = new CountingAutoReplyService();
        bool? skipped = null;
        var options = MaintenanceOptions("U-admin");
        options.AutoReply = true;

        using var host = await WebhookTestHost.StartAsync(
            client,
            services => services.AddSingleton<ILineAutoReplyService>(autoReply),
            endpoints => endpoints.MapLineWebhook("/line/webhook", Via(options), Count(() => handled++)),
            items => skipped = items.TryGetValue(LineWebhookItems.MaintenanceSkipped, out var value) ? (bool?)value : null);
        using var http = host.GetTestClient();

        using var response = await WebhookTestHost.PostAsync(http, WebhookTestHost.TextEventBody());

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(0, handled, "維護中,非管理員的事件不交給處理常式。");
        Assert.AreEqual(0, autoReply.Calls, "也不跑自動回覆。");
        var reply = recorder.Sends.Single();
        Assert.AreEqual("ReplyTextAsync", reply.Method);
        Assert.AreEqual("rt-1", reply.Target);
        Assert.AreEqual("系統維護中,請稍後再試。", reply.Text);
        Assert.AreEqual(true, skipped);
        Assert.AreEqual("Ozakboy.Line.MaintenanceSkipped", LineWebhookItems.MaintenanceSkipped, "鍵名是契約。");
    }

    [TestMethod]
    public async Task Maintenance_Admin_IsHandledNormally()
    {
        var (client, recorder) = RecordingLineMessagingClient.Create();
        var handled = 0;
        bool? skipped = null;

        using var host = await WebhookTestHost.StartAsync(
            client,
            registerServices: null,
            endpoints => endpoints.MapLineWebhook("/line/webhook", Via(MaintenanceOptions("U1")), Count(() => handled++)),
            items => skipped = items.TryGetValue(LineWebhookItems.MaintenanceSkipped, out var value) ? (bool?)value : null);
        using var http = host.GetTestClient();

        using var response = await WebhookTestHost.PostAsync(http, WebhookTestHost.TextEventBody());

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(1, handled);
        Assert.AreEqual(0, recorder.Sends.Count);
        Assert.IsNull(skipped, "沒有事件被擋下就沒有這個鍵。");
    }

    [TestMethod]
    public async Task Maintenance_CustomReplyText_IsUsed()
    {
        var (client, recorder) = RecordingLineMessagingClient.Create();
        var options = MaintenanceOptions("U-admin");
        options.Maintenance.ReplyText = "今晚 22:00 前維護";

        using var host = await WebhookTestHost.StartAsync(
            client,
            registerServices: null,
            endpoints => endpoints.MapLineWebhook("/line/webhook", Via(options), Count(() => { })));
        using var http = host.GetTestClient();

        (await WebhookTestHost.PostAsync(http, WebhookTestHost.TextEventBody())).Dispose();

        Assert.AreEqual("今晚 22:00 前維護", recorder.Sends.Single().Text);
    }

    [TestMethod]
    public async Task Maintenance_EventWithoutReplyToken_IsHeldBackButNotAnswered()
    {
        var (client, recorder) = RecordingLineMessagingClient.Create();
        var handled = 0;

        using var host = await WebhookTestHost.StartAsync(
            client,
            registerServices: null,
            endpoints => endpoints.MapLineWebhook("/line/webhook", Via(MaintenanceOptions("U-admin")), Count(() => handled++)));
        using var http = host.GetTestClient();

        using var response = await WebhookTestHost.PostAsync(http, WebhookTestHost.TextEventBody(replyToken: null));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(0, handled);
        Assert.AreEqual(0, recorder.Sends.Count, "沒有回覆權杖就不回,也不改用推播。");
    }

    [TestMethod]
    public async Task Maintenance_StandbyEvent_IsHeldBackButNotAnswered()
    {
        var (client, recorder) = RecordingLineMessagingClient.Create();
        var handled = 0;

        using var host = await WebhookTestHost.StartAsync(
            client,
            registerServices: null,
            endpoints => endpoints.MapLineWebhook("/line/webhook", Via(MaintenanceOptions("U-admin")), Count(() => handled++)));
        using var http = host.GetTestClient();

        (await WebhookTestHost.PostAsync(http, WebhookTestHost.TextEventBody(mode: "standby"))).Dispose();

        Assert.AreEqual(0, handled);
        Assert.AreEqual(0, recorder.Sends.Count, "standby 表示別的模組在接手對話,回覆等於搶話。");
    }

    [TestMethod]
    public async Task Maintenance_GroupEvent_JudgesByUserIdNotByGroupId()
    {
        var (client, recorder) = RecordingLineMessagingClient.Create();
        var handled = new List<string>();

        // 白名單放的是 groupId:群組事件仍然看 userId,因此發話的成員不是管理員就被擋。
        using var host = await WebhookTestHost.StartAsync(
            client,
            registerServices: null,
            endpoints => endpoints.MapLineWebhook("/line/webhook", Via(MaintenanceOptions("G1")), (e, _, _) =>
            {
                handled.Add(e.Source.UserId ?? "none");
                return Task.CompletedTask;
            }));
        using var http = host.GetTestClient();

        (await WebhookTestHost.PostAsync(http, WebhookTestHost.TextEventBody(source: GroupSource))).Dispose();

        Assert.AreEqual(0, handled.Count);
        Assert.AreEqual(1, recorder.Sends.Count);
    }

    [TestMethod]
    public async Task Maintenance_AdminSpeakingInAGroup_IsHandled_AndEventWithoutUserIdIsNot()
    {
        var (client, recorder) = RecordingLineMessagingClient.Create();
        var handled = new List<string>();

        using var host = await WebhookTestHost.StartAsync(
            client,
            registerServices: null,
            endpoints => endpoints.MapLineWebhook("/line/webhook", Via(MaintenanceOptions("U-member")), (e, _, _) =>
            {
                handled.Add(e.Source.UserId ?? "none");
                return Task.CompletedTask;
            }));
        using var http = host.GetTestClient();

        (await WebhookTestHost.PostAsync(http, WebhookTestHost.TextEventBody(source: GroupSource))).Dispose();
        (await WebhookTestHost.PostAsync(http, WebhookTestHost.TextEventBody(source: """{"type":"group","groupId":"G1"}"""))).Dispose();

        CollectionAssert.AreEqual(new[] { "U-member" }, handled, "沒有 UserId 的事件視為非管理員。");
        Assert.AreEqual(1, recorder.Sends.Count);
    }

    [TestMethod]
    public async Task Maintenance_ReplyFailsOrThrows_StillAnswers200()
    {
        var (client, recorder) = RecordingLineMessagingClient.Create();
        recorder.ReplyFailure = Error.Validation("line.api.rejected", "拒絕");
        using var host = await WebhookTestHost.StartAsync(
            client,
            registerServices: null,
            endpoints => endpoints.MapLineWebhook("/line/webhook", Via(MaintenanceOptions("U-admin")), Count(() => { })));
        using var http = host.GetTestClient();

        using var failed = await WebhookTestHost.PostAsync(http, WebhookTestHost.TextEventBody());
        recorder.ReplyFailure = null;
        recorder.ReplyThrows = new InvalidOperationException("網路斷了");
        using var threw = await WebhookTestHost.PostAsync(http, WebhookTestHost.TextEventBody());

        Assert.AreEqual(HttpStatusCode.OK, failed.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, threw.StatusCode);
        Assert.AreEqual(2, recorder.Sends.Count);
    }

    [TestMethod]
    public async Task Maintenance_AppliesToTheGenericOverloadToo()
    {
        var (client, recorder) = RecordingLineMessagingClient.Create();
        var options = MaintenanceOptions("U-admin");

        using var host = await WebhookTestHost.StartAsync(
            client,
            services => services.AddLineWebhookHandler<FailingHandler>(),
            endpoints => endpoints.MapLineWebhook<FailingHandler>("/line/webhook", options));
        using var http = host.GetTestClient();

        using var response = await WebhookTestHost.PostAsync(http, WebhookTestHost.TextEventBody());

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("系統維護中,請稍後再試。", recorder.Sends.Single().Text, "維護中處理常式(會擲例外的那種)根本不該被呼叫。");
    }

    [TestMethod]
    public async Task Maintenance_AppliesToTheConfigureOverloadToo()
    {
        var (client, recorder) = RecordingLineMessagingClient.Create();
        var handled = 0;

        using var host = await WebhookTestHost.StartAsync(
            client,
            registerServices: null,
            endpoints => endpoints.MapLineWebhook(
                "/line/webhook",
                options =>
                {
                    options.Maintenance.Enabled = true;
                    options.Maintenance.AdminUserIds.Add("U-admin");
                },
                Count(() => handled++)));
        using var http = host.GetTestClient();

        (await WebhookTestHost.PostAsync(http, WebhookTestHost.TextEventBody())).Dispose();

        Assert.AreEqual(0, handled);
        Assert.AreEqual(1, recorder.Sends.Count);
    }

    [TestMethod]
    public async Task Maintenance_EnabledWithoutAdmins_ThrowsWhileMapping()
    {
        var (client, _) = RecordingLineMessagingClient.Create();
        var options = new LineWebhookEndpointOptions();
        options.Maintenance.Enabled = true;

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            WebhookTestHost.StartAsync(
                client,
                registerServices: null,
                endpoints => endpoints.MapLineWebhook("/line/webhook", Via(options), Count(() => { }))));

        StringAssert.Contains(exception.Message, "Maintenance.AdminUserIds");
    }

    [TestMethod]
    public async Task Maintenance_DisabledWithEmptyAdmins_IsFine()
    {
        var (client, _) = RecordingLineMessagingClient.Create();
        var handled = 0;

        using var host = await WebhookTestHost.StartAsync(
            client,
            registerServices: null,
            endpoints => endpoints.MapLineWebhook("/line/webhook", Via(new LineWebhookEndpointOptions()), Count(() => handled++)));
        using var http = host.GetTestClient();

        (await WebhookTestHost.PostAsync(http, WebhookTestHost.TextEventBody())).Dispose();

        Assert.AreEqual(1, handled);
    }

    [TestMethod]
    public async Task ErrorNotification_PushesToEveryAdmin_WithTheDocumentedText_AndStill200()
    {
        var (client, recorder) = RecordingLineMessagingClient.Create();
        var options = NotificationOptions("U-admin-1", "U-admin-2");

        using var host = await WebhookTestHost.StartAsync(
            client,
            services => services.AddLineWebhookHandler<FailingHandler>(),
            endpoints => endpoints.MapLineWebhook<FailingHandler>("/line/webhook", options));
        using var http = host.GetTestClient();

        using var response = await WebhookTestHost.PostAsync(http, WebhookTestHost.TextEventBody());

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var pushes = recorder.Sends.Where(s => s.Method == "PushTextAsync").OrderBy(s => s.Target, StringComparer.Ordinal).ToList();
        Assert.AreEqual(2, pushes.Count);
        CollectionAssert.AreEqual(new[] { "U-admin-1", "U-admin-2" }, pushes.Select(p => p.Target).ToArray());
        const string Expected = "[Ozakboy.Line] webhook 事件處理失敗\n事件:message 01ABC\n來源:user/U1\n例外:System.InvalidOperationException: 處理常式壞了";
        Assert.AreEqual(Expected, pushes[0].Text);
        Assert.AreEqual(Expected, pushes[1].Text);
    }

    [TestMethod]
    public async Task ErrorNotification_AdminFailuresAreIndependent_AndNeverChangeTheStatus()
    {
        var (client, recorder) = RecordingLineMessagingClient.Create();
        recorder.PushFailureFor = to => to == "U-a" ? Error.Validation("line.api.rejected", "封鎖了官方帳號") : null;
        recorder.PushThrowsFor = to => to == "U-b" ? new HttpRequestException("連不上") : null;
        var options = NotificationOptions("U-a", "U-b", "U-c");

        using var host = await WebhookTestHost.StartAsync(
            client,
            services => services.AddLineWebhookHandler<FailingHandler>(),
            endpoints => endpoints.MapLineWebhook<FailingHandler>("/line/webhook", options));
        using var http = host.GetTestClient();

        using var response = await WebhookTestHost.PostAsync(http, WebhookTestHost.TextEventBody());

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "通知壞了也不能改變 200 的契約。");
        CollectionAssert.AreEquivalent(new[] { "U-a", "U-b", "U-c" }, recorder.Sends.Select(s => s.Target).ToArray(), "一位失敗不影響其他管理員。");
    }

    [TestMethod]
    public async Task ErrorNotification_StackTrace_OnlyWhenAsked()
    {
        var (client, recorder) = RecordingLineMessagingClient.Create();
        var plain = NotificationOptions("U-admin");
        var withTrace = NotificationOptions("U-admin");
        withTrace.ErrorNotification.IncludeStackTrace = true;

        using var host = await WebhookTestHost.StartAsync(
            client,
            services => services.AddLineWebhookHandler<FailingHandler>(),
            endpoints =>
            {
                endpoints.MapLineWebhook<FailingHandler>("/plain", plain);
                endpoints.MapLineWebhook<FailingHandler>("/trace", withTrace);
            });
        using var http = host.GetTestClient();

        (await PostToAsync(http, "/plain")).Dispose();
        (await PostToAsync(http, "/trace")).Dispose();

        var texts = recorder.Sends.Select(s => s.Text!).ToList();
        Assert.AreEqual(2, texts.Count);
        Assert.IsFalse(texts[0].Contains("   at ", StringComparison.Ordinal), "預設不附堆疊。");
        Assert.IsTrue(texts[1].Contains("   at ", StringComparison.Ordinal), "IncludeStackTrace 才附堆疊。");
        Assert.IsTrue(texts[1].Contains(nameof(FailingHandler), StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ErrorNotification_IsCutTo5000Characters()
    {
        var (client, recorder) = RecordingLineMessagingClient.Create();
        var options = NotificationOptions("U-admin");
        options.ErrorNotification.IncludeStackTrace = true;

        using var host = await WebhookTestHost.StartAsync(
            client,
            services => services.AddLineWebhookHandler<FailingHandler>(),
            endpoints => endpoints.MapLineWebhook<FailingHandler>("/line/webhook", options));
        using var http = host.GetTestClient();

        (await WebhookTestHost.PostAsync(http, WebhookTestHost.TextEventBody(text: "LONG"))).Dispose();

        var text = recorder.Sends.Single().Text!;
        Assert.AreEqual(5000, text.Length);
        Assert.IsTrue(text.StartsWith("[Ozakboy.Line] webhook 事件處理失敗", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ErrorNotification_NotSentWhenNothingFails_OrWhenDisabled()
    {
        var (client, recorder) = RecordingLineMessagingClient.Create();
        var enabled = NotificationOptions("U-admin");

        using var host = await WebhookTestHost.StartAsync(
            client,
            services => services.AddLineWebhookHandler<FailingHandler>(),
            endpoints =>
            {
                endpoints.MapLineWebhook("/line/webhook", Via(enabled), Count(() => { }));
                endpoints.MapLineWebhook<FailingHandler>("/disabled", new LineWebhookEndpointOptions());
            });
        using var http = host.GetTestClient();

        (await WebhookTestHost.PostAsync(http, WebhookTestHost.TextEventBody())).Dispose();
        (await PostToAsync(http, "/disabled")).Dispose();

        Assert.AreEqual(0, recorder.Sends.Count);
    }

    [TestMethod]
    public async Task ErrorNotification_AppliesToTheDelegateOverloadToo()
    {
        var (client, recorder) = RecordingLineMessagingClient.Create();

        using var host = await WebhookTestHost.StartAsync(
            client,
            registerServices: null,
            endpoints => endpoints.MapLineWebhook(
                "/line/webhook",
                options =>
                {
                    options.ErrorNotification.Enabled = true;
                    options.ErrorNotification.AdminUserIds.Add("U-admin");
                },
                (_, _, _) => throw new InvalidOperationException("委派壞了")));
        using var http = host.GetTestClient();

        using var response = await WebhookTestHost.PostAsync(http, WebhookTestHost.TextEventBody());

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(recorder.Sends.Single().Text!, "System.InvalidOperationException: 委派壞了");
    }

    [TestMethod]
    public async Task ErrorNotification_EnabledWithoutAdmins_ThrowsWhileMapping()
    {
        var (client, _) = RecordingLineMessagingClient.Create();
        var options = new LineWebhookEndpointOptions();
        options.ErrorNotification.Enabled = true;

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            WebhookTestHost.StartAsync(
                client,
                registerServices: null,
                endpoints => endpoints.MapLineWebhook("/line/webhook", Via(options), Count(() => { }))));

        StringAssert.Contains(exception.Message, "ErrorNotification.AdminUserIds");
    }

    [TestMethod]
    public async Task OptionsAreSnapshottedWhenMapping()
    {
        var (client, recorder) = RecordingLineMessagingClient.Create();
        var options = MaintenanceOptions("U-admin");

        using var host = await WebhookTestHost.StartAsync(
            client,
            services => services.AddLineWebhookHandler<FailingHandler>(),
            endpoints =>
            {
                endpoints.MapLineWebhook<FailingHandler>("/line/webhook", options);
                options.Maintenance.Enabled = false;
                options.Maintenance.AdminUserIds.Add("U1");
            });
        using var http = host.GetTestClient();

        (await WebhookTestHost.PostAsync(http, WebhookTestHost.TextEventBody())).Dispose();

        Assert.AreEqual(1, recorder.Sends.Count, "掛上之後再改選項物件不影響已掛上的端點。");
    }

    /// <summary>
    /// 把一個選項物件的設定複製成 <c>Action</c>,給委派多載用。
    /// Turns an options object into an <c>Action</c> that copies its settings, for the delegate overloads.
    /// </summary>
    private static Action<LineWebhookEndpointOptions> Via(LineWebhookEndpointOptions source) =>
        target =>
        {
            target.AutoReply = source.AutoReply;
            target.Maintenance.Enabled = source.Maintenance.Enabled;
            target.Maintenance.ReplyText = source.Maintenance.ReplyText;
            foreach (var id in source.Maintenance.AdminUserIds)
            {
                target.Maintenance.AdminUserIds.Add(id);
            }

            target.ErrorNotification.Enabled = source.ErrorNotification.Enabled;
            target.ErrorNotification.IncludeStackTrace = source.ErrorNotification.IncludeStackTrace;
            foreach (var id in source.ErrorNotification.AdminUserIds)
            {
                target.ErrorNotification.AdminUserIds.Add(id);
            }
        };

    private static Task<HttpResponseMessage> PostToAsync(HttpClient http, string path)
    {
        var body = WebhookTestHost.TextEventBody();
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative))
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(
            LineWebhookHttpRequestExtensions.SignatureHeaderName,
            LineWebhookSignature.Compute(WebhookTestHost.ChannelSecret, System.Text.Encoding.UTF8.GetBytes(body)));
        return http.SendAsync(request);
    }

    private static LineWebhookEndpointOptions MaintenanceOptions(params string[] admins)
    {
        var options = new LineWebhookEndpointOptions();
        options.Maintenance.Enabled = true;
        foreach (var admin in admins)
        {
            options.Maintenance.AdminUserIds.Add(admin);
        }

        return options;
    }

    private static LineWebhookEndpointOptions NotificationOptions(params string[] admins)
    {
        var options = new LineWebhookEndpointOptions();
        options.ErrorNotification.Enabled = true;
        foreach (var admin in admins)
        {
            options.ErrorNotification.AdminUserIds.Add(admin);
        }

        return options;
    }

    private static Func<LineWebhookEvent, HttpContext, CancellationToken, Task> Count(Action onHandled) =>
        (_, _, _) =>
        {
            onHandled();
            return Task.CompletedTask;
        };

    /// <summary>
    /// 一律擲例外;文字為 LONG 時例外訊息特別長。
    /// Always throws; the exception message is very long when the text is LONG.
    /// </summary>
    private sealed class FailingHandler : LineWebhookHandler
    {
        protected override Task OnTextMessageAsync(LineWebhookEvent webhookEvent, LineWebhookContext context, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(webhookEvent.Message!.Text == "LONG" ? new string('x', 6000) : "處理常式壞了");
    }

    /// <summary>
    /// 計算被呼叫的次數。
    /// Counts how often it was called.
    /// </summary>
    private sealed class CountingAutoReplyService : ILineAutoReplyService
    {
        internal int Calls { get; private set; }

        public Task<Result<LineAutoReplyOutcome>> HandleAsync(LineWebhookEvent webhookEvent, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Result.Success(LineAutoReplyOutcome.Skipped));
        }
    }
}
