using System.Text.Json;
using Microsoft.Extensions.Options;
using Ozakboy.Http;
using Ozakboy.Line.Messaging.Actions;
using Ozakboy.Line.Messaging.Messages;
using Ozakboy.Line.Messaging.Messages.Template;
using Ozakboy.Line.Tests.TestSupport;

namespace Ozakboy.Line.Tests;

/// <summary>
/// 範本訊息的測試:四種範本的欄位名,以及動作數與欄數的本地上限。
/// Tests for template messages: the field names of the four templates, and the local action and column limits.
/// </summary>
[TestClass]
public sealed class LineTemplateMessageTests
{
    [TestMethod]
    public void ButtonsTemplate_WritesEveryFieldWithLineNames()
    {
        var template = new ButtonsTemplate("要看哪一項?")
        {
            ThumbnailImageUrl = "https://example.com/t.jpg",
            ImageAspectRatio = ButtonsTemplate.AspectRatioSquare,
            ImageSize = ButtonsTemplate.ImageSizeContain,
            ImageBackgroundColor = "#FFFFFF",
            Title = "選單",
            DefaultAction = new UriAction("https://example.com") { Label = "官網" },
            Actions = { new MessageAction("裁罰") { Label = "裁罰" }, new PostbackAction("a=1") { Label = "評價" } },
        };

        var json = Serialize(new TemplateMessage("替代文字", template));

        Assert.AreEqual("template", json.GetProperty("type").GetString());
        Assert.AreEqual("替代文字", json.GetProperty("altText").GetString());

        var body = json.GetProperty("template");
        Assert.AreEqual("buttons", body.GetProperty("type").GetString());
        Assert.AreEqual("https://example.com/t.jpg", body.GetProperty("thumbnailImageUrl").GetString());
        Assert.AreEqual("square", body.GetProperty("imageAspectRatio").GetString());
        Assert.AreEqual("contain", body.GetProperty("imageSize").GetString());
        Assert.AreEqual("#FFFFFF", body.GetProperty("imageBackgroundColor").GetString());
        Assert.AreEqual("選單", body.GetProperty("title").GetString());
        Assert.AreEqual("要看哪一項?", body.GetProperty("text").GetString());
        Assert.AreEqual("uri", body.GetProperty("defaultAction").GetProperty("type").GetString());
        Assert.AreEqual(2, body.GetProperty("actions").GetArrayLength());
        Assert.AreEqual("postback", body.GetProperty("actions")[1].GetProperty("type").GetString());
    }

    [TestMethod]
    public void ButtonsTemplate_OptionalFieldsAreOmitted()
    {
        var json = Serialize(new TemplateMessage("alt", new ButtonsTemplate("內文") { Actions = { new MessageAction("好") } }));
        var body = json.GetProperty("template");

        Assert.IsFalse(body.TryGetProperty("thumbnailImageUrl", out _));
        Assert.IsFalse(body.TryGetProperty("title", out _));
        Assert.IsFalse(body.TryGetProperty("defaultAction", out _));
        Assert.IsFalse(body.TryGetProperty("imageAspectRatio", out _));
    }

    [TestMethod]
    public void ButtonsTemplate_FiveActions_FailsValidation()
    {
        var template = new ButtonsTemplate("內文");
        for (var index = 0; index <= LineMessagingLimits.MaxButtonsTemplateActions; index++)
        {
            template.Actions.Add(new MessageAction($"第 {index} 個"));
        }

        var result = new TemplateMessage("alt", template).Validate();

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.InvalidTemplate, result.Error.Code);
        StringAssert.Contains(result.Error.Message, "5");
    }

    [TestMethod]
    public void ButtonsTemplate_NoActions_FailsValidation()
    {
        var result = new TemplateMessage("alt", new ButtonsTemplate("內文")).Validate();

        Assert.AreEqual(LineErrorCodes.InvalidTemplate, result.Error!.Code);
    }

    [TestMethod]
    public void ConfirmTemplate_WritesTextAndExactlyTwoActions()
    {
        var template = new ConfirmTemplate("確定嗎?", new MessageAction("是") { Label = "是" }, new MessageAction("否") { Label = "否" });

        var json = Serialize(new TemplateMessage("alt", template));
        var body = json.GetProperty("template");

        Assert.AreEqual("confirm", body.GetProperty("type").GetString());
        Assert.AreEqual("確定嗎?", body.GetProperty("text").GetString());
        Assert.AreEqual(2, body.GetProperty("actions").GetArrayLength());
        Assert.AreEqual("否", body.GetProperty("actions")[1].GetProperty("text").GetString());
        Assert.IsTrue(template.Validate().IsSuccess);
    }

    [TestMethod]
    public void ConfirmTemplate_ThreeActions_FailsValidation()
    {
        // 確認範本是「恰好兩個」,多一個也退回。
        var template = new ConfirmTemplate("確定嗎?", new MessageAction("是"), new MessageAction("否"));
        template.Actions.Add(new MessageAction("再想想"));

        var result = template.Validate();

        Assert.AreEqual(LineErrorCodes.InvalidTemplate, result.Error!.Code);
    }

    [TestMethod]
    public void CarouselTemplate_WritesColumnsAndSharedImageSettings()
    {
        var template = new CarouselTemplate
        {
            ImageAspectRatio = ButtonsTemplate.AspectRatioRectangle,
            ImageSize = ButtonsTemplate.ImageSizeCover,
            Columns =
            {
                new CarouselColumn("第一欄")
                {
                    ThumbnailImageUrl = "https://example.com/1.jpg",
                    ImageBackgroundColor = "#000000",
                    Title = "一",
                    DefaultAction = new UriAction("https://example.com/1"),
                    Actions = { new MessageAction("選一") },
                },
                new CarouselColumn("第二欄") { Actions = { new MessageAction("選二") } },
            },
        };

        var json = Serialize(new TemplateMessage("alt", template));
        var body = json.GetProperty("template");

        Assert.AreEqual("carousel", body.GetProperty("type").GetString());
        Assert.AreEqual("rectangle", body.GetProperty("imageAspectRatio").GetString());
        Assert.AreEqual("cover", body.GetProperty("imageSize").GetString());

        var columns = body.GetProperty("columns");
        Assert.AreEqual(2, columns.GetArrayLength());
        Assert.AreEqual("https://example.com/1.jpg", columns[0].GetProperty("thumbnailImageUrl").GetString());
        Assert.AreEqual("#000000", columns[0].GetProperty("imageBackgroundColor").GetString());
        Assert.AreEqual("一", columns[0].GetProperty("title").GetString());
        Assert.AreEqual("uri", columns[0].GetProperty("defaultAction").GetProperty("type").GetString());
        Assert.AreEqual("第二欄", columns[1].GetProperty("text").GetString());
        Assert.IsFalse(columns[1].TryGetProperty("title", out _));
    }

    [TestMethod]
    public void CarouselTemplate_ElevenColumns_FailsValidation()
    {
        var template = new CarouselTemplate();
        for (var index = 0; index <= LineMessagingLimits.MaxCarouselColumns; index++)
        {
            template.Columns.Add(new CarouselColumn("欄") { Actions = { new MessageAction("好") } });
        }

        Assert.AreEqual(LineErrorCodes.InvalidTemplate, template.Validate().Error!.Code);
    }

    [TestMethod]
    public void CarouselTemplate_UnevenActionCounts_FailsValidation()
    {
        // 每欄動作數必須一致;LINE 的 400 不會說是哪一欄多了一個。
        var template = new CarouselTemplate
        {
            Columns =
            {
                new CarouselColumn("一") { Actions = { new MessageAction("a"), new MessageAction("b") } },
                new CarouselColumn("二") { Actions = { new MessageAction("a") } },
            },
        };

        var result = template.Validate();

        Assert.AreEqual(LineErrorCodes.InvalidTemplate, result.Error!.Code);
        StringAssert.Contains(result.Error.Message, "第 2 欄");
    }

    [TestMethod]
    public void CarouselTemplate_FourActionsInAColumn_FailsValidation()
    {
        var column = new CarouselColumn("一");
        for (var index = 0; index <= LineMessagingLimits.MaxCarouselColumnActions; index++)
        {
            column.Actions.Add(new MessageAction("a"));
        }

        var template = new CarouselTemplate { Columns = { column } };

        Assert.AreEqual(LineErrorCodes.InvalidTemplate, template.Validate().Error!.Code);
    }

    [TestMethod]
    public void ImageCarouselTemplate_WritesImageUrlAndActionPerColumn()
    {
        var template = new ImageCarouselTemplate
        {
            Columns =
            {
                new ImageCarouselColumn("https://example.com/1.jpg", new UriAction("https://example.com/1") { Label = "一" }),
                new ImageCarouselColumn("https://example.com/2.jpg", new PostbackAction("p=2") { Label = "二" }),
            },
        };

        var json = Serialize(new TemplateMessage("alt", template));
        var body = json.GetProperty("template");

        Assert.AreEqual("image_carousel", body.GetProperty("type").GetString());
        Assert.AreEqual(2, body.GetProperty("columns").GetArrayLength());
        Assert.AreEqual("https://example.com/2.jpg", body.GetProperty("columns")[1].GetProperty("imageUrl").GetString());
        Assert.AreEqual("postback", body.GetProperty("columns")[1].GetProperty("action").GetProperty("type").GetString());
        Assert.IsTrue(template.Validate().IsSuccess);
    }

    [TestMethod]
    public void ImageCarouselTemplate_NoColumns_FailsValidation()
    {
        Assert.AreEqual(LineErrorCodes.InvalidTemplate, new ImageCarouselTemplate().Validate().Error!.Code);
    }

    [TestMethod]
    public async Task PushAsync_InvalidTemplate_FailsWithoutSending()
    {
        // 本地上限的意義就在這裡:超量的範本一個請求都不送,不浪費額度。
        var handler = FakeHttpMessageHandler.Json("""{"sentMessages":[]}""");
        var client = new LineMessagingClient(
            new HttpPipelineClient(new HttpClient(handler)),
            Options.Create(new LineMessagingOptions { ChannelAccessToken = "test-channel-access-token" }));

        var result = await client.PushAsync("U1", [new TemplateMessage("alt", new ButtonsTemplate("內文"))]);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.InvalidTemplate, result.Error.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public void TemplateMessage_QuickReplyIsCheckedBeforeTheTemplate()
    {
        var message = new TemplateMessage("alt", new ButtonsTemplate("內文") { Actions = { new MessageAction("好") } })
        {
            QuickReply = new Messaging.LineQuickReply(),
        };

        Assert.AreEqual(LineErrorCodes.TooManyQuickReplyItems, message.Validate().Error!.Code);
    }

    private static JsonElement Serialize(LineMessage message) => LineMessageSerializer.ToJsonElements([message])[0];
}
