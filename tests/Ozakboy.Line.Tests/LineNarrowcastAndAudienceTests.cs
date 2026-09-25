using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Ozakboy.Http;
using Ozakboy.Http.Retry;
using Ozakboy.Line.Messaging.Messages;
using Ozakboy.Line.Messaging.Narrowcast;
using Ozakboy.Line.Tests.TestSupport;

namespace Ozakboy.Line.Tests;

/// <summary>
/// 分眾推播與受眾端點的測試。全部離線。
/// Tests for the narrowcast and audience endpoints. Entirely offline.
/// </summary>
[TestClass]
public sealed class LineNarrowcastAndAudienceTests
{
    private const string AccessToken = "test-channel-access-token";

    [TestMethod]
    public async Task NarrowcastAsync_WritesRecipientFilterLimitAndReturnsRequestIdHeader()
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new StringContent("{}") };
            response.Headers.Add("X-Line-Request-Id", "req-123");
            return response;
        });
        var client = CreateClient(handler);

        using var demographic = JsonDocument.Parse("""{"type":"gender","oneOf":["female"]}""");
        var result = await client.NarrowcastAsync(
            [new TextMessage("分眾")],
            new LineNarrowcastOptions
            {
                Recipient = LineNarrowcastRecipient.Audience(5614991017776),
                Demographic = demographic.RootElement,
                MaxRecipients = 100,
                UpToRemainingQuota = true,
                NotificationDisabled = true,
            });

        Assert.IsTrue(result.TryGetValue(out var requestId));
        Assert.AreEqual("req-123", requestId, "請求識別碼在回應標頭,不在內容裡。");
        Assert.AreEqual(LineEndpoints.NarrowcastMessage, handler.Last.Uri.AbsoluteUri);

        var json = handler.Last.Json();
        Assert.AreEqual("分眾", json.GetProperty("messages")[0].GetProperty("text").GetString());
        Assert.AreEqual("audience", json.GetProperty("recipient").GetProperty("type").GetString());
        Assert.AreEqual(5614991017776L, json.GetProperty("recipient").GetProperty("audienceGroupId").GetInt64());
        Assert.AreEqual("gender", json.GetProperty("filter").GetProperty("demographic").GetProperty("type").GetString());
        Assert.AreEqual(100, json.GetProperty("limit").GetProperty("max").GetInt32());
        Assert.IsTrue(json.GetProperty("limit").GetProperty("upToRemainingQuota").GetBoolean());
        Assert.IsTrue(json.GetProperty("notificationDisabled").GetBoolean());
    }

    [TestMethod]
    public async Task NarrowcastAsync_NoOptions_WritesOnlyMessages()
    {
        var handler = AcceptedWithRequestId("req-1");
        var client = CreateClient(handler);

        await client.NarrowcastAsync([new TextMessage("全部")]);

        var json = handler.Last.Json();
        Assert.IsFalse(json.TryGetProperty("recipient", out _));
        Assert.IsFalse(json.TryGetProperty("filter", out _));
        Assert.IsFalse(json.TryGetProperty("limit", out _));
        Assert.IsFalse(json.TryGetProperty("notificationDisabled", out _));
    }

    [TestMethod]
    public async Task NarrowcastAsync_WithRetryKey_SetsHeaderAndMarksIdempotent()
    {
        var handler = AcceptedWithRequestId("req-1");
        var client = CreateClient(handler);
        var retryKey = Guid.NewGuid();

        await client.NarrowcastAsync([new TextMessage("x")], new LineNarrowcastOptions { RetryKey = retryKey });

        Assert.AreEqual(retryKey.ToString("D"), handler.Last.Headers["X-Line-Retry-Key"]);
        Assert.AreEqual(RequestIdempotency.Idempotent, handler.Last.Idempotency);
    }

    [TestMethod]
    public async Task NarrowcastAsync_WithoutRetryKey_IsNotIdempotent()
    {
        var handler = AcceptedWithRequestId("req-1");
        var client = CreateClient(handler);

        await client.NarrowcastAsync([new TextMessage("x")]);

        Assert.IsFalse(handler.Last.Headers.ContainsKey("X-Line-Retry-Key"));
        Assert.AreNotEqual(RequestIdempotency.Idempotent, handler.Last.Idempotency);
    }

    [TestMethod]
    public async Task NarrowcastAsync_MissingRequestIdHeader_FailsAsInvalidResponse()
    {
        var handler = FakeHttpMessageHandler.Json("{}", HttpStatusCode.Accepted);
        var client = CreateClient(handler);

        var result = await client.NarrowcastAsync([new TextMessage("x")]);

        Assert.AreEqual(LineErrorCodes.ApiInvalidResponse, result.Error!.Code);
    }

    [TestMethod]
    public async Task NarrowcastAsync_LineRejects_MapsToLineErrorWithCategoryPreserved()
    {
        var handler = new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"message":"The request body has 1 error(s)","details":[{"message":"invalid audience","property":"recipient"}]}""", System.Text.Encoding.UTF8, "application/json"),
        });
        var client = CreateClient(handler);

        var result = await client.NarrowcastAsync([new TextMessage("x")]);

        Assert.AreEqual(LineErrorCodes.ApiError, result.Error!.Code);
        Assert.AreEqual(ErrorCategory.Validation, result.Error.Category);
        Assert.IsTrue(result.Error.TryGetData(LineErrorDataKeys.LineDetails, out var details));
        Assert.AreEqual("recipient: invalid audience", details);
    }

    [TestMethod]
    public async Task NarrowcastAsync_TooManyMessages_FailsWithoutSending()
    {
        var handler = AcceptedWithRequestId("req-1");
        var client = CreateClient(handler);
        var messages = Enumerable.Range(0, LineMessagingLimits.MaxMessagesPerRequest + 1).Select(i => (LineMessage)new TextMessage($"{i}")).ToArray();

        var result = await client.NarrowcastAsync(messages);

        Assert.AreEqual(LineErrorCodes.TooManyMessages, result.Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task GetNarrowcastProgressAsync_ParsesSucceededSample()
    {
        var handler = FakeHttpMessageHandler.Json(Samples.Body("narrowcast-progress"));
        var client = CreateClient(handler);

        var result = await client.GetNarrowcastProgressAsync("req-123");

        Assert.IsTrue(result.TryGetValue(out var progress));
        Assert.AreEqual(LineNarrowcastProgress.PhaseSucceeded, progress.Phase);
        Assert.AreEqual(1L, progress.SuccessCount);
        Assert.AreEqual(1L, progress.FailureCount);
        Assert.AreEqual(2L, progress.TargetCount);
        Assert.AreEqual("2020-12-03T10:15:30.121Z", progress.AcceptedTime);
        Assert.IsNull(progress.ErrorCode);
        Assert.AreEqual(LineEndpoints.NarrowcastProgress + "?requestId=req-123", handler.Last.Uri.AbsoluteUri);
    }

    [TestMethod]
    public async Task GetNarrowcastProgressAsync_ParsesFailedSample()
    {
        var client = CreateClient(FakeHttpMessageHandler.Json(Samples.Body("narrowcast-progress-failed")));

        var result = await client.GetNarrowcastProgressAsync("req-123");

        Assert.IsTrue(result.TryGetValue(out var progress));
        Assert.AreEqual(LineNarrowcastProgress.PhaseFailed, progress.Phase);
        Assert.AreEqual(1, progress.ErrorCode);
        Assert.AreEqual("narrowcast target is not found", progress.FailedDescription);
        Assert.IsNull(progress.SuccessCount);
    }

    [TestMethod]
    public void Recipient_Operators_ProduceLineShapes()
    {
        var tree = LineNarrowcastRecipient.And(
            LineNarrowcastRecipient.Audience(1),
            LineNarrowcastRecipient.Not(LineNarrowcastRecipient.Redelivery("req-old")),
            LineNarrowcastRecipient.Or(LineNarrowcastRecipient.Audience(2), LineNarrowcastRecipient.Audience(3)));

        Assert.AreEqual("operator", tree.GetProperty("type").GetString());
        var and = tree.GetProperty("and");
        Assert.AreEqual(3, and.GetArrayLength());
        Assert.AreEqual(1L, and[0].GetProperty("audienceGroupId").GetInt64());
        Assert.AreEqual("redelivery", and[1].GetProperty("not").GetProperty("type").GetString());
        Assert.AreEqual("req-old", and[1].GetProperty("not").GetProperty("requestId").GetString());
        Assert.AreEqual(2, and[2].GetProperty("or").GetArrayLength());
    }

    [TestMethod]
    public void Recipient_OperatorWithoutOperands_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => LineNarrowcastRecipient.And());
    }

    [TestMethod]
    public async Task CreateUploadAudienceGroupAsync_WritesAudiencesAsIdObjectsAndParsesSample()
    {
        var handler = FakeHttpMessageHandler.Json(Samples.Body("audience-group-created"));
        var client = CreateClient(handler);

        var result = await client.CreateUploadAudienceGroupAsync("audienceGroupName_01", ["U1", "U2"], "第一批");

        Assert.IsTrue(result.TryGetValue(out var created));
        Assert.AreEqual(1234567890123L, created.AudienceGroupId);
        Assert.AreEqual("UPLOAD", created.Type);
        Assert.AreEqual("MESSAGING_API", created.CreateRoute);
        Assert.AreEqual(1629250278L, created.ExpireTimestamp);
        Assert.IsFalse(created.IsIfaAudience);

        Assert.AreEqual(HttpMethod.Post, handler.Last.Method);
        Assert.AreEqual(LineEndpoints.AudienceGroupUpload, handler.Last.Uri.AbsoluteUri);
        var json = handler.Last.Json();
        Assert.AreEqual("audienceGroupName_01", json.GetProperty("description").GetString());
        Assert.AreEqual("第一批", json.GetProperty("uploadDescription").GetString());
        Assert.AreEqual("U2", json.GetProperty("audiences")[1].GetProperty("id").GetString(), "成員是 {id} 物件,不是裸字串。");
    }

    [TestMethod]
    public async Task CreateUploadAudienceGroupAsync_NoMembers_OmitsAudiences()
    {
        var handler = FakeHttpMessageHandler.Json(Samples.Body("audience-group-created"));
        var client = CreateClient(handler);

        var result = await client.CreateUploadAudienceGroupAsync("空的", []);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsFalse(handler.Last.Json().TryGetProperty("audiences", out _));
    }

    [TestMethod]
    public async Task CreateUploadAudienceGroupAsync_TooManyMembers_FailsWithoutSending()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);
        var members = Enumerable.Range(0, LineMessagingLimits.MaxAudienceMembersPerRequest + 1).Select(i => $"U{i}").ToArray();

        var result = await client.CreateUploadAudienceGroupAsync("多", members);

        Assert.AreEqual(LineErrorCodes.TooManyAudienceMembers, result.Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task AddAudienceGroupMembersAsync_PutsToUploadEndpoint()
    {
        // 加成員是 PUT 到 upload 端點,不是 POST 到 /{id}/members。
        var handler = FakeHttpMessageHandler.Json("{}", HttpStatusCode.Accepted);
        var client = CreateClient(handler);

        var result = await client.AddAudienceGroupMembersAsync(1234567890123, ["U3"]);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(HttpMethod.Put, handler.Last.Method);
        Assert.AreEqual(LineEndpoints.AudienceGroupUpload, handler.Last.Uri.AbsoluteUri);
        var json = handler.Last.Json();
        Assert.AreEqual(1234567890123L, json.GetProperty("audienceGroupId").GetInt64());
        Assert.AreEqual("U3", json.GetProperty("audiences")[0].GetProperty("id").GetString());
        Assert.IsFalse(json.TryGetProperty("uploadDescription", out _));
    }

    [TestMethod]
    public async Task AddAudienceGroupMembersAsync_NoMembers_FailsWithoutSending()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        var result = await client.AddAudienceGroupMembersAsync(1, []);

        Assert.AreEqual(LineErrorCodes.TooManyAudienceMembers, result.Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task GetAudienceGroupAsync_ParsesSampleWithJobs()
    {
        var handler = FakeHttpMessageHandler.Json(Samples.Body("audience-group"));
        var client = CreateClient(handler);

        var result = await client.GetAudienceGroupAsync(1234567890123);

        Assert.IsTrue(result.TryGetValue(out var detail));
        Assert.AreEqual(1234567890123L, detail.AudienceGroup.AudienceGroupId);
        Assert.AreEqual("READY", detail.AudienceGroup.Status);
        Assert.AreEqual(1887L, detail.AudienceGroup.AudienceCount);
        Assert.AreEqual("READ", detail.AudienceGroup.Permission);
        Assert.AreEqual(1, detail.Jobs.Count);
        Assert.AreEqual(12345678L, detail.Jobs[0].AudienceGroupJobId);
        Assert.AreEqual("DIFF_ADD", detail.Jobs[0].Type);
        Assert.AreEqual("FINISHED", detail.Jobs[0].JobStatus);
        Assert.IsNull(detail.Jobs[0].FailedType);
        Assert.AreEqual(LineEndpoints.AudienceGroupBase + "1234567890123", handler.Last.Uri.AbsoluteUri);
    }

    [TestMethod]
    public async Task GetAudienceGroupListAsync_ParsesSampleAndWritesQuery()
    {
        var handler = FakeHttpMessageHandler.Json(Samples.Body("audience-group-list"));
        var client = CreateClient(handler);

        var result = await client.GetAudienceGroupListAsync(page: 2, size: 40, description: "名 稱");

        Assert.IsTrue(result.TryGetValue(out var page));
        Assert.AreEqual(1, page.AudienceGroups.Count);
        Assert.AreEqual("CLICK", page.AudienceGroups[0].Type);
        Assert.AreEqual("OA_MANAGER", page.AudienceGroups[0].CreateRoute);
        Assert.IsFalse(page.HasNextPage);
        Assert.AreEqual(1L, page.TotalCount);
        Assert.AreEqual(40, page.Size);
        Assert.AreEqual(LineEndpoints.AudienceGroupList + "?page=2&size=40&description=%E5%90%8D%20%E7%A8%B1", handler.Last.Uri.AbsoluteUri);
    }

    [TestMethod]
    public async Task GetAudienceGroupListAsync_DefaultsToPageOneSizeTwenty()
    {
        var handler = FakeHttpMessageHandler.Json("""{"audienceGroups":[],"hasNextPage":false,"totalCount":0,"readWriteAudienceGroupTotalCount":0,"page":1,"size":20}""");
        var client = CreateClient(handler);

        var result = await client.GetAudienceGroupListAsync();

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(LineEndpoints.AudienceGroupList + "?page=1&size=20", handler.Last.Uri.AbsoluteUri);
    }

    [TestMethod]
    [DataRow(0, 20)]
    [DataRow(1, 0)]
    [DataRow(1, 41)]
    public async Task GetAudienceGroupListAsync_InvalidPaging_FailsWithoutSending(int page, int size)
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        var result = await client.GetAudienceGroupListAsync(page, size);

        Assert.AreEqual(LineErrorCodes.InvalidPageSize, result.Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task DeleteAudienceGroupAsync_DeletesById()
    {
        var handler = FakeHttpMessageHandler.Json("{}", HttpStatusCode.Accepted);
        var client = CreateClient(handler);

        var result = await client.DeleteAudienceGroupAsync(42);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(HttpMethod.Delete, handler.Last.Method);
        Assert.AreEqual(LineEndpoints.AudienceGroupBase + "42", handler.Last.Uri.AbsoluteUri);
    }

    [TestMethod]
    public async Task NarrowcastAndAudience_NotConfigured_FailWithoutSending()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = new LineMessagingClient(
            new HttpPipelineClient(new HttpClient(handler)),
            Options.Create(new LineMessagingOptions()));

        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.NarrowcastAsync([new TextMessage("x")])).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.GetNarrowcastProgressAsync("r")).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.CreateUploadAudienceGroupAsync("d", ["U1"])).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.AddAudienceGroupMembersAsync(1, ["U1"])).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.GetAudienceGroupAsync(1)).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.GetAudienceGroupListAsync()).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.DeleteAudienceGroupAsync(1)).Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    private static FakeHttpMessageHandler AcceptedWithRequestId(string requestId) =>
        new((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new StringContent("{}") };
            response.Headers.Add("X-Line-Request-Id", requestId);
            return response;
        });

    private static LineMessagingClient CreateClient(FakeHttpMessageHandler handler) =>
        new(
            new HttpPipelineClient(new HttpClient(handler)),
            Options.Create(new LineMessagingOptions { ChannelAccessToken = AccessToken }));
}
