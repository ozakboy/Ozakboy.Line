using System.Text.Json;
using Microsoft.Extensions.Options;
using Ozakboy.Http;
using Ozakboy.Line.Messaging.Actions;
using Ozakboy.Line.Messaging.Messages;
using Ozakboy.Line.Messaging.Messages.Imagemap;
using Ozakboy.Line.Messaging.Messages.Template;
using Ozakboy.Line.Tests.TestSupport;

namespace Ozakboy.Line.Tests;

/// <summary>
/// 訊息與動作的本地驗證測試:違反規格就在送出前失敗,而且一個請求都不送。
/// Tests for the local validation of messages and actions: breaking the specification fails before sending, and
/// not one request goes out.
/// </summary>
[TestClass]
public sealed class LineMessageValidationTests
{
    [TestMethod]
    public void Text_AtTheLimit_Passes_OneOver_Fails()
    {
        Assert.IsTrue(new TextMessage(new string('a', 5000)).Validate().IsSuccess);

        var result = new TextMessage(new string('a', 5001)).Validate();

        AssertFailure(result, LineErrorCodes.InvalidMessage, "5001", "5000", "text");
    }

    [TestMethod]
    public void TextV2_OverTheLimit_Fails()
    {
        var result = new TextV2Message(new string('a', 5001)).Validate();

        AssertFailure(result, LineErrorCodes.InvalidMessage, "5001", "5000", "text");
    }

    [TestMethod]
    public void TemplateAltText_OverTheLimit_FailsWithTemplateCode()
    {
        var template = new ConfirmTemplate("?", new MessageAction("是"), new MessageAction("否"));

        Assert.IsTrue(new TemplateMessage(new string('a', 400), template).Validate().IsSuccess);

        var result = new TemplateMessage(new string('a', 401), template).Validate();

        AssertFailure(result, LineErrorCodes.InvalidTemplate, "401", "400", "altText");
    }

    [TestMethod]
    public void ImagemapAltText_OverTheLimit_FailsWithImagemapCode()
    {
        var message = ImagemapWith(new string('a', 401), new ImagemapUriAction("https://example.com", new LineImagemapArea(0, 0, 10, 10)));

        AssertFailure(message.Validate(), LineErrorCodes.InvalidImagemap, "401", "400", "altText");
    }

    [TestMethod]
    public void FlexAltText_OverTheLimit_Fails()
    {
        using var contents = JsonDocument.Parse("""{"type":"bubble"}""");

        var result = new FlexMessage(new string('a', 401), contents.RootElement).Validate();

        AssertFailure(result, LineErrorCodes.InvalidMessage, "401", "400", "altText");
    }

    [TestMethod]
    public void Location_TitleAndAddress_AreLimitedTo100()
    {
        Assert.IsTrue(new LocationMessage(new string('t', 100), new string('a', 100), 1, 2).Validate().IsSuccess);

        AssertFailure(
            new LocationMessage(new string('t', 101), "addr", 1, 2).Validate(),
            LineErrorCodes.InvalidMessage, "title", "101", "100");
        AssertFailure(
            new LocationMessage("title", new string('a', 101), 1, 2).Validate(),
            LineErrorCodes.InvalidMessage, "address", "101", "100");
    }

    [TestMethod]
    public void ImageAndVideo_Urls_MustBeHttpsAndShortEnough()
    {
        Assert.IsTrue(new ImageMessage("https://example.com/o.jpg", "https://example.com/p.jpg").Validate().IsSuccess);
        Assert.IsTrue(new ImageMessage("HTTPS://example.com/o.jpg", "https://example.com/p.jpg").Validate().IsSuccess, "協定不分大小寫。");

        AssertFailure(
            new ImageMessage("http://example.com/o.jpg", "https://example.com/p.jpg").Validate(),
            LineErrorCodes.InvalidMessage, "originalContentUrl", "https://", "http://example.com/o.jpg");
        AssertFailure(
            new ImageMessage("https://example.com/o.jpg", "ftp://example.com/p.jpg").Validate(),
            LineErrorCodes.InvalidMessage, "previewImageUrl", "https://");
        AssertFailure(
            new VideoMessage("http://example.com/o.mp4", "https://example.com/p.jpg").Validate(),
            LineErrorCodes.InvalidMessage, "originalContentUrl", "https://");
        AssertFailure(
            new VideoMessage("https://example.com/o.mp4", "//example.com/p.jpg").Validate(),
            LineErrorCodes.InvalidMessage, "previewImageUrl", "https://");

        var tooLong = "https://example.com/" + new string('a', 2000 - "https://example.com/".Length + 1);
        AssertFailure(
            new ImageMessage(tooLong, "https://example.com/p.jpg").Validate(),
            LineErrorCodes.InvalidMessage, "2001", "2000");

        var exactly = "https://example.com/" + new string('a', 2000 - "https://example.com/".Length);
        Assert.IsTrue(new ImageMessage(exactly, "https://example.com/p.jpg").Validate().IsSuccess);
    }

    [TestMethod]
    public void Audio_Url_MustBeHttps()
    {
        Assert.IsTrue(new AudioMessage("https://example.com/a.m4a", 1000).Validate().IsSuccess);

        AssertFailure(
            new AudioMessage("http://example.com/a.m4a", 1000).Validate(),
            LineErrorCodes.InvalidMessage, "originalContentUrl", "https://");
    }

    [TestMethod]
    public void MessageConstructors_StillRefuseBlankIdsAndNonPositiveDuration()
    {
        // 這幾條規則在建構子就擋下,所以 Validate 不必再重複:貼圖識別碼非空、語音長度大於 0。
        Assert.ThrowsExactly<ArgumentException>(() => new StickerMessage("", "1"));
        Assert.ThrowsExactly<ArgumentException>(() => new StickerMessage("1", " "));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new AudioMessage("https://example.com/a.m4a", 0));
    }

    [TestMethod]
    public void ActionLabel_InQuickReply_IsLimitedTo20()
    {
        Assert.IsTrue(QuickReplyOf(new MessageAction("x") { Label = new string('標', 20) }).Validate().IsSuccess);

        var result = QuickReplyOf(new MessageAction("x") { Label = new string('標', 21) }).Validate();

        AssertFailure(result, LineErrorCodes.InvalidAction, "label", "21", "20", "quickReply.items[0]");
    }

    [TestMethod]
    public void QuickReplyImageUrl_MustBeHttps()
    {
        var message = new TextMessage("x")
        {
            QuickReply = new LineQuickReply
            {
                Items = { new LineQuickReplyItem(new MessageAction("好")) { ImageUrl = "http://example.com/i.png" } },
            },
        };

        AssertFailure(message.Validate(), LineErrorCodes.InvalidMessage, "imageUrl", "https://");
    }

    [TestMethod]
    public void QuickReplyItemCountError_StillComesFirst()
    {
        var quickReply = new LineQuickReply();
        for (var index = 0; index < 14; index++)
        {
            quickReply.Items.Add(new LineQuickReplyItem(new MessageAction("x") { Label = new string('a', 99) }));
        }

        Assert.AreEqual(LineErrorCodes.TooManyQuickReplyItems, quickReply.Validate().Error!.Code);
    }

    [TestMethod]
    [DataRow("https://example.com")]
    [DataRow("http://example.com")]
    [DataRow("line://nv/location")]
    [DataRow("tel:0212345678")]
    [DataRow("mailto:someone@example.com")]
    [DataRow("HTTPS://EXAMPLE.COM")]
    public void UriAction_AcceptedSchemes_Pass(string uri)
    {
        Assert.IsTrue(QuickReplyOf(new UriAction(uri)).Validate().IsSuccess);
    }

    [TestMethod]
    [DataRow("ftp://example.com")]
    [DataRow("javascript:alert(1)")]
    [DataRow("example.com")]
    [DataRow("//example.com")]
    [DataRow("intent://scan/#Intent;end")]
    public void UriAction_OtherSchemes_Fail(string uri)
    {
        var result = QuickReplyOf(new UriAction(uri)).Validate();

        AssertFailure(result, LineErrorCodes.InvalidAction, "uri", uri);
    }

    [TestMethod]
    public void UriAction_LengthLimit_Is1000()
    {
        var atLimit = "https://example.com/" + new string('a', 1000 - "https://example.com/".Length);

        Assert.IsTrue(QuickReplyOf(new UriAction(atLimit)).Validate().IsSuccess);
        AssertFailure(QuickReplyOf(new UriAction(atLimit + "a")).Validate(), LineErrorCodes.InvalidAction, "1001", "1000");
    }

    [TestMethod]
    public void UriAction_AltUriDesktop_IsLimitedTo1000()
    {
        var action = new UriAction("https://example.com") { AltUriDesktop = "https://example.com/" + new string('a', 1000) };

        AssertFailure(QuickReplyOf(action).Validate(), LineErrorCodes.InvalidAction, "altUri.desktop", "1000");
    }

    [TestMethod]
    public void PostbackAction_Data_IsLimitedTo300()
    {
        Assert.IsTrue(QuickReplyOf(new PostbackAction(new string('d', 300))).Validate().IsSuccess);

        AssertFailure(
            QuickReplyOf(new PostbackAction(new string('d', 301))).Validate(),
            LineErrorCodes.InvalidAction, "data", "301", "300");
    }

    [TestMethod]
    public void MessageAction_Text_IsLimitedTo300()
    {
        Assert.IsTrue(QuickReplyOf(new MessageAction(new string('t', 300))).Validate().IsSuccess);

        AssertFailure(
            QuickReplyOf(new MessageAction(new string('t', 301))).Validate(),
            LineErrorCodes.InvalidAction, "text", "301", "300");
    }

    [TestMethod]
    public void DatetimePicker_ValidFormatsForEachMode_Pass()
    {
        Assert.IsTrue(QuickReplyOf(new DatetimePickerAction("d", DatetimePickerAction.ModeDate, "2026-10-27", "2026-12-31", "2026-01-01")).Validate().IsSuccess);
        Assert.IsTrue(QuickReplyOf(new DatetimePickerAction("d", DatetimePickerAction.ModeTime, "09:30", "23:59", "00:00")).Validate().IsSuccess);
        Assert.IsTrue(QuickReplyOf(new DatetimePickerAction("d", DatetimePickerAction.ModeDatetime, "2026-10-27T09:30", "2026-12-31T23:59", "2026-01-01T00:00")).Validate().IsSuccess);
        Assert.IsTrue(QuickReplyOf(new DatetimePickerAction("d", DatetimePickerAction.ModeDate)).Validate().IsSuccess, "三個選填欄位都可以不給。");
    }

    [TestMethod]
    [DataRow("date", "2026-10-27T09:30", "initial")]
    [DataRow("date", "2026/10/27", "initial")]
    [DataRow("date", "2026-13-01", "initial")]
    [DataRow("time", "9:30", "initial")]
    [DataRow("time", "09:30:15", "initial")]
    [DataRow("time", "24:00", "initial")]
    [DataRow("datetime", "2026-10-27 09:30", "initial")]
    [DataRow("datetime", "2026-10-27", "initial")]
    public void DatetimePicker_FormatNotMatchingTheMode_Fails(string mode, string value, string field)
    {
        var result = QuickReplyOf(new DatetimePickerAction("d", mode, initial: value)).Validate();

        AssertFailure(result, LineErrorCodes.InvalidAction, field, mode, value);
    }

    [TestMethod]
    public void DatetimePicker_MinAndMaxFormats_AreCheckedToo()
    {
        AssertFailure(
            QuickReplyOf(new DatetimePickerAction("d", "date", min: "10/01/2026")).Validate(),
            LineErrorCodes.InvalidAction, "min");
        AssertFailure(
            QuickReplyOf(new DatetimePickerAction("d", "date", max: "tomorrow")).Validate(),
            LineErrorCodes.InvalidAction, "max");
    }

    [TestMethod]
    public void DatetimePicker_MaxEarlierThanMin_Fails_EqualPasses()
    {
        AssertFailure(
            QuickReplyOf(new DatetimePickerAction("d", "date", max: "2026-01-01", min: "2026-06-01")).Validate(),
            LineErrorCodes.InvalidAction, "max", "min", "2026-01-01", "2026-06-01");
        Assert.IsTrue(QuickReplyOf(new DatetimePickerAction("d", "time", max: "10:00", min: "10:00")).Validate().IsSuccess);
    }

    [TestMethod]
    public void DatetimePicker_DataOver300_Fails()
    {
        AssertFailure(
            QuickReplyOf(new DatetimePickerAction(new string('d', 301), "date")).Validate(),
            LineErrorCodes.InvalidAction, "data", "301");
    }

    [TestMethod]
    public void DatetimePicker_UnknownMode_StillThrowsAtConstruction()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new DatetimePickerAction("d", "dateTime"));
    }

    [TestMethod]
    public void ButtonsTemplate_ActionLabelOver20_Fails()
    {
        var template = new ButtonsTemplate("內文") { Actions = { new MessageAction("x") { Label = new string('a', 21) } } };

        var result = new TemplateMessage("alt", template).Validate();

        AssertFailure(result, LineErrorCodes.InvalidAction, "template.actions[0]", "21", "20");
    }

    [TestMethod]
    public void ButtonsTemplate_DefaultActionIsChecked()
    {
        var template = new ButtonsTemplate("內文")
        {
            DefaultAction = new UriAction("ftp://example.com"),
            Actions = { new MessageAction("x") },
        };

        AssertFailure(new TemplateMessage("alt", template).Validate(), LineErrorCodes.InvalidAction, "defaultAction", "uri");
    }

    [TestMethod]
    public void ConfirmTemplate_ActionLabelOver20_Fails()
    {
        var template = new ConfirmTemplate("?", new MessageAction("是"), new MessageAction("否") { Label = new string('a', 21) });

        AssertFailure(new TemplateMessage("alt", template).Validate(), LineErrorCodes.InvalidAction, "template.actions[1]", "21");
    }

    [TestMethod]
    public void CarouselTemplate_ColumnActionLabelAndDefaultAction_AreChecked()
    {
        var badLabel = new CarouselTemplate
        {
            Columns = { new CarouselColumn("a") { Actions = { new MessageAction("x") { Label = new string('a', 21) } } } },
        };
        AssertFailure(new TemplateMessage("alt", badLabel).Validate(), LineErrorCodes.InvalidAction, "template.columns[0].actions[0]", "21");

        var badDefault = new CarouselTemplate
        {
            Columns = { new CarouselColumn("a") { DefaultAction = new UriAction("nope"), Actions = { new MessageAction("x") } } },
        };
        AssertFailure(new TemplateMessage("alt", badDefault).Validate(), LineErrorCodes.InvalidAction, "template.columns[0].defaultAction");
    }

    [TestMethod]
    public void ImageCarousel_LabelLimitIs12_NotTheUsual20()
    {
        var ok = new ImageCarouselTemplate
        {
            Columns = { new ImageCarouselColumn("https://example.com/a.jpg", new MessageAction("x") { Label = new string('a', 12) }) },
        };
        Assert.IsTrue(new TemplateMessage("alt", ok).Validate().IsSuccess);

        var tooLong = new ImageCarouselTemplate
        {
            Columns = { new ImageCarouselColumn("https://example.com/a.jpg", new MessageAction("x") { Label = new string('a', 13) }) },
        };
        AssertFailure(new TemplateMessage("alt", tooLong).Validate(), LineErrorCodes.InvalidAction, "template.columns[0].action", "13", "12");
    }

    [TestMethod]
    public void ImagemapAction_LabelLimitIs50()
    {
        var ok = ImagemapWith("alt", new ImagemapUriAction("https://example.com", new LineImagemapArea(0, 0, 1, 1)) { Label = new string('a', 50) });
        Assert.IsTrue(ok.Validate().IsSuccess);

        var tooLong = ImagemapWith("alt", new ImagemapMessageAction("hi", new LineImagemapArea(0, 0, 1, 1)) { Label = new string('a', 51) });
        AssertFailure(tooLong.Validate(), LineErrorCodes.InvalidAction, "actions[0]", "51", "50");
    }

    [TestMethod]
    public void ImagemapAction_LinkUriSchemeAndMessageTextLength_AreChecked()
    {
        var badUri = ImagemapWith("alt", new ImagemapUriAction("ftp://example.com", new LineImagemapArea(0, 0, 1, 1)));
        AssertFailure(badUri.Validate(), LineErrorCodes.InvalidAction, "linkUri");

        var longText = ImagemapWith("alt", new ImagemapMessageAction(new string('a', 401), new LineImagemapArea(0, 0, 1, 1)));
        AssertFailure(longText.Validate(), LineErrorCodes.InvalidAction, "text", "401", "400");
    }

    [TestMethod]
    [DataRow("#FFFFFF")]
    [DataRow("#a1B2c3")]
    [DataRow("#000000")]
    public void ImageBackgroundColor_WellFormed_Passes(string color)
    {
        var template = new ButtonsTemplate("內文") { ImageBackgroundColor = color, Actions = { new MessageAction("x") } };

        Assert.IsTrue(new TemplateMessage("alt", template).Validate().IsSuccess);
    }

    [TestMethod]
    [DataRow("#FFF")]
    [DataRow("FFFFFF")]
    [DataRow("red")]
    [DataRow("#GGGGGG")]
    [DataRow("#FFFFFFFF")]
    public void ImageBackgroundColor_Malformed_FailsWithTemplateCode(string color)
    {
        var buttons = new ButtonsTemplate("內文") { ImageBackgroundColor = color, Actions = { new MessageAction("x") } };
        AssertFailure(new TemplateMessage("alt", buttons).Validate(), LineErrorCodes.InvalidTemplate, "imageBackgroundColor", color);

        var carousel = new CarouselTemplate
        {
            Columns = { new CarouselColumn("a") { ImageBackgroundColor = color, Actions = { new MessageAction("x") } } },
        };
        AssertFailure(new TemplateMessage("alt", carousel).Validate(), LineErrorCodes.InvalidTemplate, "imageBackgroundColor", color);
    }

    [TestMethod]
    public void RawAction_IsNeverValidated()
    {
        using var contents = JsonDocument.Parse(
            "{\"type\":\"uri\",\"label\":\"" + new string('a', 100) + "\",\"uri\":\"ftp://example.com\"}");

        Assert.IsTrue(QuickReplyOf(new RawAction(contents.RootElement)).Validate().IsSuccess, "RawAction 是逃生口,內容由呼叫端負責。");
    }

    [TestMethod]
    public void RawMessage_IsNeverValidated()
    {
        using var contents = JsonDocument.Parse("{\"type\":\"text\",\"text\":\"" + new string('a', 6000) + "\"}");

        Assert.IsTrue(new RawMessage(contents.RootElement).Validate().IsSuccess);
    }

    [TestMethod]
    public void ActionsWithoutExtraRules_AlwaysPass()
    {
        Assert.IsTrue(QuickReplyOf(new CameraAction()).Validate().IsSuccess);
        Assert.IsTrue(QuickReplyOf(new CameraRollAction()).Validate().IsSuccess);
        Assert.IsTrue(QuickReplyOf(new LocationAction()).Validate().IsSuccess);
        Assert.IsTrue(QuickReplyOf(new ClipboardAction("文字")).Validate().IsSuccess);
        Assert.IsTrue(QuickReplyOf(new RichMenuSwitchAction("alias", "data")).Validate().IsSuccess);
    }

    [TestMethod]
    public async Task PushAsync_InvalidMessage_FailsWithoutSendingAnything()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = new LineMessagingClient(
            new HttpPipelineClient(new HttpClient(handler)),
            Options.Create(new LineMessagingOptions { ChannelAccessToken = "test-channel-access-token" }));

        var result = await client.PushAsync("U1", [new TextMessage("ok"), new ImageMessage("http://example.com/o.jpg", "https://example.com/p.jpg")]);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.InvalidMessage, result.Error.Code);
        Assert.AreEqual(0, handler.Requests.Count, "違反規格的訊息必須在本地擋下,不能送出任何請求。");
    }

    [TestMethod]
    public async Task ReplyAsync_InvalidActionInsideTemplate_FailsWithoutSendingAnything()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = new LineMessagingClient(
            new HttpPipelineClient(new HttpClient(handler)),
            Options.Create(new LineMessagingOptions { ChannelAccessToken = "test-channel-access-token" }));
        var template = new ButtonsTemplate("內文") { Actions = { new PostbackAction(new string('d', 301)) } };

        var result = await client.ReplyAsync("reply-token", [new TemplateMessage("alt", template)]);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.InvalidAction, result.Error.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    /// <summary>
    /// 驗證結果必須是指定代碼的失敗,而且訊息含全部給定的片段。
    /// Asserts that a result is a failure with the given code whose message contains every given fragment.
    /// </summary>
    /// <param name="result">結果。The result.</param>
    /// <param name="code">預期的錯誤代碼。The expected error code.</param>
    /// <param name="fragments">訊息應含的片段。Fragments the message must contain.</param>
    private static void AssertFailure(Result result, string code, params string[] fragments)
    {
        Assert.IsTrue(result.IsFailure, "預期驗證失敗,但通過了。");
        Assert.AreEqual(code, result.Error!.Code);
        foreach (var fragment in fragments)
        {
            StringAssert.Contains(result.Error.Message, fragment, "錯誤訊息必須說出哪個欄位、實際值與上限。");
        }
    }

    /// <summary>
    /// 建立一則只掛一顆快速回覆按鈕的文字訊息,供驗證動作用。
    /// Builds a text message with a single quick reply button, for validating an action.
    /// </summary>
    /// <param name="action">動作。The action.</param>
    /// <returns>訊息。The message.</returns>
    private static QuickReplyHolder QuickReplyOf(LineAction action) =>
        new(new TextMessage("x") { QuickReply = new LineQuickReply { Items = { new LineQuickReplyItem(action) } } });

    /// <summary>
    /// 建立一則圖片地圖訊息。
    /// Builds an imagemap message.
    /// </summary>
    /// <param name="altText">替代文字。The alternative text.</param>
    /// <param name="action">動作。The action.</param>
    /// <returns>訊息。The message.</returns>
    private static ImagemapMessage ImagemapWith(string altText, LineImagemapAction action) =>
        new("https://example.com/map", altText, 700) { Actions = { action } };

    /// <summary>
    /// 包住掛了快速回覆的訊息,讓 <see cref="Validate"/> 同時走完訊息與快速回覆兩層檢查。
    /// Wraps a message carrying a quick reply so <see cref="Validate"/> runs both the message and the quick reply checks.
    /// </summary>
    /// <param name="Message">訊息。The message.</param>
    private sealed record QuickReplyHolder(TextMessage Message)
    {
        /// <summary>
        /// 驗證訊息。
        /// Validates the message.
        /// </summary>
        /// <returns>驗證結果。The validation result.</returns>
        public Result Validate() => Message.Validate();
    }
}
