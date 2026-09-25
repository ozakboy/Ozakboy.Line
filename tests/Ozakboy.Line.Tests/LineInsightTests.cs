using Microsoft.Extensions.Options;
using Ozakboy.Http;
using Ozakboy.Line.Messaging.Insight;
using Ozakboy.Line.Tests.TestSupport;

namespace Ozakboy.Line.Tests;

/// <summary>
/// 成效洞察端點的測試:日期格式與三種回應的反序列化。全部離線。
/// Tests for the insight endpoints: the date format and the deserialisation of the three responses. Entirely
/// offline.
/// </summary>
[TestClass]
public sealed class LineInsightTests
{
    private const string AccessToken = "test-channel-access-token";

    [TestMethod]
    public async Task GetMessageDeliveryInsightAsync_FormatsDateAndParsesSample()
    {
        var handler = FakeHttpMessageHandler.Json(Samples.Body("insight-message-delivery"));
        var client = CreateClient(handler);

        var result = await client.GetMessageDeliveryInsightAsync(new DateOnly(2019, 12, 31));

        Assert.IsTrue(result.TryGetValue(out var insight));
        Assert.AreEqual(LineMessageDeliveryInsight.StatusReady, insight.Status);
        Assert.AreEqual(5385L, insight.Broadcast);
        Assert.AreEqual(522L, insight.Targeting);
        Assert.AreEqual(1200L, insight.AutoResponse);
        Assert.AreEqual(1201L, insight.WelcomeResponse);
        Assert.AreEqual(1202L, insight.Chat);
        Assert.AreEqual(1203L, insight.ApiBroadcast);
        Assert.AreEqual(1204L, insight.ApiPush);
        Assert.AreEqual(1205L, insight.ApiMulticast);
        Assert.AreEqual(1206L, insight.ApiNarrowcast);
        Assert.AreEqual(1207L, insight.ApiReply);
        Assert.AreEqual(LineEndpoints.InsightMessageDelivery + "?date=20191231", handler.Last.Uri.AbsoluteUri, "日期是八位數字,不帶分隔符號。");
    }

    [TestMethod]
    public async Task GetMessageDeliveryInsightAsync_Unready_LeavesNumbersNull()
    {
        var client = CreateClient(FakeHttpMessageHandler.Json(Samples.Body("insight-message-delivery-unready")));

        var result = await client.GetMessageDeliveryInsightAsync(new DateOnly(2026, 9, 25));

        Assert.IsTrue(result.TryGetValue(out var insight));
        Assert.AreEqual(LineMessageDeliveryInsight.StatusUnready, insight.Status);
        Assert.IsNull(insight.ApiPush, "還沒統計是 null,不是 0。");
    }

    [TestMethod]
    public async Task GetFollowersInsightAsync_ParsesSample()
    {
        var handler = FakeHttpMessageHandler.Json(Samples.Body("insight-followers"));
        var client = CreateClient(handler);

        var result = await client.GetFollowersInsightAsync(new DateOnly(2019, 4, 18));

        Assert.IsTrue(result.TryGetValue(out var insight));
        Assert.AreEqual("ready", insight.Status);
        Assert.AreEqual(7620L, insight.Followers);
        Assert.AreEqual(5848L, insight.TargetedReaches);
        Assert.AreEqual(237L, insight.Blocks);
        Assert.AreEqual(LineEndpoints.InsightFollowers + "?date=20190418", handler.Last.Uri.AbsoluteUri);
    }

    [TestMethod]
    public async Task GetDemographicInsightAsync_ParsesSample()
    {
        var handler = FakeHttpMessageHandler.Json(Samples.Body("insight-demographic"));
        var client = CreateClient(handler);

        var result = await client.GetDemographicInsightAsync();

        Assert.IsTrue(result.TryGetValue(out var insight));
        Assert.IsTrue(insight.Available);
        Assert.AreEqual(3, insight.Genders.Count);
        Assert.AreEqual("male", insight.Genders[1].Gender);
        Assert.AreEqual(31.8, insight.Genders[1].Percentage);
        Assert.AreEqual("from50", insight.Ages[1].Age);
        Assert.AreEqual("徳島", insight.Areas[1].Area);
        Assert.AreEqual("ios", insight.AppTypes[0].AppType);
        Assert.AreEqual(62.4, insight.AppTypes[0].Percentage);
        Assert.AreEqual("over365days", insight.SubscriptionPeriods[0].SubscriptionPeriod);
        Assert.AreEqual(0.0, insight.SubscriptionPeriods[5].Percentage);
        Assert.AreEqual(LineEndpoints.InsightDemographic, handler.Last.Uri.AbsoluteUri);
    }

    [TestMethod]
    public async Task GetDemographicInsightAsync_Unavailable_HasEmptyLists()
    {
        var client = CreateClient(FakeHttpMessageHandler.Json(Samples.Body("insight-demographic-unavailable")));

        var result = await client.GetDemographicInsightAsync();

        Assert.IsTrue(result.TryGetValue(out var insight));
        Assert.IsFalse(insight.Available);
        Assert.AreEqual(0, insight.Genders.Count);
        Assert.AreEqual(0, insight.SubscriptionPeriods.Count);
    }

    [TestMethod]
    public async Task Insight_NotConfigured_FailsWithoutSending()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = new LineMessagingClient(
            new HttpPipelineClient(new HttpClient(handler)),
            Options.Create(new LineMessagingOptions()));

        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.GetMessageDeliveryInsightAsync(new DateOnly(2026, 1, 1))).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.GetFollowersInsightAsync(new DateOnly(2026, 1, 1))).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.GetDemographicInsightAsync()).Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    private static LineMessagingClient CreateClient(FakeHttpMessageHandler handler) =>
        new(
            new HttpPipelineClient(new HttpClient(handler)),
            Options.Create(new LineMessagingOptions { ChannelAccessToken = AccessToken }));
}
