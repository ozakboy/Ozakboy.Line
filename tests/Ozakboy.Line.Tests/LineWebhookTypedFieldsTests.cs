using Ozakboy.Line.Tests.TestSupport;

namespace Ozakboy.Line.Tests;

/// <summary>
/// webhook 事件與訊息欄位強型別補齊的測試:每種新事件、每個新訊息欄位都以樣本檔驗證。
/// Tests for the typed webhook events and message fields: every new event and every new message field is
/// verified against a sample file.
/// </summary>
/// <remarks>
/// 樣本在 <c>Samples/webhook-*.json</c>,出處與擷取日期寫在各檔的 <c>source</c> 與 <c>retrieved</c> 欄位。
/// The samples are <c>Samples/webhook-*.json</c>, with their origin and date in each file's <c>source</c> and
/// <c>retrieved</c> fields.
/// </remarks>
[TestClass]
public sealed class LineWebhookTypedFieldsTests
{
    private static readonly string[] StickerKeywords = ["Anger", "Angry", "Frustrated", "Hot-tempered"];

    private static readonly string[] OnlyK = ["k"];

    [TestMethod]
    public void Parse_MemberJoined_ReadsEveryMember()
    {
        var webhookEvent = ParseSingle("webhook-member-joined");

        Assert.AreEqual(LineWebhookEventTypes.MemberJoined, webhookEvent.Type);
        Assert.IsNotNull(webhookEvent.Joined);
        Assert.IsNull(webhookEvent.Left, "加入事件不該帶離開清單。");
        Assert.AreEqual(2, webhookEvent.Joined.Members.Count);
        Assert.AreEqual("user", webhookEvent.Joined.Members[0].Type);
        Assert.AreEqual("Utest-joined-1", webhookEvent.Joined.Members[0].UserId);
        Assert.AreEqual("Utest-joined-2", webhookEvent.Joined.Members[1].UserId);
    }

    [TestMethod]
    public void Parse_MemberLeft_ReadsEveryMember()
    {
        var webhookEvent = ParseSingle("webhook-member-left");

        Assert.AreEqual(LineWebhookEventTypes.MemberLeft, webhookEvent.Type);
        Assert.IsNotNull(webhookEvent.Left);
        Assert.IsNull(webhookEvent.Joined);
        Assert.AreEqual(1, webhookEvent.Left.Members.Count);
        Assert.AreEqual("user", webhookEvent.Left.Members[0].Type);
        Assert.AreEqual("Utest-left-1", webhookEvent.Left.Members[0].UserId);
    }

    [TestMethod]
    public void Parse_JoinAndLeave_CarryNothingExtra()
    {
        var result = LineWebhookParser.Parse(Samples.Body("webhook-join-leave"));

        Assert.IsTrue(result.TryGetValue(out var payload));
        Assert.AreEqual(2, payload.Events.Count);
        Assert.AreEqual(LineWebhookEventTypes.Join, payload.Events[0].Type);
        Assert.AreEqual("group", payload.Events[0].Source.Type);
        Assert.AreEqual("Ctest-group-id", payload.Events[0].Source.GroupId);
        Assert.AreEqual(LineWebhookEventTypes.Leave, payload.Events[1].Type);
        Assert.IsNull(payload.Events[1].ReplyToken, "離開群組之後已經沒有話可回。");
        Assert.IsNull(payload.Events[0].Joined);
        Assert.IsNull(payload.Events[1].Left);
    }

    [TestMethod]
    public void Parse_Beacon_ReadsHardwareIdTypeAndDeviceMessage()
    {
        var webhookEvent = ParseSingle("webhook-beacon");

        Assert.AreEqual(LineWebhookEventTypes.Beacon, webhookEvent.Type);
        Assert.IsNotNull(webhookEvent.Beacon);
        Assert.AreEqual("d41d8cd98f", webhookEvent.Beacon.Hwid);
        Assert.AreEqual(LineWebhookBeaconTypes.Enter, webhookEvent.Beacon.Type);
        Assert.AreEqual("1234567890abcdef", webhookEvent.Beacon.Dm);
    }

    [TestMethod]
    public void Parse_BeaconWithoutDeviceMessage_LeavesDmNull()
    {
        const string Body = """{"destination":"U","events":[{"type":"beacon","timestamp":1,"source":{"type":"user"},"beacon":{"hwid":"abc","type":"stay"}}]}""";

        var result = LineWebhookParser.Parse(Body);

        Assert.IsTrue(result.TryGetValue(out var payload));
        Assert.AreEqual(LineWebhookBeaconTypes.Stay, payload.Events[0].Beacon!.Type);
        Assert.IsNull(payload.Events[0].Beacon!.Dm);
    }

    [TestMethod]
    public void Parse_Unsend_ReadsMessageId()
    {
        var webhookEvent = ParseSingle("webhook-unsend");

        Assert.AreEqual(LineWebhookEventTypes.Unsend, webhookEvent.Type);
        Assert.IsNotNull(webhookEvent.Unsend);
        Assert.AreEqual("325708", webhookEvent.Unsend.MessageId);
        Assert.IsNull(webhookEvent.Message, "收回事件只有被收回的訊息識別碼,沒有訊息內容。");
    }

    [TestMethod]
    public void Parse_AccountLink_ReadsResultAndNonce()
    {
        var webhookEvent = ParseSingle("webhook-account-link");

        Assert.AreEqual(LineWebhookEventTypes.AccountLink, webhookEvent.Type);
        Assert.IsNotNull(webhookEvent.Link);
        Assert.AreEqual(LineWebhookLinkResults.Ok, webhookEvent.Link.Result);
        Assert.AreEqual("test-nonce-0123456789", webhookEvent.Link.Nonce);
    }

    [TestMethod]
    public void Parse_AccountLinkFailed_HasNoNonceRequirement()
    {
        const string Body = """{"destination":"U","events":[{"type":"accountLink","timestamp":1,"source":{"type":"user"},"link":{"result":"failed"}}]}""";

        var result = LineWebhookParser.Parse(Body);

        Assert.IsTrue(result.TryGetValue(out var payload));
        Assert.AreEqual(LineWebhookLinkResults.Failed, payload.Events[0].Link!.Result);
        Assert.IsNull(payload.Events[0].Link!.Nonce);
    }

    [TestMethod]
    public void Parse_Membership_ReadsTypeAndPlanId()
    {
        var webhookEvent = ParseSingle("webhook-membership");

        Assert.AreEqual(LineWebhookEventTypes.Membership, webhookEvent.Type);
        Assert.IsNotNull(webhookEvent.Membership);
        Assert.AreEqual(LineWebhookMembershipTypes.Joined, webhookEvent.Membership.Type);
        Assert.AreEqual(3189L, webhookEvent.Membership.MembershipId);
    }

    [TestMethod]
    public void Parse_MembershipIdSentAsString_IsStillRead()
    {
        const string Body = """{"destination":"U","events":[{"type":"membership","timestamp":1,"source":{"type":"user"},"membership":{"type":"renewed","membershipId":"77"}}]}""";

        var result = LineWebhookParser.Parse(Body);

        Assert.IsTrue(result.TryGetValue(out var payload));
        Assert.AreEqual(LineWebhookMembershipTypes.Renewed, payload.Events[0].Membership!.Type);
        Assert.AreEqual(77L, payload.Events[0].Membership!.MembershipId);
    }

    [TestMethod]
    public void Parse_TextWithEmojis_ReadsEveryEmoji()
    {
        var message = ParseSingle("webhook-text-emoji").Message!;

        Assert.IsNotNull(message.Emojis);
        Assert.AreEqual(2, message.Emojis.Count);
        Assert.AreEqual(0, message.Emojis[0].Index);
        Assert.AreEqual(1, message.Emojis[0].Length);
        Assert.AreEqual("5ac1bfd5040ab15980c9b435", message.Emojis[0].ProductId);
        Assert.AreEqual("001", message.Emojis[0].EmojiId);
        Assert.AreEqual(13, message.Emojis[1].Index);
        Assert.AreEqual("002", message.Emojis[1].EmojiId);
    }

    [TestMethod]
    public void Parse_TextWithMention_ReadsMentioneesIncludingIsSelf()
    {
        var message = ParseSingle("webhook-text-mention").Message!;

        Assert.IsNotNull(message.Mention);
        Assert.AreEqual(3, message.Mention.Mentionees.Count);

        var everyone = message.Mention.Mentionees[0];
        Assert.AreEqual("all", everyone.Type);
        Assert.AreEqual(0, everyone.Index);
        Assert.AreEqual(4, everyone.Length);
        Assert.IsNull(everyone.UserId, "@All 沒有指定使用者。");
        Assert.IsNull(everyone.IsSelf);

        var bot = message.Mention.Mentionees[1];
        Assert.AreEqual("user", bot.Type);
        Assert.AreEqual("Utest-bot-id", bot.UserId);
        Assert.AreEqual(true, bot.IsSelf);

        Assert.AreEqual(false, message.Mention.Mentionees[2].IsSelf);
    }

    [TestMethod]
    public void Parse_ImageInASet_ReadsImageSet()
    {
        var message = ParseSingle("webhook-image-set").Message!;

        Assert.IsNotNull(message.ImageSet);
        Assert.AreEqual("E005D41A7288F41B65593ED38FF6E9834B046AB36A37921A56BC236F13A91855", message.ImageSet.Id);
        Assert.AreEqual(2, message.ImageSet.Index);
        Assert.AreEqual(3, message.ImageSet.Total);
    }

    [TestMethod]
    public void Parse_QuotingReply_ReadsQuotedMessageIdAndMarkAsReadToken()
    {
        var message = ParseSingle("webhook-text-quoted").Message!;

        Assert.AreEqual("468789532432007169", message.QuotedMessageId);
        Assert.AreEqual("test-mark-as-read-token", message.MarkAsReadToken);
        Assert.AreEqual("test-quote-token", message.QuoteToken);
    }

    [TestMethod]
    public void Parse_Sticker_ReadsResourceTypeAndKeywords()
    {
        var message = ParseSingle("webhook-sticker-resource").Message!;

        Assert.AreEqual("ANIMATION", message.StickerResourceType);
        Assert.IsNotNull(message.Keywords);
        CollectionAssert.AreEqual(StickerKeywords, message.Keywords.ToArray());
        Assert.AreEqual("446", message.PackageId);
    }

    [TestMethod]
    public void Parse_PlainTextMessage_LeavesEveryNewFieldNull()
    {
        const string Body = """{"destination":"U","events":[{"type":"message","timestamp":1,"source":{"type":"user"},"message":{"id":"m","type":"text","text":"x"}}]}""";

        var result = LineWebhookParser.Parse(Body);

        Assert.IsTrue(result.TryGetValue(out var payload));
        var message = payload.Events[0].Message!;
        Assert.IsNull(message.Emojis);
        Assert.IsNull(message.Mention);
        Assert.IsNull(message.ImageSet);
        Assert.IsNull(message.QuotedMessageId);
        Assert.IsNull(message.MarkAsReadToken);
        Assert.IsNull(message.StickerResourceType);
        Assert.IsNull(message.Keywords);

        var webhookEvent = payload.Events[0];
        Assert.IsNull(webhookEvent.Joined);
        Assert.IsNull(webhookEvent.Left);
        Assert.IsNull(webhookEvent.Beacon);
        Assert.IsNull(webhookEvent.Unsend);
        Assert.IsNull(webhookEvent.Link);
        Assert.IsNull(webhookEvent.Membership);
    }

    [TestMethod]
    public void Parse_NewObjectsMissingTheirFields_DoNotThrow()
    {
        // 物件在、欄位全缺:每個強型別都該給預設值而不是擲例外 —— 一個壞事件不該讓整批 webhook 回 400。
        const string Body = """
        {"destination":"U","events":[
          {"type":"memberJoined","joined":{}},
          {"type":"memberLeft","left":{"members":[{}, 5, {"userId":"U9"}]}},
          {"type":"beacon","beacon":{}},
          {"type":"unsend","unsend":{}},
          {"type":"accountLink","link":{}},
          {"type":"membership","membership":{}},
          {"type":"message","message":{"type":"text","emojis":[{}],"mention":{},"imageSet":{},"keywords":[1,"k",null]}}
        ]}
        """;

        var result = LineWebhookParser.Parse(Body);

        Assert.IsTrue(result.TryGetValue(out var payload));
        Assert.AreEqual(7, payload.Events.Count);
        Assert.AreEqual(0, payload.Events[0].Joined!.Members.Count);

        var left = payload.Events[1].Left!.Members;
        Assert.AreEqual(2, left.Count, "不是物件的成員項目略過,其餘照讀。");
        Assert.AreEqual(string.Empty, left[0].Type);
        Assert.IsNull(left[0].UserId);
        Assert.AreEqual("U9", left[1].UserId);

        Assert.AreEqual(string.Empty, payload.Events[2].Beacon!.Hwid);
        Assert.IsNull(payload.Events[2].Beacon!.Dm);
        Assert.AreEqual(string.Empty, payload.Events[3].Unsend!.MessageId);
        Assert.AreEqual(string.Empty, payload.Events[4].Link!.Result);
        Assert.IsNull(payload.Events[5].Membership!.MembershipId);

        var message = payload.Events[6].Message!;
        Assert.AreEqual(1, message.Emojis!.Count);
        Assert.AreEqual(string.Empty, message.Emojis[0].ProductId);
        Assert.AreEqual(0, message.Mention!.Mentionees.Count);
        Assert.AreEqual(0, message.ImageSet!.Total);
        CollectionAssert.AreEqual(OnlyK, message.Keywords!.ToArray());
    }

    [TestMethod]
    public void Parse_NewFieldsWithWrongKinds_DoNotThrow()
    {
        const string Body = """{"destination":"U","events":[{"type":"message","timestamp":1,"source":{"type":"user"},"message":{"id":"m","type":"text","text":"x","emojis":"nope","mention":[],"imageSet":3,"keywords":"k","quotedMessageId":7}}]}""";

        var result = LineWebhookParser.Parse(Body);

        Assert.IsTrue(result.TryGetValue(out var payload));
        var message = payload.Events[0].Message!;
        Assert.IsNull(message.Emojis);
        Assert.IsNull(message.Mention);
        Assert.IsNull(message.ImageSet);
        Assert.IsNull(message.Keywords);
        Assert.IsNull(message.QuotedMessageId);
    }

    [TestMethod]
    public void Parse_EmptyEventsArray_StillSucceeds()
    {
        var result = LineWebhookParser.Parse("""{"events":[]}""");

        Assert.IsTrue(result.TryGetValue(out var payload));
        Assert.AreEqual(0, payload.Events.Count);
    }

    [TestMethod]
    public void Parse_UnknownEventType_KeepsOnlyRaw()
    {
        const string Body = """{"destination":"U","events":[{"type":"somethingNew","timestamp":1,"source":{"type":"user"},"somethingNew":{"x":1}}]}""";

        var result = LineWebhookParser.Parse(Body);

        Assert.IsTrue(result.TryGetValue(out var payload));
        var webhookEvent = payload.Events[0];
        Assert.AreEqual("somethingNew", webhookEvent.Type);
        Assert.IsNull(webhookEvent.Joined);
        Assert.IsNull(webhookEvent.Beacon);
        Assert.IsNull(webhookEvent.Membership);
        Assert.AreEqual(1, webhookEvent.Raw.GetProperty("somethingNew").GetProperty("x").GetInt32(), "沒建模的東西仍在 Raw 裡。");
    }

    [TestMethod]
    public void Parse_TypedEvents_StillKeepRaw()
    {
        var webhookEvent = ParseSingle("webhook-beacon");

        Assert.AreEqual("d41d8cd98f", webhookEvent.Raw.GetProperty("beacon").GetProperty("hwid").GetString());
    }

    /// <summary>
    /// 解析樣本並取出唯一的事件。
    /// Parses a sample and returns its only event.
    /// </summary>
    /// <param name="sample">樣本名稱。The sample name.</param>
    /// <returns>事件。The event.</returns>
    private static LineWebhookEvent ParseSingle(string sample)
    {
        var result = LineWebhookParser.Parse(Samples.Body(sample));

        Assert.IsTrue(result.TryGetValue(out var payload), "樣本必須能解析。");
        Assert.AreEqual(1, payload.Events.Count);
        return payload.Events[0];
    }
}
