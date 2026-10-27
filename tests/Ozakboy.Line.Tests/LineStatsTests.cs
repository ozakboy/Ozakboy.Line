using System.Net;
using Microsoft.Extensions.Options;
using Ozakboy.Http;
using Ozakboy.Line.Messaging.Insight;
using Ozakboy.Line.Messaging.Messages;
using Ozakboy.Line.Messaging.Narrowcast;
using Ozakboy.Line.Tests.TestSupport;

namespace Ozakboy.Line.Tests;

/// <summary>
/// 投遞統計、單則訊息互動統計與統計單位的測試。全部離線。
/// Tests for delivery counts, per-message engagement statistics and aggregation units. Entirely offline.
/// </summary>
[TestClass]
public sealed class LineStatsTests
{
    private const string AccessToken = "test-channel-access-token";

    [TestMethod]
    [DataRow(LineMessageDeliveryType.Reply, "reply")]
    [DataRow(LineMessageDeliveryType.Push, "push")]
    [DataRow(LineMessageDeliveryType.Multicast, "multicast")]
    [DataRow(LineMessageDeliveryType.Broadcast, "broadcast")]
    public async Task GetMessageDeliveryCountAsync_UsesChannelSegmentAndCompactDate(LineMessageDeliveryType type, string segment)
    {
        var handler = FakeHttpMessageHandler.Json(Samples.Body("message-delivery-count"));
        var client = CreateClient(handler);

        var result = await client.GetMessageDeliveryCountAsync(type, new DateOnly(2026, 3, 9));

        Assert.IsTrue(result.TryGetValue(out var count));
        Assert.AreEqual(LineMessageDeliveryCountStatus.Ready, count.Status);
        Assert.AreEqual(10000L, count.Success);
        Assert.AreEqual(HttpMethod.Get, handler.Last.Method);
        Assert.AreEqual($"{LineEndpoints.MessageDeliveryBase}{segment}?date=20260309", handler.Last.Uri.AbsoluteUri);
    }

    [TestMethod]
    [DataRow("unready", LineMessageDeliveryCountStatus.Unready)]
    [DataRow("out_of_service", LineMessageDeliveryCountStatus.OutOfService)]
    [DataRow("something_new", LineMessageDeliveryCountStatus.Unknown)]
    public async Task GetMessageDeliveryCountAsync_NotReady_LeavesSuccessNullAndNeverThrowsOnNewStatus(string status, LineMessageDeliveryCountStatus expected)
    {
        var client = CreateClient(FakeHttpMessageHandler.Json($$"""{"status":"{{status}}"}"""));

        var result = await client.GetMessageDeliveryCountAsync(LineMessageDeliveryType.Push, new DateOnly(2026, 1, 1));

        Assert.IsTrue(result.TryGetValue(out var count));
        Assert.AreEqual(expected, count.Status);
        Assert.IsNull(count.Success, "還沒統計是 null,不是 0。");
    }

    [TestMethod]
    public async Task GetMessageDeliveryCountAsync_UndefinedType_Throws()
    {
        var client = CreateClient(FakeHttpMessageHandler.Json("{}"));

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
            () => client.GetMessageDeliveryCountAsync((LineMessageDeliveryType)99, new DateOnly(2026, 1, 1)));
    }

    [TestMethod]
    public async Task GetMessageEventInsightAsync_ParsesOverviewMessagesAndClicksKeepingNulls()
    {
        var handler = FakeHttpMessageHandler.Json(Samples.Body("message-event-insight"));
        var client = CreateClient(handler);

        var result = await client.GetMessageEventInsightAsync("req id/1");

        Assert.IsTrue(result.TryGetValue(out var insight));
        Assert.AreEqual(LineEndpoints.InsightMessageEvent + "?requestId=req%20id%2F1", handler.Last.Uri.AbsoluteUri);

        var overview = insight.Overview!;
        Assert.AreEqual("f70dd685-499a-4231-a441-f24b8d4fba21", overview.RequestId);
        Assert.AreEqual(1568214000L, overview.Timestamp);
        Assert.AreEqual(320L, overview.Delivered);
        Assert.AreEqual(82L, overview.UniqueImpression);
        Assert.IsNull(overview.UniqueClick, "不適用的欄位是 null,不是 0。");
        Assert.AreEqual(2L, overview.UniqueMediaPlayed);
        Assert.IsNull(overview.UniqueMediaPlayed100Percent);

        Assert.AreEqual(1, insight.Messages.Count);
        var message = insight.Messages[0];
        Assert.AreEqual(1, message.Seq);
        Assert.AreEqual(100L, message.Impression);
        Assert.AreEqual(30L, message.MediaPlayed);
        Assert.AreEqual(25L, message.MediaPlayed25Percent);
        Assert.AreEqual(20L, message.MediaPlayed50Percent);
        Assert.AreEqual(10L, message.MediaPlayed75Percent);
        Assert.IsNull(message.MediaPlayed100Percent);
        Assert.AreEqual(2L, message.UniqueMediaPlayed);
        Assert.AreEqual(1L, message.UniqueMediaPlayed50Percent);
        Assert.IsNull(message.UniqueMediaPlayed75Percent);

        Assert.AreEqual(2, insight.Clicks.Count);
        Assert.AreEqual("https://example.com/campaign", insight.Clicks[0].Url);
        Assert.AreEqual(7L, insight.Clicks[0].Click);
        Assert.AreEqual(5L, insight.Clicks[0].UniqueClick);
        Assert.IsNull(insight.Clicks[0].UniqueClickOfRequest);
        Assert.IsNull(insight.Clicks[1].Click);
        Assert.AreEqual(9L, insight.Clicks[1].UniqueClickOfRequest);
    }

    [TestMethod]
    public async Task GetMessageEventInsightAsync_EmptyBody_GivesEmptyListsNotNull()
    {
        var client = CreateClient(FakeHttpMessageHandler.Json("{}"));

        var result = await client.GetMessageEventInsightAsync("r");

        Assert.IsTrue(result.TryGetValue(out var insight));
        Assert.IsNull(insight.Overview);
        Assert.AreEqual(0, insight.Messages.Count);
        Assert.AreEqual(0, insight.Clicks.Count);
    }

    [TestMethod]
    public async Task GetMessageEventInsightByUnitAsync_WritesUnitAndCompactDates()
    {
        var handler = FakeHttpMessageHandler.Json(Samples.Body("message-event-aggregation"));
        var client = CreateClient(handler);

        var result = await client.GetMessageEventInsightByUnitAsync("promotion_a", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

        Assert.IsTrue(result.TryGetValue(out var insight));
        Assert.AreEqual(
            LineEndpoints.InsightMessageEventAggregation + "?customAggregationUnit=promotion_a&from=20260901&to=20260930",
            handler.Last.Uri.AbsoluteUri);
        Assert.IsNull(insight.Overview!.RequestId, "依統計單位彙總沒有請求識別碼。");
        Assert.AreEqual(40646L, insight.Overview.UniqueImpression);
        Assert.AreEqual(20L, insight.Overview.UniqueClick);
        Assert.AreEqual(8L, insight.Clicks[0].Click);
    }

    [TestMethod]
    public async Task GetMessageEventInsightByUnitAsync_ThirtyDaysApart_IsAllowed()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        var result = await client.GetMessageEventInsightByUnitAsync("u", new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(1, handler.Requests.Count);
    }

    [TestMethod]
    public async Task GetMessageEventInsightByUnitAsync_RangeTooLongOrReversed_FailsWithoutSending()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        var tooLong = await client.GetMessageEventInsightByUnitAsync("u", new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 2));
        var reversed = await client.GetMessageEventInsightByUnitAsync("u", new DateOnly(2026, 9, 2), new DateOnly(2026, 9, 1));

        Assert.AreEqual(LineErrorCodes.InvalidDateRange, tooLong.Error!.Code);
        Assert.AreEqual(LineErrorCodes.InvalidDateRange, reversed.Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("has space")]
    [DataRow("中文單位")]
    [DataRow("dash-name")]
    [DataRow("0123456789012345678901234567890")]
    public async Task GetMessageEventInsightByUnitAsync_BadUnitName_FailsWithoutSending(string unit)
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        if (unit.Length == 0)
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => client.GetMessageEventInsightByUnitAsync(unit, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2)));
            return;
        }

        var result = await client.GetMessageEventInsightByUnitAsync(unit, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2));

        Assert.AreEqual(LineErrorCodes.InvalidAggregationUnit, result.Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task GetAggregationUnitUsageAsync_ReturnsCount()
    {
        var handler = FakeHttpMessageHandler.Json(Samples.Body("aggregation-unit-usage"));
        var client = CreateClient(handler);

        var result = await client.GetAggregationUnitUsageAsync();

        Assert.IsTrue(result.TryGetValue(out var used));
        Assert.AreEqual(22, used);
        Assert.AreEqual(LineEndpoints.MessageAggregationInfo, handler.Last.Uri.AbsoluteUri);
    }

    [TestMethod]
    public async Task GetAggregationUnitNamesAsync_NoArguments_HasNoQuery()
    {
        var handler = FakeHttpMessageHandler.Json(Samples.Body("aggregation-unit-names"));
        var client = CreateClient(handler);

        var result = await client.GetAggregationUnitNamesAsync();

        Assert.IsTrue(result.TryGetValue(out var page));
        Assert.AreEqual(2, page.CustomAggregationUnits.Count);
        Assert.AreEqual("promotion_a", page.CustomAggregationUnits[0]);
        Assert.AreEqual("promotion_b", page.CustomAggregationUnits[1]);
        Assert.AreEqual("MTc5Nzg4MjAwMDAw", page.Next);
        Assert.AreEqual(LineEndpoints.MessageAggregationList, handler.Last.Uri.AbsoluteUri);
    }

    [TestMethod]
    public async Task GetAggregationUnitNamesAsync_WithLimitAndStart_WritesBoth()
    {
        var handler = FakeHttpMessageHandler.Json("""{"customAggregationUnits":[]}""");
        var client = CreateClient(handler);

        var result = await client.GetAggregationUnitNamesAsync(100, "a b");

        Assert.IsTrue(result.TryGetValue(out var page));
        Assert.IsNull(page.Next);
        Assert.AreEqual(LineEndpoints.MessageAggregationList + "?limit=100&start=a%20b", handler.Last.Uri.AbsoluteUri);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(101)]
    [DataRow(-1)]
    public async Task GetAggregationUnitNamesAsync_LimitOutOfRange_FailsWithoutSending(int limit)
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        var result = await client.GetAggregationUnitNamesAsync(limit);

        Assert.AreEqual(LineErrorCodes.InvalidPageSize, result.Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task Stats_NotConfigured_FailsWithoutSending()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = new LineMessagingClient(
            new HttpPipelineClient(new HttpClient(handler)),
            Options.Create(new LineMessagingOptions()));

        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.GetMessageDeliveryCountAsync(LineMessageDeliveryType.Push, new DateOnly(2026, 1, 1))).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.GetMessageEventInsightAsync("r")).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.GetMessageEventInsightByUnitAsync("u", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2))).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.GetAggregationUnitUsageAsync()).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.GetAggregationUnitNamesAsync()).Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task Push_WithAggregationUnit_WritesArrayInBody()
    {
        var handler = FakeHttpMessageHandler.Json("""{"sentMessages":[]}""");
        var client = CreateClient(handler);

        await client.PushAsync("U1", [new TextMessage("x")], new LinePushOptions { CustomAggregationUnits = ["promotion_a"] });

        var units = handler.Last.Json().GetProperty("customAggregationUnits");
        Assert.AreEqual(1, units.GetArrayLength());
        Assert.AreEqual("promotion_a", units[0].GetString());
    }

    [TestMethod]
    public async Task Multicast_Broadcast_AndRawJson_WriteAggregationUnit()
    {
        var handler = FakeHttpMessageHandler.Json("""{"sentMessages":[]}""");
        var client = CreateClient(handler);
        var options = new LinePushOptions { CustomAggregationUnits = ["u_1"] };

        await client.MulticastAsync(["U1"], [new TextMessage("x")], options);
        Assert.AreEqual("u_1", handler.Last.Json().GetProperty("customAggregationUnits")[0].GetString());

        await client.BroadcastAsync([new TextMessage("x")], options);
        Assert.AreEqual("u_1", handler.Last.Json().GetProperty("customAggregationUnits")[0].GetString());

        await client.PushRawJsonAsync("U1", """{"type":"text","text":"x"}""", options);
        Assert.AreEqual("u_1", handler.Last.Json().GetProperty("customAggregationUnits")[0].GetString());

        await client.BroadcastRawJsonAsync("""{"type":"text","text":"x"}""", options);
        Assert.AreEqual("u_1", handler.Last.Json().GetProperty("customAggregationUnits")[0].GetString());
    }

    [TestMethod]
    public async Task Narrowcast_WithAggregationUnit_WritesArrayInBody()
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new StringContent("{}") };
            response.Headers.Add("X-Line-Request-Id", "req-1");
            return response;
        });
        var client = CreateClient(handler);

        var result = await client.NarrowcastAsync(
            [new TextMessage("x")],
            new LineNarrowcastOptions { CustomAggregationUnits = ["promotion_a"] });

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("promotion_a", handler.Last.Json().GetProperty("customAggregationUnits")[0].GetString());
    }

    [TestMethod]
    public async Task Push_WithoutAggregationUnitOrEmptyList_OmitsField()
    {
        var handler = FakeHttpMessageHandler.Json("""{"sentMessages":[]}""");
        var client = CreateClient(handler);

        await client.PushAsync("U1", [new TextMessage("x")]);
        Assert.IsFalse(handler.Last.Json().TryGetProperty("customAggregationUnits", out _));

        await client.PushAsync("U1", [new TextMessage("x")], new LinePushOptions { CustomAggregationUnits = [] });
        Assert.IsFalse(handler.Last.Json().TryGetProperty("customAggregationUnits", out _));
    }

    [TestMethod]
    public async Task Send_WithTwoOrInvalidAggregationUnits_FailsWithoutSending()
    {
        var handler = FakeHttpMessageHandler.Json("""{"sentMessages":[]}""");
        var client = CreateClient(handler);

        var two = await client.PushAsync("U1", [new TextMessage("x")], new LinePushOptions { CustomAggregationUnits = ["a", "b"] });
        var bad = await client.BroadcastAsync([new TextMessage("x")], new LinePushOptions { CustomAggregationUnits = ["bad name"] });
        var multicast = await client.MulticastAsync(["U1"], [new TextMessage("x")], new LinePushOptions { CustomAggregationUnits = [new string('a', 31)] });
        var narrowcast = await client.NarrowcastAsync([new TextMessage("x")], new LineNarrowcastOptions { CustomAggregationUnits = ["a", "b"] });

        Assert.AreEqual(LineErrorCodes.InvalidAggregationUnit, two.Error!.Code);
        Assert.AreEqual(LineErrorCodes.InvalidAggregationUnit, bad.Error!.Code);
        Assert.AreEqual(LineErrorCodes.InvalidAggregationUnit, multicast.Error!.Code);
        Assert.AreEqual(LineErrorCodes.InvalidAggregationUnit, narrowcast.Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    private static LineMessagingClient CreateClient(FakeHttpMessageHandler handler) =>
        new(
            new HttpPipelineClient(new HttpClient(handler)),
            Options.Create(new LineMessagingOptions { ChannelAccessToken = AccessToken }));
}
