using System.Text;
using System.Text.Json;

namespace Ozakboy.Line.Tests;

/// <summary>
/// webhook 驗簽與解析的測試。
/// Tests for webhook signature verification and parsing.
/// </summary>
[TestClass]
public sealed class LineWebhookTests
{
    private const string ChannelSecret = "test-webhook-channel-secret";

    [TestMethod]
    public void Verify_ComputedSignature_Passes()
    {
        const string Body = """{"destination":"Ubot","events":[]}""";
        var signature = LineWebhookSignature.Compute(ChannelSecret, Encoding.UTF8.GetBytes(Body));

        Assert.IsTrue(LineWebhookSignature.Verify(ChannelSecret, Body, signature));
    }

    [TestMethod]
    public void Verify_TamperedBody_Fails()
    {
        const string Body = """{"destination":"Ubot","events":[]}""";
        var signature = LineWebhookSignature.Compute(ChannelSecret, Encoding.UTF8.GetBytes(Body));

        Assert.IsFalse(
            LineWebhookSignature.Verify(ChannelSecret, """{"destination":"Uevil","events":[]}""", signature),
            "內容改了一個字,簽章就必須驗不過 —— 這正是驗簽存在的理由。");
    }

    [TestMethod]
    public void Verify_WrongSecret_Fails()
    {
        const string Body = """{"destination":"Ubot","events":[]}""";
        var signature = LineWebhookSignature.Compute("another-secret-value", Encoding.UTF8.GetBytes(Body));

        Assert.IsFalse(LineWebhookSignature.Verify(ChannelSecret, Body, signature));
    }

    [TestMethod]
    public void Verify_SignatureIsNotBase64_ReturnsFalseInsteadOfThrowing()
    {
        // 這個值由外部送進來,不能因為格式不對就讓端點擲例外 —— 那會變成一個免費的 500 製造機。
        Assert.IsFalse(LineWebhookSignature.Verify(ChannelSecret, "{}", "這不是 base64!!!"));
    }

    [TestMethod]
    public void Verify_SignatureIsWrongLength_ReturnsFalse()
    {
        Assert.IsFalse(LineWebhookSignature.Verify(ChannelSecret, "{}", Convert.ToBase64String([1, 2, 3])));
    }

    [TestMethod]
    public void Verify_MissingSignature_ReturnsFalse()
    {
        Assert.IsFalse(LineWebhookSignature.Verify(ChannelSecret, "{}", null));
        Assert.IsFalse(LineWebhookSignature.Verify(ChannelSecret, "{}", string.Empty));
    }

    [TestMethod]
    public void Verify_BlankSecret_ReturnsFalse()
    {
        // 密鑰忘了設定時,絕不能變成「什麼都驗得過」。
        Assert.IsFalse(LineWebhookSignature.Verify(string.Empty, "{}", Convert.ToBase64String(new byte[32])));
    }

    [TestMethod]
    public void Parse_TextMessageEvent_ReadsEveryField()
    {
        const string Body = """
        {
          "destination": "Ubot",
          "events": [{
            "type": "message",
            "mode": "active",
            "timestamp": 1799999999000,
            "webhookEventId": "01ABC",
            "deliveryContext": { "isRedelivery": true },
            "replyToken": "rt-1",
            "source": { "type": "user", "userId": "U1" },
            "message": { "id": "m-1", "type": "text", "text": "哈囉", "quoteToken": "qt-1" }
          }]
        }
        """;

        var result = LineWebhookParser.Parse(Body);

        Assert.IsTrue(result.TryGetValue(out var payload));
        Assert.AreEqual("Ubot", payload.Destination);
        Assert.AreEqual(1, payload.Events.Count);

        var webhookEvent = payload.Events[0];
        Assert.AreEqual(LineWebhookEventTypes.Message, webhookEvent.Type);
        Assert.AreEqual("active", webhookEvent.Mode);
        Assert.AreEqual("01ABC", webhookEvent.WebhookEventId);
        Assert.IsTrue(webhookEvent.IsRedelivery, "deliveryContext.isRedelivery 必須讀出來,否則副作用會被重做。");
        Assert.AreEqual("rt-1", webhookEvent.ReplyToken);
        Assert.AreEqual("user", webhookEvent.Source.Type);
        Assert.AreEqual("U1", webhookEvent.Source.UserId);
        Assert.AreEqual(LineWebhookMessageTypes.Text, webhookEvent.Message!.Type);
        Assert.AreEqual("哈囉", webhookEvent.Message.Text);
        Assert.AreEqual("qt-1", webhookEvent.Message.QuoteToken);
    }

    [TestMethod]
    public void Parse_Timestamp_IsReadAsMilliseconds()
    {
        // LINE 送的是毫秒。當成秒解會落在 1970 年附近,而畫面上只會表現為「時間怪怪的」。
        const string Body = """{"destination":"U","events":[{"type":"message","timestamp":1799999999000,"source":{"type":"user"},"message":{"id":"m","type":"text","text":"x"}}]}""";

        var result = LineWebhookParser.Parse(Body);

        Assert.IsTrue(result.TryGetValue(out var payload));
        Assert.AreEqual(DateTimeOffset.FromUnixTimeMilliseconds(1799999999000L), payload.Events[0].Timestamp);
    }

    [TestMethod]
    public void Parse_LocationMessage_ReadsCoordinates()
    {
        const string Body = """{"destination":"U","events":[{"type":"message","timestamp":1,"source":{"type":"user","userId":"U1"},"message":{"id":"m","type":"location","title":"台北 101","address":"台北市信義區","latitude":25.0339,"longitude":121.5645}}]}""";

        var result = LineWebhookParser.Parse(Body);

        Assert.IsTrue(result.TryGetValue(out var payload));
        var message = payload.Events[0].Message!;
        Assert.AreEqual(LineWebhookMessageTypes.Location, message.Type);
        Assert.AreEqual("台北 101", message.Title);
        Assert.AreEqual("台北市信義區", message.Address);
        Assert.AreEqual(25.0339, message.Latitude);
        Assert.AreEqual(121.5645, message.Longitude);
    }

    [TestMethod]
    public void Parse_StickerMessage_ReadsIds()
    {
        const string Body = """{"destination":"U","events":[{"type":"message","timestamp":1,"source":{"type":"user"},"message":{"id":"m","type":"sticker","packageId":"446","stickerId":"1988"}}]}""";

        var result = LineWebhookParser.Parse(Body);

        Assert.IsTrue(result.TryGetValue(out var payload));
        Assert.AreEqual("446", payload.Events[0].Message!.PackageId);
        Assert.AreEqual("1988", payload.Events[0].Message!.StickerId);
    }

    [TestMethod]
    public void Parse_FileMessage_ReadsNameAndSize()
    {
        const string Body = """{"destination":"U","events":[{"type":"message","timestamp":1,"source":{"type":"user"},"message":{"id":"m","type":"file","fileName":"報表.pdf","fileSize":123456,"contentProvider":{"type":"line"}}}]}""";

        var result = LineWebhookParser.Parse(Body);

        Assert.IsTrue(result.TryGetValue(out var payload));
        var message = payload.Events[0].Message!;
        Assert.AreEqual("報表.pdf", message.FileName);
        Assert.AreEqual(123456L, message.FileSize);
        Assert.AreEqual("line", message.ContentProvider!.Type);
        Assert.IsNull(message.ContentProvider.OriginalContentUrl);
    }

    [TestMethod]
    public void Parse_FollowEvent_ReadsIsUnblocked()
    {
        // 加好友與解除封鎖送的是同一種事件,只差這個旗標;不看它就會對解除封鎖的人重發一次新戶好禮。
        const string Body = """{"destination":"U","events":[{"type":"follow","timestamp":1,"replyToken":"rt","source":{"type":"user","userId":"U1"},"follow":{"isUnblocked":true}}]}""";

        var result = LineWebhookParser.Parse(Body);

        Assert.IsTrue(result.TryGetValue(out var payload));
        Assert.AreEqual(LineWebhookEventTypes.Follow, payload.Events[0].Type);
        Assert.IsTrue(payload.Events[0].Follow!.IsUnblocked);
    }

    [TestMethod]
    public void Parse_FollowEventWithoutFollowObject_TreatsItAsFirstTime()
    {
        const string Body = """{"destination":"U","events":[{"type":"follow","timestamp":1,"source":{"type":"user","userId":"U1"}}]}""";

        var result = LineWebhookParser.Parse(Body);

        Assert.IsTrue(result.TryGetValue(out var payload));
        Assert.IsNotNull(payload.Events[0].Follow);
        Assert.IsFalse(payload.Events[0].Follow!.IsUnblocked);
    }

    [TestMethod]
    public void Parse_UnfollowEvent_HasNoFollowObject()
    {
        const string Body = """{"destination":"U","events":[{"type":"unfollow","timestamp":1,"source":{"type":"user","userId":"U1"}}]}""";

        var result = LineWebhookParser.Parse(Body);

        Assert.IsTrue(result.TryGetValue(out var payload));
        Assert.AreEqual(LineWebhookEventTypes.Unfollow, payload.Events[0].Type);
        Assert.IsNull(payload.Events[0].Follow);
        Assert.IsNull(payload.Events[0].ReplyToken, "封鎖事件沒有回覆權杖 —— 對方已經封鎖了,回不了話。");
    }

    [TestMethod]
    public void Parse_PostbackEvent_ReadsDataAndParams()
    {
        const string Body = """{"destination":"U","events":[{"type":"postback","timestamp":1,"replyToken":"rt","source":{"type":"user","userId":"U1"},"postback":{"data":"action=book&id=7","params":{"datetime":"2026-09-20T10:00"}}}]}""";

        var result = LineWebhookParser.Parse(Body);

        Assert.IsTrue(result.TryGetValue(out var payload));
        var postback = payload.Events[0].Postback!;
        Assert.AreEqual("action=book&id=7", postback.Data);
        Assert.AreEqual("2026-09-20T10:00", postback.Params["datetime"]);
    }

    [TestMethod]
    public void Parse_PostbackWithoutParams_HasEmptyDictionary()
    {
        const string Body = """{"destination":"U","events":[{"type":"postback","timestamp":1,"source":{"type":"user"},"postback":{"data":"a=1"}}]}""";

        var result = LineWebhookParser.Parse(Body);

        Assert.IsTrue(result.TryGetValue(out var payload));
        Assert.AreEqual(0, payload.Events[0].Postback!.Params.Count, "沒有參數時是空字典,不是 null。");
    }

    [TestMethod]
    public void Parse_GroupSource_ReadsGroupId()
    {
        const string Body = """{"destination":"U","events":[{"type":"join","timestamp":1,"replyToken":"rt","source":{"type":"group","groupId":"G1"}}]}""";

        var result = LineWebhookParser.Parse(Body);

        Assert.IsTrue(result.TryGetValue(out var payload));
        Assert.AreEqual("group", payload.Events[0].Source.Type);
        Assert.AreEqual("G1", payload.Events[0].Source.GroupId);
        Assert.IsNull(payload.Events[0].Source.UserId);
    }

    [TestMethod]
    public void Parse_UnknownEventType_StillArrivesWithItsRawJson()
    {
        // LINE 隨時可能新增事件型別。解析不該擋下它 —— 沒建模的欄位留在 Raw 裡,呼叫端仍然拿得到。
        const string Body = """{"destination":"U","events":[{"type":"brandNewEventType","timestamp":1,"source":{"type":"user","userId":"U1"},"somethingNew":{"value":42}}]}""";

        var result = LineWebhookParser.Parse(Body);

        Assert.IsTrue(result.TryGetValue(out var payload));
        Assert.AreEqual(1, payload.Events.Count);
        Assert.AreEqual("brandNewEventType", payload.Events[0].Type);
        Assert.AreEqual(42, payload.Events[0].Raw.GetProperty("somethingNew").GetProperty("value").GetInt32());
    }

    [TestMethod]
    public void Parse_Raw_SurvivesAfterParseReturns()
    {
        // Raw 必須是 Clone 過的。沒有 Clone 的話,產生它的 JsonDocument 在 Parse 回傳前就被釋放,
        // 呼叫端拿到的會是一個一碰就擲例外的空殼。
        const string Body = """{"destination":"U","events":[{"type":"message","timestamp":1,"source":{"type":"user"},"message":{"id":"m","type":"text","text":"x"}}]}""";

        var result = LineWebhookParser.Parse(Body);

        Assert.IsTrue(result.TryGetValue(out var payload));
        Assert.AreEqual(JsonValueKind.Object, payload.Events[0].Raw.ValueKind);
        Assert.AreEqual("message", payload.Events[0].Raw.GetProperty("type").GetString());
    }

    [TestMethod]
    public void Parse_VerificationRequestWithNoEvents_Succeeds()
    {
        // LINE 後台驗證 webhook 位址時送的就是一個沒有事件的請求,必須照樣解析成功。
        var result = LineWebhookParser.Parse("""{"destination":"Ubot","events":[]}""");

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(0, result.GetValueOrThrow().Events.Count);
    }

    [TestMethod]
    public void Parse_InvalidJson_Fails()
    {
        var result = LineWebhookParser.Parse("{not json");

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.WebhookInvalidPayload, result.Error.Code);
    }

    [TestMethod]
    public void Parse_JsonArray_Fails()
    {
        var result = LineWebhookParser.Parse("[]");

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.WebhookInvalidPayload, result.Error.Code);
    }

    [TestMethod]
    public void Parse_Bytes_MatchesStringOverload()
    {
        const string Body = """{"destination":"Ubot","events":[{"type":"unfollow","timestamp":1,"source":{"type":"user","userId":"U1"}}]}""";

        var fromBytes = LineWebhookParser.Parse(Encoding.UTF8.GetBytes(Body));
        var fromString = LineWebhookParser.Parse(Body);

        Assert.IsTrue(fromBytes.TryGetValue(out var a));
        Assert.IsTrue(fromString.TryGetValue(out var b));
        Assert.AreEqual(b.Destination, a.Destination);
        Assert.AreEqual(b.Events[0].Type, a.Events[0].Type);
    }

    [TestMethod]
    public void Parse_VideoPlayCompleteEvent_ReadsTrackingId()
    {
        // trackingId 是送影片時自己填的值;能讀回來,「有沒有看完」才閉得了環。
        var result = LineWebhookParser.Parse(
            """{"destination":"Ubot","events":[{"type":"videoPlayComplete","mode":"active","timestamp":1700000000000,"webhookEventId":"e1","deliveryContext":{"isRedelivery":false},"replyToken":"rt","source":{"type":"user","userId":"U1"},"videoPlayComplete":{"trackingId":"track-1"}}]}""");

        Assert.IsTrue(result.TryGetValue(out var payload));
        var evt = payload.Events[0];
        Assert.AreEqual(LineWebhookEventTypes.VideoPlayComplete, evt.Type);
        Assert.IsNotNull(evt.VideoPlayComplete);
        Assert.AreEqual("track-1", evt.VideoPlayComplete.TrackingId);
    }

    [TestMethod]
    public void Parse_MessageEvent_HasNoVideoPlayComplete()
    {
        var result = LineWebhookParser.Parse(
            """{"destination":"Ubot","events":[{"type":"message","mode":"active","timestamp":1700000000000,"webhookEventId":"e1","source":{"type":"user","userId":"U1"},"message":{"id":"m1","type":"text","text":"嗨","quoteToken":"qt-1"}}]}""");

        Assert.IsTrue(result.TryGetValue(out var payload));
        Assert.IsNull(payload.Events[0].VideoPlayComplete);
        Assert.AreEqual("qt-1", payload.Events[0].Message!.QuoteToken);
    }
}
