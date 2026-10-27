using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Ozakboy.Http;
using Ozakboy.Http.Retry;
using Ozakboy.Line.Messaging.Actions;
using Ozakboy.Line.Messaging.Messages;
using Ozakboy.Line.Tests.TestSupport;

namespace Ozakboy.Line.Tests;

/// <summary>
/// 超量自動分批發送(<c>PushManyAsync</c> / <c>MulticastManyAsync</c>)的測試。全部離線。
/// Tests for the automatic batching of oversized sends (<c>PushManyAsync</c> / <c>MulticastManyAsync</c>).
/// Entirely offline.
/// </summary>
[TestClass]
public sealed class LineBulkSendTests
{
    private const string AccessToken = "test-channel-access-token";

    [TestMethod]
    [DataRow(1, new[] { 1 })]
    [DataRow(5, new[] { 5 })]
    [DataRow(6, new[] { 5, 1 })]
    [DataRow(10, new[] { 5, 5 })]
    [DataRow(11, new[] { 5, 5, 1 })]
    public async Task PushManyAsync_SplitsEveryFiveMessagesInOrder(int total, int[] expectedBatchSizes)
    {
        var handler = SendingHandler();
        var client = CreateClient(handler);

        var result = await client.PushManyAsync("U1", Texts(total));

        Assert.IsTrue(result.TryGetValue(out var sent));
        Assert.AreEqual(expectedBatchSizes.Length, handler.Requests.Count);
        Assert.AreEqual(expectedBatchSizes.Length, sent.BatchCount);
        Assert.AreEqual(total, sent.TotalMessages);

        var seen = new List<string>();
        for (var index = 0; index < handler.Requests.Count; index++)
        {
            var json = handler.Requests[index].Json();
            Assert.AreEqual(LineEndpoints.PushMessage, handler.Requests[index].Uri.AbsoluteUri);
            Assert.AreEqual("U1", json.GetProperty("to").GetString());
            Assert.AreEqual(expectedBatchSizes[index], json.GetProperty("messages").GetArrayLength());
            seen.AddRange(json.GetProperty("messages").EnumerateArray().Select(m => m.GetProperty("text").GetString()!));
        }

        CollectionAssert.AreEqual(Enumerable.Range(0, total).Select(i => "m" + i).ToArray(), seen, "順序不得被打亂,也不得有訊息被丟掉。");
        Assert.AreEqual(total, sent.SentMessages.Count, "已送出的訊息依批次串接。");
    }

    [TestMethod]
    public async Task PushManyAsync_SendsBatchesOneAfterAnother()
    {
        var concurrent = 0;
        var peak = 0;
        var handler = new FakeHttpMessageHandler((_, _) =>
        {
            peak = Math.Max(peak, Interlocked.Increment(ref concurrent));
            Interlocked.Decrement(ref concurrent);
            return Ok("""{"sentMessages":[]}""");
        });
        var client = CreateClient(handler);

        await client.PushManyAsync("U1", Texts(21));

        Assert.AreEqual(5, handler.Requests.Count);
        Assert.AreEqual(1, peak, "批次必須依序送,不可平行。");
    }

    [TestMethod]
    public async Task PushManyAsync_SecondBatchFails_StopsAndReportsProgress()
    {
        var handler = SendingHandler(failOnRequest: 2);
        var client = CreateClient(handler);

        var result = await client.PushManyAsync("U1", Texts(16));

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(2, handler.Requests.Count, "第二批失敗後不得再送第三批。");
        Assert.AreEqual(LineErrorCodes.ApiError, result.Error!.Code, "失敗批原本的代碼保持不變。");
        Assert.IsTrue(result.Error.TryGetInt64(LineBulkSendErrorDataKeys.SentBatches, out var sentBatches));
        Assert.IsTrue(result.Error.TryGetInt64(LineBulkSendErrorDataKeys.FailedBatchIndex, out var failedIndex));
        Assert.AreEqual(1L, sentBatches);
        Assert.AreEqual(1L, failedIndex);
        Assert.IsTrue(result.Error.TryGetData("statusCode", out _), "Ozakboy.Http 放進去的資料鍵要保留。");
    }

    [TestMethod]
    public async Task PushManyAsync_FirstBatchFails_ReportsZeroSent()
    {
        var handler = SendingHandler(failOnRequest: 1);
        var client = CreateClient(handler);

        var result = await client.PushManyAsync("U1", Texts(7));

        Assert.IsTrue(result.Error!.TryGetInt64(LineBulkSendErrorDataKeys.SentBatches, out var sentBatches));
        Assert.AreEqual(0L, sentBatches);
        Assert.AreEqual(1, handler.Requests.Count);
    }

    [TestMethod]
    public async Task PushManyAsync_InvalidMessageInLaterBatch_SendsNothing()
    {
        var handler = SendingHandler();
        var client = CreateClient(handler);
        var tooManyButtons = new TextMessage("壞的") { QuickReply = new LineQuickReply() };
        for (var index = 0; index <= LineMessagingLimits.MaxQuickReplyItems; index++)
        {
            tooManyButtons.QuickReply.Items.Add(new LineQuickReplyItem(new MessageAction("好")));
        }

        var messages = Texts(8).Concat([tooManyButtons]).ToList();

        var result = await client.PushManyAsync("U1", messages);

        Assert.AreEqual(LineErrorCodes.TooManyQuickReplyItems, result.Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count, "整份輸入先驗完才送第一批。");
    }

    [TestMethod]
    public async Task PushManyAsync_NoMessages_FailsLikeTheOneToFiveCheck()
    {
        var handler = SendingHandler();
        var client = CreateClient(handler);

        var result = await client.PushManyAsync("U1", []);

        Assert.AreEqual(LineErrorCodes.TooManyMessages, result.Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task PushManyAsync_BadAggregationUnit_SendsNothing()
    {
        var handler = SendingHandler();
        var client = CreateClient(handler);

        var result = await client.PushManyAsync("U1", Texts(12), new LinePushOptions { CustomAggregationUnits = ["a", "b"] });

        Assert.AreEqual(LineErrorCodes.InvalidAggregationUnit, result.Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task PushManyAsync_CarriesNotificationAndAggregationUnitIntoEveryBatch()
    {
        var handler = SendingHandler();
        var client = CreateClient(handler);

        await client.PushManyAsync(
            "U1",
            Texts(11),
            new LinePushOptions { NotificationDisabled = true, CustomAggregationUnits = ["promo"] });

        Assert.AreEqual(3, handler.Requests.Count);
        foreach (var request in handler.Requests)
        {
            var json = request.Json();
            Assert.IsTrue(json.GetProperty("notificationDisabled").GetBoolean());
            Assert.AreEqual("promo", json.GetProperty("customAggregationUnits")[0].GetString());
        }
    }

    [TestMethod]
    public async Task PushManyAsync_WithoutRetryKey_NoBatchCarriesKeyOrIsIdempotent()
    {
        var handler = SendingHandler();
        var client = CreateClient(handler);

        await client.PushManyAsync("U1", Texts(11));

        foreach (var request in handler.Requests)
        {
            Assert.IsFalse(request.Headers.ContainsKey("X-Line-Retry-Key"));
            Assert.AreNotEqual(RequestIdempotency.Idempotent, request.Idempotency);
        }
    }

    [TestMethod]
    public async Task PushManyAsync_WithRetryKey_EachBatchHasItsOwnReproducibleKey()
    {
        var retryKey = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var first = SendingHandler();
        var second = SendingHandler();

        await CreateClient(first).PushManyAsync("U1", Texts(11), new LinePushOptions { RetryKey = retryKey });
        await CreateClient(second).PushManyAsync("U1", Texts(11), new LinePushOptions { RetryKey = retryKey });

        var firstKeys = first.Requests.Select(r => r.Headers["X-Line-Retry-Key"]).ToArray();
        var secondKeys = second.Requests.Select(r => r.Headers["X-Line-Retry-Key"]).ToArray();

        Assert.AreEqual(3, firstKeys.Length);
        CollectionAssert.AllItemsAreUnique(firstKeys, "各批的鍵必須不同,否則第二批會被當成第一批的重複。");
        CollectionAssert.AreEqual(firstKeys, secondKeys, "重跑同一個呼叫要得到同樣的鍵,LINE 才去得了重。");
        Assert.IsFalse(firstKeys.Contains(retryKey.ToString("D")), "批次鍵是推導出來的,不是原鍵。");
        Assert.IsTrue(first.Requests.All(r => r.Idempotency == RequestIdempotency.Idempotent));
        Assert.AreEqual(LineBulkSendRetryKey.Derive(retryKey, 2).ToString("D"), firstKeys[2]);
    }

    [TestMethod]
    public void RetryKeyDerive_IsDeterministicDistinctAndWellFormed()
    {
        var key = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var other = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeef");

        var derived = Enumerable.Range(0, 50).Select(i => LineBulkSendRetryKey.Derive(key, i)).ToArray();

        CollectionAssert.AreEqual(derived, Enumerable.Range(0, 50).Select(i => LineBulkSendRetryKey.Derive(key, i)).ToArray());
        Assert.AreEqual(derived.Length, derived.Distinct().Count());
        Assert.AreNotEqual(LineBulkSendRetryKey.Derive(key, 0), LineBulkSendRetryKey.Derive(other, 0));

        foreach (var value in derived)
        {
            var text = value.ToString("D");
            Assert.AreEqual('5', text[14], "版本位元是 5。");
            Assert.IsTrue("89ab".Contains(text[19], StringComparison.Ordinal), "變體位元是 RFC 4122。");
        }

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => LineBulkSendRetryKey.Derive(key, -1));
    }

    [TestMethod]
    [DataRow(1, new[] { 1 })]
    [DataRow(500, new[] { 500 })]
    [DataRow(501, new[] { 500, 1 })]
    [DataRow(1000, new[] { 500, 500 })]
    [DataRow(1001, new[] { 500, 500, 1 })]
    public async Task MulticastManyAsync_SplitsEvery500Recipients(int recipients, int[] expectedBatchSizes)
    {
        var handler = SendingHandler();
        var client = CreateClient(handler);
        var ids = Enumerable.Range(0, recipients).Select(i => "U" + i).ToArray();

        var result = await client.MulticastManyAsync(ids, Texts(2));

        Assert.IsTrue(result.TryGetValue(out var sent));
        Assert.AreEqual(expectedBatchSizes.Length, handler.Requests.Count);
        Assert.AreEqual(expectedBatchSizes.Length, sent.BatchCount);
        Assert.AreEqual(2, sent.TotalMessages);
        Assert.AreEqual(0, sent.SentMessages.Count, "群發端點不回已送出的訊息。");

        var seen = new List<string>();
        for (var index = 0; index < handler.Requests.Count; index++)
        {
            Assert.AreEqual(LineEndpoints.MulticastMessage, handler.Requests[index].Uri.AbsoluteUri);
            var to = handler.Requests[index].Json().GetProperty("to");
            Assert.AreEqual(expectedBatchSizes[index], to.GetArrayLength());
            seen.AddRange(to.EnumerateArray().Select(e => e.GetString()!));
        }

        CollectionAssert.AreEqual(ids, seen, "收件人順序不得被打亂,也不得有人被丟掉。");
    }

    [TestMethod]
    public async Task MulticastManyAsync_RecipientBatchesOutsideMessageBatchesInside()
    {
        var handler = SendingHandler();
        var client = CreateClient(handler);
        var ids = Enumerable.Range(0, 501).Select(i => "U" + i).ToArray();

        var result = await client.MulticastManyAsync(ids, Texts(6));

        Assert.IsTrue(result.TryGetValue(out var sent));
        Assert.AreEqual(4, sent.BatchCount);

        var shape = handler.Requests
            .Select(r => (To: r.Json().GetProperty("to").GetArrayLength(), Messages: r.Json().GetProperty("messages").GetArrayLength()))
            .ToArray();
        CollectionAssert.AreEqual(new[] { (500, 5), (500, 1), (1, 5), (1, 1) }, shape);
    }

    [TestMethod]
    public async Task MulticastManyAsync_SecondBatchFails_ReportsFlattenedIndex()
    {
        var handler = SendingHandler(failOnRequest: 2);
        var client = CreateClient(handler);
        var ids = Enumerable.Range(0, 501).Select(i => "U" + i).ToArray();

        var result = await client.MulticastManyAsync(ids, Texts(6));

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(2, handler.Requests.Count);
        Assert.IsTrue(result.Error!.TryGetInt64(LineBulkSendErrorDataKeys.SentBatches, out var sentBatches));
        Assert.IsTrue(result.Error.TryGetInt64(LineBulkSendErrorDataKeys.FailedBatchIndex, out var failedIndex));
        Assert.AreEqual(1L, sentBatches);
        Assert.AreEqual(1L, failedIndex);
    }

    [TestMethod]
    public async Task MulticastManyAsync_WithRetryKey_UsesFlattenedBatchIndexForDerivation()
    {
        var retryKey = Guid.NewGuid();
        var handler = SendingHandler();
        var client = CreateClient(handler);
        var ids = Enumerable.Range(0, 501).Select(i => "U" + i).ToArray();

        await client.MulticastManyAsync(ids, Texts(6), new LinePushOptions { RetryKey = retryKey });

        var keys = handler.Requests.Select(r => r.Headers["X-Line-Retry-Key"]).ToArray();
        CollectionAssert.AllItemsAreUnique(keys);
        for (var index = 0; index < keys.Length; index++)
        {
            Assert.AreEqual(LineBulkSendRetryKey.Derive(retryKey, index).ToString("D"), keys[index]);
        }
    }

    [TestMethod]
    public async Task MulticastManyAsync_NoRecipients_FailsWithNoRecipientsAndSendsNothing()
    {
        var handler = SendingHandler();
        var client = CreateClient(handler);

        var result = await client.MulticastManyAsync([], Texts(1));

        Assert.AreEqual(LineErrorCodes.NoRecipients, result.Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task MulticastManyAsync_NoMessages_FailsWithoutSending()
    {
        var handler = SendingHandler();
        var client = CreateClient(handler);

        var result = await client.MulticastManyAsync(["U1"], []);

        Assert.AreEqual(LineErrorCodes.TooManyMessages, result.Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task BulkSend_NotConfigured_FailsWithoutSending()
    {
        var handler = SendingHandler();
        var client = new LineMessagingClient(
            new HttpPipelineClient(new HttpClient(handler)),
            Options.Create(new LineMessagingOptions()));

        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.PushManyAsync("U1", Texts(8))).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.MulticastManyAsync(["U1"], Texts(8))).Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    private static List<LineMessage> Texts(int count) =>
        Enumerable.Range(0, count).Select(i => (LineMessage)new TextMessage("m" + i)).ToList();

    private static HttpResponseMessage Ok(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    /// <summary>
    /// 推播回與請求等量的 sentMessages;第 <paramref name="failOnRequest"/> 個請求(從 1 起算)回 400。
    /// Answers a push with as many sentMessages as the request carried, and the request numbered
    /// <paramref name="failOnRequest"/> (from 1) with a 400.
    /// </summary>
    private static FakeHttpMessageHandler SendingHandler(int failOnRequest = 0)
    {
        var seen = 0;
        return new FakeHttpMessageHandler((request, body) =>
        {
            seen++;
            if (seen == failOnRequest)
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("""{"message":"The request body has 1 error(s)"}""", System.Text.Encoding.UTF8, "application/json"),
                };
            }

            if (request.RequestUri!.AbsoluteUri == LineEndpoints.PushMessage)
            {
                using var document = JsonDocument.Parse(body!);
                var count = document.RootElement.GetProperty("messages").GetArrayLength();
                var entries = string.Join(',', Enumerable.Range(0, count).Select(i => $$"""{"id":"{{seen}}-{{i}}"}"""));
                return Ok($$"""{"sentMessages":[{{entries}}]}""");
            }

            return Ok("{}");
        });
    }

    private static LineMessagingClient CreateClient(FakeHttpMessageHandler handler) =>
        new(
            new HttpPipelineClient(new HttpClient(handler)),
            Options.Create(new LineMessagingOptions { ChannelAccessToken = AccessToken }));
}
