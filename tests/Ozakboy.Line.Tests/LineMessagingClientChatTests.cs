using System.Net;
using Microsoft.Extensions.Options;
using Ozakboy.Http;
using Ozakboy.Line.Messaging.Messages;
using Ozakboy.Line.Messaging.Messages.Template;
using Ozakboy.Line.Tests.TestSupport;

namespace Ozakboy.Line.Tests;

/// <summary>
/// 好友清單、群組與聊天室、載入動畫、已讀標記與訊息驗證端點的測試。全部離線。
/// Tests for the follower list, group and room, loading animation, read mark and message validation endpoints.
/// Entirely offline.
/// </summary>
[TestClass]
public sealed class LineMessagingClientChatTests
{
    private const string AccessToken = "test-channel-access-token";

    [TestMethod]
    public async Task GetFollowerIdsAsync_ParsesSampleAndPassesCursor()
    {
        var handler = FakeHttpMessageHandler.Json(Samples.Body("followers-ids"));
        var client = CreateClient(handler);

        var result = await client.GetFollowerIdsAsync(start: "abc def", limit: 1000);

        Assert.IsTrue(result.TryGetValue(out var page));
        Assert.AreEqual(3, page.UserIds.Count);
        Assert.AreEqual("U4af4980629...", page.UserIds[0]);
        Assert.AreEqual("yANU9IA...", page.Next);
        Assert.AreEqual(HttpMethod.Get, handler.Last.Method);
        Assert.AreEqual(LineEndpoints.FollowerIds + "?limit=1000&start=abc%20def", handler.Last.Uri.AbsoluteUri, "驗證編碼要比對 AbsoluteUri,ToString 會把 %20 還原。");
    }

    [TestMethod]
    public async Task GetFollowerIdsAsync_NoArguments_SendsNoQuery()
    {
        var handler = FakeHttpMessageHandler.Json("""{"userIds":[]}""");
        var client = CreateClient(handler);

        var result = await client.GetFollowerIdsAsync();

        Assert.IsTrue(result.TryGetValue(out var page));
        Assert.AreEqual(0, page.UserIds.Count);
        Assert.IsNull(page.Next);
        Assert.AreEqual(LineEndpoints.FollowerIds, handler.Last.Uri.AbsoluteUri);
    }

    [TestMethod]
    public async Task GetFollowerIdsAsync_LimitOverCap_FailsWithoutSending()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        var result = await client.GetFollowerIdsAsync(limit: LineMessagingLimits.MaxFollowerIdsPerPage + 1);

        Assert.AreEqual(LineErrorCodes.InvalidPageSize, result.Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task GetGroupSummaryAsync_ParsesSample()
    {
        var handler = FakeHttpMessageHandler.Json(Samples.Body("group-summary"));
        var client = CreateClient(handler);

        var result = await client.GetGroupSummaryAsync("C1");

        Assert.IsTrue(result.TryGetValue(out var summary));
        Assert.AreEqual("Ca56f94637c...", summary.GroupId);
        Assert.AreEqual("Group name", summary.GroupName);
        StringAssert.StartsWith(summary.PictureUrl, "https://profile.line-scdn.net/");
        StringAssert.EndsWith(handler.Last.Uri.AbsoluteUri, "/v2/bot/group/C1/summary");
    }

    [TestMethod]
    public async Task GetGroupMemberCountAsync_ParsesCount()
    {
        var handler = FakeHttpMessageHandler.Json("""{"count":3}""");
        var client = CreateClient(handler);

        var result = await client.GetGroupMemberCountAsync("C1");

        Assert.IsTrue(result.TryGetValue(out var count));
        Assert.AreEqual(3, count);
        StringAssert.EndsWith(handler.Last.Uri.AbsoluteUri, "/v2/bot/group/C1/members/count");
    }

    [TestMethod]
    public async Task GetGroupMemberIdsAsync_ReadsMemberIdsFieldAndCursor()
    {
        // 這個端點的欄位是 memberIds,不是好友清單的 userIds。
        var handler = FakeHttpMessageHandler.Json(Samples.Body("group-members-ids"));
        var client = CreateClient(handler);

        var result = await client.GetGroupMemberIdsAsync("C1", start: "cursor");

        Assert.IsTrue(result.TryGetValue(out var page));
        Assert.AreEqual(3, page.UserIds.Count);
        Assert.AreEqual("jxEWCEEP...", page.Next);
        StringAssert.EndsWith(handler.Last.Uri.AbsoluteUri, "/v2/bot/group/C1/members/ids?start=cursor");
    }

    [TestMethod]
    public async Task GetGroupMemberProfileAsync_ParsesSample()
    {
        var handler = FakeHttpMessageHandler.Json(Samples.Body("group-member-profile"));
        var client = CreateClient(handler);

        var result = await client.GetGroupMemberProfileAsync("C1", "U1");

        Assert.IsTrue(result.TryGetValue(out var profile));
        Assert.AreEqual("LINE taro", profile.DisplayName);
        Assert.AreEqual("U4af4980629...", profile.UserId);
        Assert.IsNull(profile.StatusMessage, "群組成員檔案沒有狀態訊息。");
        StringAssert.EndsWith(handler.Last.Uri.AbsoluteUri, "/v2/bot/group/C1/member/U1");
    }

    [TestMethod]
    public async Task LeaveGroupAsync_PostsWithoutBody()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        var result = await client.LeaveGroupAsync("C1");

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(HttpMethod.Post, handler.Last.Method);
        Assert.IsNull(handler.Last.Body);
        StringAssert.EndsWith(handler.Last.Uri.AbsoluteUri, "/v2/bot/group/C1/leave");
    }

    [TestMethod]
    public async Task RoomEndpoints_UseRoomRoutes()
    {
        var handler = FakeHttpMessageHandler.Routes(
            ("/members/count", """{"count":2}"""),
            ("/members/ids", """{"memberIds":["U1","U2"]}"""),
            ("/member/", """{"displayName":"名字","userId":"U1"}"""),
            ("/leave", "{}"));
        var client = CreateClient(handler);

        Assert.AreEqual(2, (await client.GetRoomMemberCountAsync("R1")).GetValueOrThrow());
        StringAssert.EndsWith(handler.Last.Uri.AbsoluteUri, "/v2/bot/room/R1/members/count");

        Assert.AreEqual(2, (await client.GetRoomMemberIdsAsync("R1")).GetValueOrThrow().UserIds.Count);
        StringAssert.EndsWith(handler.Last.Uri.AbsoluteUri, "/v2/bot/room/R1/members/ids");

        Assert.AreEqual("名字", (await client.GetRoomMemberProfileAsync("R1", "U1")).GetValueOrThrow().DisplayName);
        StringAssert.EndsWith(handler.Last.Uri.AbsoluteUri, "/v2/bot/room/R1/member/U1");

        Assert.IsTrue((await client.LeaveRoomAsync("R1")).IsSuccess);
        Assert.AreEqual(HttpMethod.Post, handler.Last.Method);
        StringAssert.EndsWith(handler.Last.Uri.AbsoluteUri, "/v2/bot/room/R1/leave");
    }

    [TestMethod]
    public async Task StartLoadingAnimationAsync_WritesChatIdAndSeconds()
    {
        var handler = FakeHttpMessageHandler.Json("{}", HttpStatusCode.Accepted);
        var client = CreateClient(handler);

        var result = await client.StartLoadingAnimationAsync("U1", 30);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(LineEndpoints.ChatLoadingStart, handler.Last.Uri.AbsoluteUri);
        var json = handler.Last.Json();
        Assert.AreEqual("U1", json.GetProperty("chatId").GetString());
        Assert.AreEqual(30, json.GetProperty("loadingSeconds").GetInt32());
    }

    [TestMethod]
    public async Task StartLoadingAnimationAsync_NoSeconds_OmitsTheField()
    {
        var handler = FakeHttpMessageHandler.Json("{}", HttpStatusCode.Accepted);
        var client = CreateClient(handler);

        await client.StartLoadingAnimationAsync("U1");

        Assert.IsFalse(handler.Last.Json().TryGetProperty("loadingSeconds", out _), "不給秒數就讓 LINE 用預設的 20 秒。");
    }

    [TestMethod]
    [DataRow(4)]
    [DataRow(7)]
    [DataRow(65)]
    [DataRow(0)]
    public async Task StartLoadingAnimationAsync_InvalidSeconds_FailsWithoutSending(int seconds)
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        var result = await client.StartLoadingAnimationAsync("U1", seconds);

        Assert.AreEqual(LineErrorCodes.InvalidLoadingSeconds, result.Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    [DataRow(5)]
    [DataRow(60)]
    public async Task StartLoadingAnimationAsync_BoundarySeconds_Send(int seconds)
    {
        var handler = FakeHttpMessageHandler.Json("{}", HttpStatusCode.Accepted);
        var client = CreateClient(handler);

        var result = await client.StartLoadingAnimationAsync("U1", seconds);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(1, handler.Requests.Count);
    }

    [TestMethod]
    public async Task MarkAsReadAsync_WritesNestedChatUserId()
    {
        // LINE 的形狀是 chat.userId,不是平的 userId。
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        var result = await client.MarkAsReadAsync("U1");

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(LineEndpoints.MarkAsRead, handler.Last.Uri.AbsoluteUri);
        Assert.AreEqual("U1", handler.Last.Json().GetProperty("chat").GetProperty("userId").GetString());
        Assert.IsFalse(handler.Last.Json().TryGetProperty("userId", out _));
    }

    [TestMethod]
    [DataRow(LineMessageValidationTarget.Push, "push")]
    [DataRow(LineMessageValidationTarget.Multicast, "multicast")]
    [DataRow(LineMessageValidationTarget.Broadcast, "broadcast")]
    [DataRow(LineMessageValidationTarget.Reply, "reply")]
    [DataRow(LineMessageValidationTarget.Narrowcast, "narrowcast")]
    public async Task ValidateMessagesAsync_PostsMessagesToTheTargetEndpoint(LineMessageValidationTarget target, string segment)
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        var result = await client.ValidateMessagesAsync(target, [new TextMessage("哈囉")]);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(HttpMethod.Post, handler.Last.Method);
        Assert.AreEqual(LineEndpoints.ValidateMessageBase + segment, handler.Last.Uri.AbsoluteUri);
        var json = handler.Last.Json();
        Assert.AreEqual(1, json.GetProperty("messages").GetArrayLength());
        Assert.IsFalse(json.TryGetProperty("to", out _), "驗證端點只收 messages。");
    }

    [TestMethod]
    public async Task ValidateMessagesAsync_LineRejects_CarriesLineDetails()
    {
        // 400 的重點在 details:它才說得出是哪個欄位錯了。
        var handler = new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                """{"message":"The request body has 1 error(s)","details":[{"message":"May not be empty","property":"messages[0].text"}]}""",
                System.Text.Encoding.UTF8,
                "application/json"),
        });
        var client = CreateClient(handler);

        var result = await client.ValidateMessagesAsync(LineMessageValidationTarget.Push, [new TextMessage("x")]);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.ApiError, result.Error.Code);
        Assert.AreEqual(ErrorCategory.Validation, result.Error.Category);
        Assert.IsTrue(result.Error.TryGetData(LineErrorDataKeys.LineDetails, out var details));
        Assert.AreEqual("messages[0].text: May not be empty", details);
    }

    [TestMethod]
    public async Task ValidateMessagesAsync_LocalLimitFailsFirst_WithoutSending()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        var result = await client.ValidateMessagesAsync(
            LineMessageValidationTarget.Broadcast,
            [new TemplateMessage("alt", new ButtonsTemplate("內文"))]);

        Assert.AreEqual(LineErrorCodes.InvalidTemplate, result.Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task ValidateMessagesRawJsonAsync_WrapsSingleObject()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        var result = await client.ValidateMessagesRawJsonAsync(LineMessageValidationTarget.Reply, """{"type":"text","text":"回覆"}""");

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(LineEndpoints.ValidateMessageBase + "reply", handler.Last.Uri.AbsoluteUri);
        Assert.AreEqual(1, handler.Last.Json().GetProperty("messages").GetArrayLength());
    }

    [TestMethod]
    public async Task ChatEndpoints_NotConfigured_FailWithoutSending()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = new LineMessagingClient(
            new HttpPipelineClient(new HttpClient(handler)),
            Options.Create(new LineMessagingOptions()));

        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.GetFollowerIdsAsync()).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.GetGroupSummaryAsync("C1")).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.GetGroupMemberCountAsync("C1")).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.GetRoomMemberIdsAsync("R1")).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.LeaveGroupAsync("C1")).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.GetRoomMemberProfileAsync("R1", "U1")).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.StartLoadingAnimationAsync("U1")).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.MarkAsReadAsync("U1")).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.ValidateMessagesAsync(LineMessageValidationTarget.Push, [new TextMessage("x")])).Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    private static LineMessagingClient CreateClient(FakeHttpMessageHandler handler) =>
        new(
            new HttpPipelineClient(new HttpClient(handler)),
            Options.Create(new LineMessagingOptions { ChannelAccessToken = AccessToken }));
}
