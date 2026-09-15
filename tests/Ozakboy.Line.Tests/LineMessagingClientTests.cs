using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ozakboy.Http;
using Ozakboy.Http.Retry;
using Ozakboy.Line.Messaging.Actions;
using Ozakboy.Line.Messaging.Messages;
using Ozakboy.Line.Messaging.RichMenu;
using Ozakboy.Line.Tests.TestSupport;

namespace Ozakboy.Line.Tests;

/// <summary>
/// Messaging API 用戶端的測試。全部離線,不連 LINE。
/// Tests for the Messaging API client. Entirely offline; LINE is never called.
/// </summary>
[TestClass]
public sealed class LineMessagingClientTests
{
    private const string AccessToken = "test-channel-access-token";

    [TestMethod]
    public async Task PushAsync_SendsExpectedPayloadShape()
    {
        var handler = FakeHttpMessageHandler.Json("""{"sentMessages":[{"id":"1","quoteToken":"qt-1"}]}""");
        var client = CreateClient(handler);

        var result = await client.PushAsync("U1", [new TextMessage("哈囉")]);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsTrue(result.TryGetValue(out var sent));
        Assert.AreEqual(1, sent.Count);
        Assert.AreEqual("1", sent[0].Id);
        Assert.AreEqual("qt-1", sent[0].QuoteToken);

        var request = handler.Last;
        Assert.AreEqual(HttpMethod.Post, request.Method);
        Assert.AreEqual(LineEndpoints.PushMessage, request.Uri.ToString());
        Assert.AreEqual("Bearer " + AccessToken, request.Headers["Authorization"]);
        Assert.AreEqual("application/json", request.ContentType);

        var json = request.Json();
        Assert.AreEqual("U1", json.GetProperty("to").GetString());
        Assert.AreEqual("text", json.GetProperty("messages")[0].GetProperty("type").GetString());
        Assert.AreEqual("哈囉", json.GetProperty("messages")[0].GetProperty("text").GetString());
        Assert.IsFalse(json.TryGetProperty("notificationDisabled", out _), "沒有要求靜音時不輸出這個欄位。");
    }

    [TestMethod]
    public async Task PushAsync_NotificationDisabled_WritesTheFlag()
    {
        var handler = FakeHttpMessageHandler.Json("""{"sentMessages":[]}""");
        var client = CreateClient(handler);

        await client.PushAsync("U1", [new TextMessage("哈囉")], new LinePushOptions { NotificationDisabled = true });

        Assert.IsTrue(handler.Last.Json().GetProperty("notificationDisabled").GetBoolean());
    }

    [TestMethod]
    public async Task PushAsync_WithRetryKey_SetsHeaderAndMarksIdempotent()
    {
        // 兩件事缺一不可:標頭讓 LINE 去重,冪等標記讓管線願意重試。
        var handler = FakeHttpMessageHandler.Json("""{"sentMessages":[]}""");
        var client = CreateClient(handler);
        var retryKey = Guid.NewGuid();

        await client.PushAsync("U1", [new TextMessage("哈囉")], new LinePushOptions { RetryKey = retryKey });

        Assert.AreEqual(retryKey.ToString("D"), handler.Last.Headers["X-Line-Retry-Key"]);
        Assert.AreEqual(RequestIdempotency.Idempotent, handler.Last.Idempotency);
    }

    [TestMethod]
    public async Task PushAsync_WithoutRetryKey_IsNotMarkedIdempotent()
    {
        var handler = FakeHttpMessageHandler.Json("""{"sentMessages":[]}""");
        var client = CreateClient(handler);

        await client.PushAsync("U1", [new TextMessage("哈囉")]);

        Assert.IsFalse(handler.Last.Headers.ContainsKey("X-Line-Retry-Key"));
        Assert.AreNotEqual(RequestIdempotency.Idempotent, handler.Last.Idempotency);
    }

    [TestMethod]
    public async Task PushAsync_WithoutRetryKey_IsSentOnceEvenOnServerError()
    {
        // 沒有重試鍵的推播重送一次就是多發一則訊息。伺服器錯誤照樣只送一次。
        var handler = new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("""{"message":"server error"}""", System.Text.Encoding.UTF8, "application/json"),
        });
        var client = CreatePipelineClient(handler);

        var result = await client.PushAsync("U1", [new TextMessage("哈囉")]);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(1, handler.Requests.Count, "沒有重試鍵時絕不重送。");
    }

    [TestMethod]
    public async Task PushAsync_WithRetryKey_IsRetriedOnServerError()
    {
        var handler = new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("""{"message":"server error"}""", System.Text.Encoding.UTF8, "application/json"),
        });
        var client = CreatePipelineClient(handler);

        var result = await client.PushAsync(
            "U1",
            [new TextMessage("哈囉")],
            new LinePushOptions { RetryKey = Guid.NewGuid() });

        Assert.IsTrue(result.IsFailure);
        Assert.IsTrue(handler.Requests.Count > 1, "帶了重試鍵就可以安全重試,LINE 會以該鍵去重。");
        Assert.IsTrue(
            handler.Requests.All(request => request.Headers["X-Line-Retry-Key"] == handler.Requests[0].Headers["X-Line-Retry-Key"]),
            "每一次重試都必須帶同一個鍵,否則 LINE 去不了重。");
    }

    [TestMethod]
    public async Task PushTextAsync_WrapsTheTextIntoOneMessage()
    {
        var handler = FakeHttpMessageHandler.Json("""{"sentMessages":[]}""");
        var client = CreateClient(handler);

        await client.PushTextAsync("U1", "哈囉");

        Assert.AreEqual(1, handler.Last.Json().GetProperty("messages").GetArrayLength());
    }

    [TestMethod]
    public async Task MulticastAsync_TooManyRecipients_FailsWithoutSending()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);
        var recipients = Enumerable.Range(0, LineMessagingLimits.MulticastRecipients + 1).Select(i => $"U{i}").ToArray();

        var result = await client.MulticastAsync(recipients, [new TextMessage("哈囉")]);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.TooManyRecipients, result.Error.Code);
        Assert.AreEqual(0, handler.Requests.Count, "超量的請求在本地就該擋下,不該浪費一次 LINE 的額度。");
    }

    [TestMethod]
    public async Task MulticastAsync_NoRecipients_FailsWithoutSending()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        var result = await client.MulticastAsync([], [new TextMessage("哈囉")]);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.TooManyRecipients, result.Error.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task MulticastAsync_AtTheLimit_Sends()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);
        var recipients = Enumerable.Range(0, LineMessagingLimits.MulticastRecipients).Select(i => $"U{i}").ToArray();

        var result = await client.MulticastAsync(recipients, [new TextMessage("哈囉")]);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(LineMessagingLimits.MulticastRecipients, handler.Last.Json().GetProperty("to").GetArrayLength());
    }

    [TestMethod]
    public async Task PushAsync_NoMessages_FailsWithoutSending()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        var result = await client.PushAsync("U1", []);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.TooManyMessages, result.Error.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task PushAsync_SixMessages_FailsWithoutSending()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);
        var messages = Enumerable.Range(0, LineMessagingLimits.MaxMessagesPerRequest + 1)
            .Select(i => (LineMessage)new TextMessage($"第 {i} 則"))
            .ToArray();

        var result = await client.PushAsync("U1", messages);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.TooManyMessages, result.Error.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task BroadcastAsync_SendsMessagesWithoutRecipient()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        var result = await client.BroadcastTextAsync("公告");

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(LineEndpoints.BroadcastMessage, handler.Last.Uri.ToString());
        Assert.IsFalse(handler.Last.Json().TryGetProperty("to", out _), "廣播沒有收件者欄位。");
    }

    [TestMethod]
    public async Task BroadcastRawJsonAsync_SingleObject_IsWrappedIntoAnArray()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        var result = await client.BroadcastRawJsonAsync("""{"type":"text","text":"公告"}""");

        Assert.IsTrue(result.IsSuccess);
        var messages = handler.Last.Json().GetProperty("messages");
        Assert.AreEqual(JsonValueKind.Array, messages.ValueKind);
        Assert.AreEqual(1, messages.GetArrayLength());
        Assert.AreEqual("公告", messages[0].GetProperty("text").GetString());
    }

    [TestMethod]
    public async Task BroadcastRawJsonAsync_Array_IsSentAsIs()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        var result = await client.BroadcastRawJsonAsync("""[{"type":"text","text":"一"},{"type":"text","text":"二"}]""");

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(2, handler.Last.Json().GetProperty("messages").GetArrayLength());
    }

    [TestMethod]
    public async Task BroadcastRawJsonAsync_InvalidJson_FailsWithoutSending()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        var result = await client.BroadcastRawJsonAsync("{not json");

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.InvalidJson, result.Error.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task BroadcastRawJsonAsync_ScalarJson_FailsWithoutSending()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        var result = await client.BroadcastRawJsonAsync("42");

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.InvalidJson, result.Error.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task ReplyAsync_SendsReplyTokenAndParsesSentMessages()
    {
        var handler = FakeHttpMessageHandler.Json("""{"sentMessages":[{"id":"1"},{"id":"2"}]}""");
        var client = CreateClient(handler);

        var result = await client.ReplyAsync("rt-1", [new TextMessage("一"), new TextMessage("二")]);

        Assert.IsTrue(result.TryGetValue(out var sent));
        Assert.AreEqual(2, sent.Count);
        Assert.AreEqual("2", sent[1].Id);
        Assert.IsNull(sent[0].QuoteToken);
        Assert.AreEqual(LineEndpoints.ReplyMessage, handler.Last.Uri.ToString());
        Assert.AreEqual("rt-1", handler.Last.Json().GetProperty("replyToken").GetString());
    }

    [TestMethod]
    public async Task ReplyRawJsonAsync_WrapsSingleObject()
    {
        var handler = FakeHttpMessageHandler.Json("""{"sentMessages":[]}""");
        var client = CreateClient(handler);

        await client.ReplyRawJsonAsync("rt-1", """{"type":"text","text":"回覆"}""");

        var json = handler.Last.Json();
        Assert.AreEqual("rt-1", json.GetProperty("replyToken").GetString());
        Assert.AreEqual(1, json.GetProperty("messages").GetArrayLength());
    }

    [TestMethod]
    public async Task GetQuotaAsync_ParsesLimitedQuota()
    {
        var client = CreateClient(FakeHttpMessageHandler.Json("""{"type":"limited","value":500}"""));

        var result = await client.GetQuotaAsync();

        Assert.IsTrue(result.TryGetValue(out var quota));
        Assert.AreEqual("limited", quota.Type);
        Assert.AreEqual(500L, quota.Value);
    }

    [TestMethod]
    public async Task GetQuotaAsync_ParsesUnlimitedQuota()
    {
        var client = CreateClient(FakeHttpMessageHandler.Json("""{"type":"none"}"""));

        var result = await client.GetQuotaAsync();

        Assert.IsTrue(result.TryGetValue(out var quota));
        Assert.AreEqual("none", quota.Type);
        Assert.IsNull(quota.Value);
    }

    [TestMethod]
    public async Task GetQuotaConsumptionAsync_ParsesTotalUsage()
    {
        var client = CreateClient(FakeHttpMessageHandler.Json("""{"totalUsage":"1234"}"""));

        var result = await client.GetQuotaConsumptionAsync();

        Assert.IsTrue(result.TryGetValue(out var usage));
        Assert.AreEqual(1234L, usage, "LINE 有時把數字放在字串裡,JSON 設定必須容得下。");
    }

    [TestMethod]
    public async Task GetBotInfoAsync_ParsesEveryField()
    {
        var client = CreateClient(FakeHttpMessageHandler.Json(
            """{"userId":"Ubot","basicId":"@abc","premiumId":"nice-id","displayName":"幼明燈","pictureUrl":"https://example.com/b.jpg","chatMode":"bot","markAsReadMode":"auto"}"""));

        var result = await client.GetBotInfoAsync();

        Assert.IsTrue(result.TryGetValue(out var info));
        Assert.AreEqual("Ubot", info.UserId);
        Assert.AreEqual("@abc", info.BasicId);
        Assert.AreEqual("nice-id", info.PremiumId);
        Assert.AreEqual("bot", info.ChatMode);
        Assert.AreEqual("auto", info.MarkAsReadMode);
    }

    [TestMethod]
    public async Task GetProfileAsync_NotAFriend_FailsAsNotFound()
    {
        var handler = new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("""{"message":"Not found"}""", System.Text.Encoding.UTF8, "application/json"),
        });
        var client = CreateClient(handler);

        var result = await client.GetProfileAsync("U1");

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(ErrorCategory.NotFound, result.Error.Category);
        StringAssert.Contains(handler.Last.Uri.ToString(), "/v2/bot/profile/U1");
    }

    [TestMethod]
    public async Task GetMessageContentAsync_ReturnsBytesAndContentType()
    {
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        var handler = new FakeHttpMessageHandler((_, _) =>
        {
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });
        var client = CreateClient(handler);

        var result = await client.GetMessageContentAsync("m-1");

        Assert.IsTrue(result.TryGetValue(out var content));
        CollectionAssert.AreEqual(bytes, content.Bytes, "二進位內容不能經過字串轉換。");
        Assert.AreEqual("image/jpeg", content.ContentType);
        StringAssert.StartsWith(handler.Last.Uri.ToString(), LineEndpoints.MessagingDataApiBase, "訊息內容走資料端點。");
        StringAssert.EndsWith(handler.Last.Uri.ToString(), "/v2/bot/message/m-1/content");
    }

    [TestMethod]
    public async Task GetMessageContentAsync_NonSuccess_MapsToLineError()
    {
        var handler = new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("""{"message":"Content not found"}""", System.Text.Encoding.UTF8, "application/json"),
        });
        var client = CreateClient(handler);

        var result = await client.GetMessageContentAsync("m-1");

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.ApiError, result.Error.Code);
        Assert.AreEqual(ErrorCategory.NotFound, result.Error.Category);
        Assert.IsTrue(result.Error.TryGetData(LineErrorDataKeys.LineMessage, out var message));
        Assert.AreEqual("Content not found", message);
    }

    [TestMethod]
    public async Task CreateRichMenuAsync_SendsMenuAndReturnsId()
    {
        var handler = FakeHttpMessageHandler.Json("""{"richMenuId":"richmenu-1"}""");
        var client = CreateClient(handler);

        var menu = new LineRichMenu { Name = "主選單", ChatBarText = "開啟選單", Selected = true };
        menu.Areas.Add(new LineRichMenuArea
        {
            Bounds = new LineRichMenuBounds { X = 0, Y = 0, Width = 1250, Height = 1686 },
            Action = new UriAction("https://example.com") { Label = "官網" },
        });

        var result = await client.CreateRichMenuAsync(menu);

        Assert.IsTrue(result.TryGetValue(out var id));
        Assert.AreEqual("richmenu-1", id);

        var json = handler.Last.Json();
        Assert.AreEqual(2500, json.GetProperty("size").GetProperty("width").GetInt32());
        Assert.AreEqual(1686, json.GetProperty("size").GetProperty("height").GetInt32());
        Assert.AreEqual("主選單", json.GetProperty("name").GetString());
        Assert.AreEqual("開啟選單", json.GetProperty("chatBarText").GetString());
        Assert.IsTrue(json.GetProperty("selected").GetBoolean());

        var area = json.GetProperty("areas")[0];
        Assert.AreEqual(1250, area.GetProperty("bounds").GetProperty("width").GetInt32());
        Assert.AreEqual("uri", area.GetProperty("action").GetProperty("type").GetString());
        Assert.AreEqual("官網", area.GetProperty("action").GetProperty("label").GetString());
        Assert.AreEqual("https://example.com", area.GetProperty("action").GetProperty("uri").GetString());
    }

    [TestMethod]
    public async Task CreateRichMenuAsync_ResponseWithoutId_Fails()
    {
        var client = CreateClient(FakeHttpMessageHandler.Json("{}"));

        var result = await client.CreateRichMenuAsync(new LineRichMenu { Name = "n", ChatBarText = "c" });

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.ApiInvalidResponse, result.Error.Code);
    }

    [TestMethod]
    public async Task UploadRichMenuImageAsync_PostsBinaryToDataEndpoint()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        var result = await client.UploadRichMenuImageAsync("richmenu-1", [1, 2, 3], "image/png");

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(HttpMethod.Post, handler.Last.Method);
        StringAssert.StartsWith(handler.Last.Uri.ToString(), LineEndpoints.MessagingDataApiBase, "圖片上傳走資料端點,不是一般 API 端點。");
        StringAssert.EndsWith(handler.Last.Uri.ToString(), "/v2/bot/richmenu/richmenu-1/content");
        Assert.AreEqual("image/png", handler.Last.ContentType);
    }

    [TestMethod]
    public async Task GetRichMenuListAsync_ParsesLowercaseRichmenusField()
    {
        // LINE 這個欄位是全小寫的 richmenus。靠命名原則自動轉會得到永遠空的清單,而且不會有錯誤。
        var client = CreateClient(FakeHttpMessageHandler.Json(
            """{"richmenus":[{"richMenuId":"r1","size":{"width":2500,"height":843},"selected":false,"name":"選單一","chatBarText":"開啟","areas":[{"bounds":{"x":0,"y":0,"width":100,"height":100},"action":{"type":"postback","data":"a=1"}}]}]}"""));

        var result = await client.GetRichMenuListAsync();

        Assert.IsTrue(result.TryGetValue(out var menus));
        Assert.AreEqual(1, menus.Count);
        Assert.AreEqual("r1", menus[0].RichMenuId);
        Assert.AreEqual(843, menus[0].Size.Height);
        Assert.AreEqual("選單一", menus[0].Name);
        Assert.AreEqual("postback", menus[0].Areas[0].Action.Type);
        Assert.IsInstanceOfType<RawAction>(menus[0].Areas[0].Action, "讀回來的動作一律保留原樣,不猜具體型別。");
    }

    [TestMethod]
    public async Task GetRichMenuListAsync_EmptyResponse_ReturnsEmptyList()
    {
        var client = CreateClient(FakeHttpMessageHandler.Json("{}"));

        var result = await client.GetRichMenuListAsync();

        Assert.IsTrue(result.TryGetValue(out var menus));
        Assert.AreEqual(0, menus.Count, "清單屬性永遠不是 null。");
    }

    [TestMethod]
    public async Task GetDefaultRichMenuIdAsync_NotFound_SucceedsWithNull()
    {
        // 「沒有設定預設選單」是正常狀態,不是錯誤;逼呼叫端自己讀錯誤代碼判斷等於把協定細節推出去。
        var handler = new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("""{"message":"no default"}""", System.Text.Encoding.UTF8, "application/json"),
        });
        var client = CreateClient(handler);

        var result = await client.GetDefaultRichMenuIdAsync();

        Assert.IsTrue(result.IsSuccess);
        Assert.IsTrue(result.TryGetValue(out var id));
        Assert.IsNull(id);
    }

    [TestMethod]
    public async Task GetRichMenuIdOfUserAsync_NotFound_SucceedsWithNull()
    {
        var handler = new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("""{"message":"no menu"}""", System.Text.Encoding.UTF8, "application/json"),
        });
        var client = CreateClient(handler);

        var result = await client.GetRichMenuIdOfUserAsync("U1");

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNull(result.GetValueOrDefault());
        StringAssert.EndsWith(handler.Last.Uri.ToString(), "/v2/bot/user/U1/richmenu");
    }

    [TestMethod]
    public async Task GetRichMenuIdOfUserAsync_Found_ReturnsId()
    {
        var client = CreateClient(FakeHttpMessageHandler.Json("""{"richMenuId":"richmenu-9"}"""));

        var result = await client.GetRichMenuIdOfUserAsync("U1");

        Assert.IsTrue(result.TryGetValue(out var id));
        Assert.AreEqual("richmenu-9", id);
    }

    [TestMethod]
    public async Task GetRichMenuIdOfUserAsync_ServerError_StillFails()
    {
        // 只有 404 才當成「沒有連結」。500 仍然是失敗,不能被一併吞掉。
        var handler = new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("""{"message":"boom"}""", System.Text.Encoding.UTF8, "application/json"),
        });
        var client = CreateClient(handler);

        var result = await client.GetRichMenuIdOfUserAsync("U1");

        Assert.IsTrue(result.IsFailure);
    }

    [TestMethod]
    public async Task LinkAndUnlinkRichMenu_UseExpectedRoutesAndMethods()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        await client.LinkRichMenuToUserAsync("U1", "richmenu-1");
        Assert.AreEqual(HttpMethod.Post, handler.Last.Method);
        StringAssert.EndsWith(handler.Last.Uri.ToString(), "/v2/bot/user/U1/richmenu/richmenu-1");

        await client.UnlinkRichMenuFromUserAsync("U1");
        Assert.AreEqual(HttpMethod.Delete, handler.Last.Method);
        StringAssert.EndsWith(handler.Last.Uri.ToString(), "/v2/bot/user/U1/richmenu");

        await client.SetDefaultRichMenuAsync("richmenu-1");
        Assert.AreEqual(HttpMethod.Post, handler.Last.Method);
        StringAssert.EndsWith(handler.Last.Uri.ToString(), "/v2/bot/user/all/richmenu/richmenu-1");

        await client.ClearDefaultRichMenuAsync();
        Assert.AreEqual(HttpMethod.Delete, handler.Last.Method);
        StringAssert.EndsWith(handler.Last.Uri.ToString(), "/v2/bot/user/all/richmenu");

        await client.DeleteRichMenuAsync("richmenu-1");
        Assert.AreEqual(HttpMethod.Delete, handler.Last.Method);
        StringAssert.EndsWith(handler.Last.Uri.ToString(), "/v2/bot/richmenu/richmenu-1");
    }

    [TestMethod]
    public async Task NotConfigured_FailsWithoutSendingAnything()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = new LineMessagingClient(
            new HttpPipelineClient(new HttpClient(handler)),
            Options.Create(new LineMessagingOptions()));

        var push = await client.PushTextAsync("U1", "哈囉");
        var quota = await client.GetQuotaAsync();

        Assert.AreEqual(LineErrorCodes.NotConfigured, push.Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, quota.Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    /// <summary>
    /// 建立直接接上假處理器的用戶端(沒有重試處理器)。
    /// Creates a client wired straight to a fake handler, with no retry handler.
    /// </summary>
    /// <param name="handler">假處理器。The fake handler.</param>
    /// <returns>用戶端。The client.</returns>
    private static LineMessagingClient CreateClient(FakeHttpMessageHandler handler) =>
        new LineMessagingClient(
            new HttpPipelineClient(new HttpClient(handler)),
            Options.Create(new LineMessagingOptions { ChannelAccessToken = AccessToken }));

    /// <summary>
    /// 以真正的註冊路徑建立用戶端,管線含重試處理器。
    /// Creates a client through the real registration path, with the retry handler in the pipeline.
    /// </summary>
    /// <param name="handler">假處理器,作為管線最底層。The fake handler, at the bottom of the pipeline.</param>
    /// <returns>用戶端。The client.</returns>
    private static ILineMessagingClient CreatePipelineClient(FakeHttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLineMessaging(
            options => options.ChannelAccessToken = AccessToken,
            pipeline =>
            {
                // 退避延遲壓到近乎零:這個測試驗的是「重試了幾次」,不是「等了多久」。
                pipeline.Retry.Policy = new RetryPolicy
                {
                    MaxAttempts = 3,
                    BaseDelay = TimeSpan.FromMilliseconds(1),
                    MaxDelay = TimeSpan.FromMilliseconds(2),
                    JitterRatio = 0,
                };
            });

        services.AddHttpClient(LineHttpClientNames.Messaging).ConfigurePrimaryHttpMessageHandler(() => handler);

        return services.BuildServiceProvider().GetRequiredService<ILineMessagingClient>();
    }
}
