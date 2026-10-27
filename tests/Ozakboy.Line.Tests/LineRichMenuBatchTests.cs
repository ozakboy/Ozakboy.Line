using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Ozakboy.Http;
using Ozakboy.Line.Messaging.Actions;
using Ozakboy.Line.Messaging.RichMenu;
using Ozakboy.Line.Tests.TestSupport;

namespace Ozakboy.Line.Tests;

/// <summary>
/// 圖文選單批次作業的測試,以及讀回圖文選單時九種動作都還原正確的測試。全部離線。
/// Tests for rich menu batch operations, and for all nine action types coming back correctly when a rich menu is
/// read. Entirely offline.
/// </summary>
[TestClass]
public sealed class LineRichMenuBatchTests
{
    private const string AccessToken = "test-channel-access-token";

    [TestMethod]
    public async Task ReplaceOrUnlinkRichMenusInBatchAsync_WritesOperationsAndReturnsRequestIdHeader()
    {
        var handler = AcceptedWithRequestId("batch-req-1");
        var client = CreateClient(handler);

        var result = await client.ReplaceOrUnlinkRichMenusInBatchAsync(
        [
            LineRichMenuBatchOperation.Link("richmenu-old", "richmenu-new"),
            LineRichMenuBatchOperation.Unlink("richmenu-gone"),
            LineRichMenuBatchOperation.UnlinkAll(),
        ]);

        Assert.IsTrue(result.TryGetValue(out var requestId));
        Assert.AreEqual("batch-req-1", requestId, "請求識別碼在回應標頭,不在內容裡。");
        Assert.AreEqual(HttpMethod.Post, handler.Last.Method);
        Assert.AreEqual(LineEndpoints.RichMenuBatch, handler.Last.Uri.AbsoluteUri);

        var json = handler.Last.Json();
        var operations = json.GetProperty("operations");
        Assert.AreEqual(3, operations.GetArrayLength());

        Assert.AreEqual("link", operations[0].GetProperty("type").GetString());
        Assert.AreEqual("richmenu-old", operations[0].GetProperty("from").GetString());
        Assert.AreEqual("richmenu-new", operations[0].GetProperty("to").GetString());

        Assert.AreEqual("unlink", operations[1].GetProperty("type").GetString());
        Assert.AreEqual("richmenu-gone", operations[1].GetProperty("from").GetString());
        Assert.IsFalse(operations[1].TryGetProperty("to", out _));

        Assert.AreEqual("unlinkAll", operations[2].GetProperty("type").GetString());
        Assert.IsFalse(operations[2].TryGetProperty("from", out _));

        Assert.IsFalse(json.TryGetProperty("resumeRequestKey", out _), "沒有續跑鍵就不寫。");
    }

    [TestMethod]
    public async Task ReplaceOrUnlinkRichMenusInBatchAsync_WithResumeRequestKey_WritesIt()
    {
        var handler = AcceptedWithRequestId("batch-req-2");
        var client = CreateClient(handler);

        await client.ReplaceOrUnlinkRichMenusInBatchAsync([LineRichMenuBatchOperation.UnlinkAll()], "earlier-request");

        Assert.AreEqual("earlier-request", handler.Last.Json().GetProperty("resumeRequestKey").GetString());
    }

    [TestMethod]
    public async Task ReplaceOrUnlinkRichMenusInBatchAsync_NoRetryKeyOrIdempotency()
    {
        var handler = AcceptedWithRequestId("batch-req-3");
        var client = CreateClient(handler);

        await client.ReplaceOrUnlinkRichMenusInBatchAsync([LineRichMenuBatchOperation.UnlinkAll()]);

        Assert.IsFalse(handler.Last.Headers.ContainsKey("X-Line-Retry-Key"));
    }

    [TestMethod]
    public async Task ReplaceOrUnlinkRichMenusInBatchAsync_MissingRequestIdHeader_FailsAsInvalidResponse()
    {
        var client = CreateClient(FakeHttpMessageHandler.Json("{}", HttpStatusCode.Accepted));

        var result = await client.ReplaceOrUnlinkRichMenusInBatchAsync([LineRichMenuBatchOperation.UnlinkAll()]);

        Assert.AreEqual(LineErrorCodes.ApiInvalidResponse, result.Error!.Code);
    }

    [TestMethod]
    public async Task ReplaceOrUnlinkRichMenusInBatchAsync_LineRejects_MapsToLineError()
    {
        var handler = new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"message":"The request body has 1 error(s)"}""", System.Text.Encoding.UTF8, "application/json"),
        });
        var client = CreateClient(handler);

        var result = await client.ReplaceOrUnlinkRichMenusInBatchAsync([LineRichMenuBatchOperation.UnlinkAll()]);

        Assert.AreEqual(LineErrorCodes.ApiError, result.Error!.Code);
    }

    [TestMethod]
    public async Task ReplaceOrUnlinkRichMenusInBatchAsync_Boundaries()
    {
        var handler = AcceptedWithRequestId("r");
        var client = CreateClient(handler);

        var empty = await client.ReplaceOrUnlinkRichMenusInBatchAsync([]);
        var over = await client.ReplaceOrUnlinkRichMenusInBatchAsync(Operations(LineMessagingLimits.MaxRichMenuBatchOperations + 1));
        var withNull = await client.ReplaceOrUnlinkRichMenusInBatchAsync([LineRichMenuBatchOperation.UnlinkAll(), null!]);

        Assert.AreEqual(LineErrorCodes.InvalidRichMenuBatch, empty.Error!.Code);
        Assert.AreEqual(LineErrorCodes.InvalidRichMenuBatch, over.Error!.Code);
        Assert.AreEqual(LineErrorCodes.InvalidRichMenuBatch, withNull.Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count, "本地檢查失敗時一個請求都不送。");

        var atLimit = await client.ReplaceOrUnlinkRichMenusInBatchAsync(Operations(LineMessagingLimits.MaxRichMenuBatchOperations));
        Assert.IsTrue(atLimit.IsSuccess);
        Assert.AreEqual(1000, handler.Last.Json().GetProperty("operations").GetArrayLength());
    }

    [TestMethod]
    public void Operations_BlankIds_Throw()
    {
        Assert.ThrowsExactly<ArgumentException>(() => LineRichMenuBatchOperation.Link(" ", "b"));
        Assert.ThrowsExactly<ArgumentException>(() => LineRichMenuBatchOperation.Link("a", ""));
        Assert.ThrowsExactly<ArgumentException>(() => LineRichMenuBatchOperation.Unlink(" "));
    }

    [TestMethod]
    public async Task ValidateRichMenuBatchAsync_PostsToValidateEndpoint()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        var result = await client.ValidateRichMenuBatchAsync([LineRichMenuBatchOperation.Link("a", "b")]);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(LineEndpoints.RichMenuValidateBatch, handler.Last.Uri.AbsoluteUri);
        Assert.AreEqual("link", handler.Last.Json().GetProperty("operations")[0].GetProperty("type").GetString());
    }

    [TestMethod]
    public async Task ValidateRichMenuBatchAsync_EmptyBatch_FailsWithoutSending()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        var result = await client.ValidateRichMenuBatchAsync([]);

        Assert.AreEqual(LineErrorCodes.InvalidRichMenuBatch, result.Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task GetRichMenuBatchProgressAsync_Succeeded_ParsesSample()
    {
        var handler = FakeHttpMessageHandler.Json(Samples.Body("richmenu-batch-progress"));
        var client = CreateClient(handler);

        var result = await client.GetRichMenuBatchProgressAsync("batch req/1");

        Assert.IsTrue(result.TryGetValue(out var progress));
        Assert.AreEqual(LineRichMenuBatchPhase.Succeeded, progress.Phase);
        Assert.AreEqual("2026-10-27T01:00:00.000+00:00", progress.AcceptedTime);
        Assert.AreEqual("2026-10-27T01:00:12.345+00:00", progress.CompletedTime);
        Assert.AreEqual(LineEndpoints.RichMenuBatchProgress + "?requestId=batch%20req%2F1", handler.Last.Uri.AbsoluteUri);
    }

    [TestMethod]
    public async Task GetRichMenuBatchProgressAsync_Ongoing_HasNoCompletedTime()
    {
        var client = CreateClient(FakeHttpMessageHandler.Json(Samples.Body("richmenu-batch-progress-ongoing")));

        var result = await client.GetRichMenuBatchProgressAsync("r");

        Assert.IsTrue(result.TryGetValue(out var progress));
        Assert.AreEqual(LineRichMenuBatchPhase.Ongoing, progress.Phase);
        Assert.IsNull(progress.CompletedTime);
    }

    [TestMethod]
    [DataRow("failed", LineRichMenuBatchPhase.Failed)]
    [DataRow("brand_new_phase", LineRichMenuBatchPhase.Unknown)]
    public async Task GetRichMenuBatchProgressAsync_PhaseMapping(string phase, LineRichMenuBatchPhase expected)
    {
        var client = CreateClient(FakeHttpMessageHandler.Json($$"""{"phase":"{{phase}}","acceptedTime":"t"}"""));

        var result = await client.GetRichMenuBatchProgressAsync("r");

        Assert.IsTrue(result.TryGetValue(out var progress));
        Assert.AreEqual(expected, progress.Phase);
    }

    [TestMethod]
    public async Task RichMenuBatch_NotConfigured_FailsWithoutSending()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = new LineMessagingClient(
            new HttpPipelineClient(new HttpClient(handler)),
            Options.Create(new LineMessagingOptions()));

        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.ReplaceOrUnlinkRichMenusInBatchAsync([LineRichMenuBatchOperation.UnlinkAll()])).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.ValidateRichMenuBatchAsync([LineRichMenuBatchOperation.UnlinkAll()])).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.GetRichMenuBatchProgressAsync("r")).Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task GetRichMenuAsync_NineActionTypes_EachAreaKeepsItsOwnTypeAndFields()
    {
        // 別家套件的轉換器讀回來永遠是第一個型別;這裡確認九個區塊各自是各自的動作,欄位一個不少。
        // Another package's converter always came back as the first type; this checks that each of the nine areas
        // is its own action with every field intact.
        var client = CreateClient(FakeHttpMessageHandler.Json(Samples.Body("richmenu-nine-actions")));

        var result = await client.GetRichMenuAsync("richmenu-test-0001");

        Assert.IsTrue(result.TryGetValue(out var menu));
        Assert.AreEqual(9, menu.Areas.Count);

        var expectedTypes = new[]
        {
            "message", "uri", "postback", "datetimepicker", "camera", "cameraRoll", "location", "richmenuswitch", "clipboard",
        };
        for (var index = 0; index < expectedTypes.Length; index++)
        {
            var action = menu.Areas[index].Action;
            Assert.IsInstanceOfType<RawAction>(action, "讀回的動作一律是保留全部內容的 RawAction。");
            Assert.AreEqual(expectedTypes[index], action.Type, $"第 {index} 個區塊的動作型別。");
            Assert.AreEqual(expectedTypes[index], ((RawAction)action).Contents.GetProperty("type").GetString());
        }

        CollectionAssert.AllItemsAreUnique(menu.Areas.Select(area => area.Action.Type).ToArray());

        string Field(int index, string name) => ((RawAction)menu.Areas[index].Action).Contents.GetProperty(name).GetString()!;

        Assert.AreEqual("你好", Field(0, "text"));
        Assert.AreEqual("https://example.com/", Field(1, "uri"));
        Assert.AreEqual("action=buy&itemid=1", Field(2, "data"));
        Assert.AreEqual("買", Field(2, "displayText"));
        Assert.AreEqual("date", Field(3, "mode"));
        Assert.AreEqual("2026-10-27", Field(3, "initial"));
        Assert.AreEqual("相機", Field(4, "label"));
        Assert.AreEqual("相簿", Field(5, "label"));
        Assert.AreEqual("位置", Field(6, "label"));
        Assert.AreEqual("richmenu-alias-b", Field(7, "richMenuAliasId"));
        Assert.AreEqual("richmenu-changed-to-b", Field(7, "data"));
        Assert.AreEqual("COUPON2026", Field(8, "clipboardText"));

        Assert.AreEqual(833, menu.Areas[0].Bounds.Width);
        Assert.AreEqual(1124, menu.Areas[8].Bounds.Y);
    }

    private static LineRichMenuBatchOperation[] Operations(int count) =>
        Enumerable.Range(0, count).Select(i => LineRichMenuBatchOperation.Unlink("richmenu-" + i)).ToArray();

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
