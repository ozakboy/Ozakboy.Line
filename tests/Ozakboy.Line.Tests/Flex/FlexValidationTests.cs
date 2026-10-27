using Microsoft.Extensions.Options;
using Ozakboy.Core.Abstractions;
using Ozakboy.Http;
using Ozakboy.Line.Messaging;
using Ozakboy.Line.Messaging.Actions;
using Ozakboy.Line.Messaging.Messages;
using Ozakboy.Line.Messaging.Messages.Flex;
using Ozakboy.Line.Tests.TestSupport;

namespace Ozakboy.Line.Tests.Flex;

/// <summary>
/// Flex 版面的本地驗證:每條規則正反各一,錯誤訊息帶版面路徑,Raw 不驗,用戶端送出前會擋下。
/// Local validation of Flex layouts: each rule has a passing and a failing case, errors carry the layout path, Raw is not
/// checked, and the client stops an invalid message before sending.
/// </summary>
[TestClass]
public sealed class FlexValidationTests
{
    private const string Image = "https://example.com/a.png";

    [TestMethod]
    public void ValidTypedMessage_Passes()
    {
        var message = Message(new FlexBubble
        {
            Size = FlexBubbleSize.Mega,
            Hero = new FlexImage(Image) { Size = FlexSize.Full, AspectRatio = FlexAspectRatio.Of(20, 13), AspectMode = FlexAspectMode.Cover },
            Body = new FlexBox
            {
                Layout = FlexLayout.Vertical,
                Spacing = FlexSpacing.Md,
                PaddingAll = FlexSpacing.Px(12),
                Width = FlexSize.Percent(100),
                Background = new FlexLinearGradient { Angle = "90deg", StartColor = "#FF0000", EndColor = "#0000FF80", CenterPosition = "50%" },
                Contents =
                [
                    new FlexText("店名") { Size = FlexSize.Xl, Weight = FlexWeight.Bold, Color = "#111111" },
                    new FlexBox
                    {
                        Layout = FlexLayout.Baseline,
                        Contents = [new FlexIcon(Image) { Size = FlexSize.Sm }, new FlexText("4.5"), new FlexFiller()],
                    },
                    new FlexText { Contents = { new FlexSpan("a") { Color = "#AABBCC" } } },
                ],
            },
            Footer = new FlexBox
            {
                Layout = FlexLayout.Vertical,
                Contents = [new FlexButton(new UriAction("https://example.com") { Label = "官網" }) { Style = FlexButtonStyle.Primary }, new FlexSeparator()],
            },
        });

        Assert.IsTrue(message.Validate().IsSuccess);
    }

    [TestMethod]
    public void VideoHero_WithImageFallbackAndUriAction_Passes()
    {
        var video = new FlexVideo("https://example.com/v.mp4", Image, new FlexImage(Image)) { Action = new UriAction("https://example.com") { Label = "看" } };

        Assert.IsTrue(Message(new FlexBubble { Hero = video }).Validate().IsSuccess);
    }

    [TestMethod]
    public void Carousel_Count_MustBeOneToTwelve()
    {
        Assert.IsTrue(Message(new FlexCarousel { Contents = Bubbles(1) }).Validate().IsSuccess);
        Assert.IsTrue(Message(new FlexCarousel { Contents = Bubbles(12) }).Validate().IsSuccess);
        AssertFlex(Message(new FlexCarousel()), "contents");
        AssertFlex(Message(new FlexCarousel { Contents = Bubbles(13) }), "contents");
    }

    [TestMethod]
    public void Carousel_ErrorPath_NamesTheBubbleAndComponent()
    {
        var bubbles = Bubbles(3);
        bubbles[2].Body = Vertical(new FlexText("x"), new FlexImage("http://example.com/a.png"));

        AssertFlex(Message(new FlexCarousel { Contents = bubbles }), "contents[2].body.contents[1].url");
    }

    [TestMethod]
    public void Bubble_NeedsAtLeastOneArea()
    {
        AssertFlex(Message(new FlexBubble()), string.Empty);
        Assert.IsTrue(Message(new FlexBubble { Footer = Vertical(new FlexText("x")) }).Validate().IsSuccess);
    }

    [TestMethod]
    public void Bubble_Size_MustBeAKeyword()
    {
        AssertFlex(Message(new FlexBubble { Size = "huge", Body = Vertical(new FlexText("x")) }), "size");
        Assert.IsTrue(Message(new FlexBubble { Size = "giga", Body = Vertical(new FlexText("x")) }).Validate().IsSuccess);
    }

    [TestMethod]
    public void Hero_OnlyImageBoxOrVideo()
    {
        AssertFlex(Message(new FlexBubble { Hero = new FlexText("x") }), "hero");
        Assert.IsTrue(Message(new FlexBubble { Hero = Vertical(new FlexText("x")) }).Validate().IsSuccess);
    }

    [TestMethod]
    public void Video_OutsideHero_Fails()
    {
        var video = new FlexVideo("https://example.com/v.mp4", Image, new FlexImage(Image));

        AssertFlex(Message(new FlexBubble { Body = Vertical(video) }), "body.contents[0]");
    }

    [TestMethod]
    public void Video_AltContent_MustBeImageOrBox()
    {
        var video = new FlexVideo("https://example.com/v.mp4", Image, new FlexText("x"));

        AssertFlex(Message(new FlexBubble { Hero = video }), "hero.altContent");
    }

    [TestMethod]
    public void Video_Urls_MustBeHttps()
    {
        var video = new FlexVideo("https://example.com/v.mp4", "http://example.com/p.png", new FlexImage(Image));

        AssertFlex(Message(new FlexBubble { Hero = video }), "hero.previewUrl");
    }

    [TestMethod]
    public void Video_Action_IsChecked()
    {
        var video = new FlexVideo("https://example.com/v.mp4", Image, new FlexImage(Image)) { Action = new UriAction("ftp://example.com") };
        var result = Message(new FlexBubble { Hero = video }).Validate();

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.InvalidAction, result.Error!.Code);
        StringAssert.Contains(result.Error.Message, "hero.action");
    }

    [TestMethod]
    public void Box_Layout_IsRequiredAndKnown()
    {
        var missing = new FlexBox { Layout = string.Empty };
        var unknown = new FlexBox { Layout = "grid" };

        AssertFlex(Message(new FlexBubble { Body = missing }), "body.layout");
        AssertFlex(Message(new FlexBubble { Body = unknown }), "body.layout");
    }

    [TestMethod]
    public void BaselineBox_OnlyTextIconAndFiller()
    {
        FlexComponent[] bad =
        [
            new FlexButton(new MessageAction("好")),
            new FlexImage(Image),
            Vertical(new FlexText("x")),
            new FlexSeparator(),
        ];

        foreach (var child in bad)
        {
            var box = new FlexBox { Layout = FlexLayout.Baseline, Contents = [child] };
            AssertFlex(Message(new FlexBubble { Body = box }), "body.contents[0]");
        }

        var good = new FlexBox { Layout = FlexLayout.Baseline, Contents = [new FlexText("x"), new FlexIcon(Image), new FlexFiller()] };
        Assert.IsTrue(Message(new FlexBubble { Body = good }).Validate().IsSuccess);
    }

    [TestMethod]
    public void Flex_MustNotBeNegative()
    {
        AssertFlex(Message(new FlexBubble { Body = WithBox(b => b.Flex = -1) }), "body.flex");
        AssertFlex(Message(new FlexBubble { Body = Vertical(new FlexText("x") { Flex = -1 }) }), "body.contents[0].flex");
        Assert.IsTrue(Message(new FlexBubble { Body = Vertical(new FlexText("x") { Flex = 0 }) }).Validate().IsSuccess);
    }

    [TestMethod]
    public void Box_JustifyContentAndAlignItems_AreChecked()
    {
        var justify = Vertical(new FlexText("x"));
        justify.JustifyContent = "middle";
        var align = Vertical(new FlexText("x"));
        align.AlignItems = "stretch";
        var ok = Vertical(new FlexText("x"));
        ok.JustifyContent = FlexJustifyContent.SpaceBetween;
        ok.AlignItems = FlexAlignItems.Center;

        AssertFlex(Message(new FlexBubble { Body = justify }), "body.justifyContent");
        AssertFlex(Message(new FlexBubble { Body = align }), "body.alignItems");
        Assert.IsTrue(Message(new FlexBubble { Body = ok }).Validate().IsSuccess);
    }

    [TestMethod]
    public void Dimensions_AcceptKeywordsAndUnits_RejectOthers()
    {
        Assert.IsTrue(Message(new FlexBubble { Body = WithBox(b => b.Margin = "lg") }).Validate().IsSuccess);
        Assert.IsTrue(Message(new FlexBubble { Body = WithBox(b => b.Margin = "12px") }).Validate().IsSuccess);
        AssertFlex(Message(new FlexBubble { Body = WithBox(b => b.Margin = "12") }), "body.margin");
        AssertFlex(Message(new FlexBubble { Body = WithBox(b => b.Spacing = "huge") }), "body.spacing");
        AssertFlex(Message(new FlexBubble { Body = WithBox(b => b.PaddingAll = "1em") }), "body.paddingAll");
        AssertFlex(Message(new FlexBubble { Body = WithBox(b => b.OffsetTop = "top") }), "body.offsetTop");
        AssertFlex(Message(new FlexBubble { Body = WithBox(b => b.CornerRadius = "round") }), "body.cornerRadius");
        AssertFlex(Message(new FlexBubble { Body = WithBox(b => b.BorderWidth = "thick") }), "body.borderWidth");
        AssertFlex(Message(new FlexBubble { Body = WithBox(b => b.Position = "fixed") }), "body.position");
    }

    [TestMethod]
    public void WidthAndHeight_AcceptPixelsAndPercent_ButNotKeywords()
    {
        Assert.IsTrue(Message(new FlexBubble { Body = WithBox(b => b.Width = "50%") }).Validate().IsSuccess);
        Assert.IsTrue(Message(new FlexBubble { Body = WithBox(b => b.Height = "80px") }).Validate().IsSuccess);
        AssertFlex(Message(new FlexBubble { Body = WithBox(b => b.Width = "md") }), "body.width");
        AssertFlex(Message(new FlexBubble { Body = WithBox(b => b.MaxHeight = "tall") }), "body.maxHeight");
    }

    [TestMethod]
    public void Colors_MustBeHexWithOptionalAlpha()
    {
        Assert.IsTrue(Message(new FlexBubble { Body = WithBox(b => b.BackgroundColor = "#AABBCC") }).Validate().IsSuccess);
        Assert.IsTrue(Message(new FlexBubble { Body = WithBox(b => b.BackgroundColor = "#AABBCC80") }).Validate().IsSuccess);
        AssertFlex(Message(new FlexBubble { Body = WithBox(b => b.BackgroundColor = "red") }), "body.backgroundColor");
        AssertFlex(Message(new FlexBubble { Body = WithBox(b => b.BorderColor = "#ABC") }), "body.borderColor");
        AssertFlex(Message(new FlexBubble { Body = Vertical(new FlexText("x") { Color = "blue" }) }), "body.contents[0].color");
        AssertFlex(Message(new FlexBubble { Body = Vertical(new FlexSeparator { Color = "#12" }) }), "body.contents[0].color");
        AssertFlex(Message(new FlexBubble { Body = Vertical(new FlexText("x")), Styles = new FlexBubbleStyles { Body = new FlexBlockStyle { BackgroundColor = "white" } } }), "styles.body.backgroundColor");
    }

    [TestMethod]
    public void Gradient_AngleAndColors_AreChecked()
    {
        FlexLinearGradient Gradient(string angle) => new() { Angle = angle, StartColor = "#000000", EndColor = "#FFFFFF" };

        Assert.IsTrue(Message(new FlexBubble { Body = WithBox(b => b.Background = Gradient("45deg")) }).Validate().IsSuccess);
        AssertFlex(Message(new FlexBubble { Body = WithBox(b => b.Background = Gradient("45")) }), "body.background.angle");
        AssertFlex(
            Message(new FlexBubble { Body = WithBox(b => b.Background = new FlexLinearGradient { Angle = "0deg", StartColor = "black", EndColor = "#FFFFFF" }) }),
            "body.background.startColor");
    }

    [TestMethod]
    public void Text_TextAndContents_AreExclusive()
    {
        var both = new FlexText("x");
        both.Contents.Add(new FlexSpan("y"));

        AssertFlex(Message(new FlexBubble { Body = Vertical(new FlexText()) }), "body.contents[0]");
        AssertFlex(Message(new FlexBubble { Body = Vertical(both) }), "body.contents[0]");
        Assert.IsTrue(Message(new FlexBubble { Body = Vertical(new FlexText("x")) }).Validate().IsSuccess);
    }

    [TestMethod]
    public void Text_Fields_AreChecked()
    {
        AssertFlex(Message(new FlexBubble { Body = Vertical(new FlexText("x") { MaxLines = -1 }) }), "body.contents[0].maxLines");
        AssertFlex(Message(new FlexBubble { Body = Vertical(new FlexText("x") { Size = "huge" }) }), "body.contents[0].size");
        AssertFlex(Message(new FlexBubble { Body = Vertical(new FlexText("x") { Size = FlexSize.Full }) }), "body.contents[0].size");
        AssertFlex(Message(new FlexBubble { Body = Vertical(new FlexText("x") { Align = "left" }) }), "body.contents[0].align");
        AssertFlex(Message(new FlexBubble { Body = Vertical(new FlexText("x") { Gravity = "middle" }) }), "body.contents[0].gravity");
        AssertFlex(Message(new FlexBubble { Body = Vertical(new FlexText("x") { Weight = "heavy" }) }), "body.contents[0].weight");
        AssertFlex(Message(new FlexBubble { Body = Vertical(new FlexText("x") { Style = "oblique" }) }), "body.contents[0].style");
        AssertFlex(Message(new FlexBubble { Body = Vertical(new FlexText("x") { Decoration = "strike" }) }), "body.contents[0].decoration");
        Assert.IsTrue(Message(new FlexBubble { Body = Vertical(new FlexText("x") { MaxLines = 0, Size = "14px" }) }).Validate().IsSuccess);
    }

    [TestMethod]
    public void Span_Fields_AreChecked()
    {
        FlexText Text(FlexSpan span)
        {
            var text = new FlexText();
            text.Contents.Add(span);
            return text;
        }

        AssertFlex(Message(new FlexBubble { Body = Vertical(Text(new FlexSpan("x") { Size = "huge" })) }), "body.contents[0].contents[0].size");
        AssertFlex(Message(new FlexBubble { Body = Vertical(Text(new FlexSpan("x") { Weight = "heavy" })) }), "body.contents[0].contents[0].weight");
        AssertFlex(Message(new FlexBubble { Body = Vertical(Text(new FlexSpan("x") { Color = "x" })) }), "body.contents[0].contents[0].color");
        AssertFlex(Message(new FlexBubble { Body = Vertical(Text(new FlexSpan("x") { Style = "bad" })) }), "body.contents[0].contents[0].style");
        AssertFlex(Message(new FlexBubble { Body = Vertical(Text(new FlexSpan("x") { Decoration = "bad" })) }), "body.contents[0].contents[0].decoration");
    }

    [TestMethod]
    public void ImageIconAndVideo_Urls_MustBeHttpsAndShort()
    {
        AssertFlex(Message(new FlexBubble { Body = Vertical(new FlexImage("http://example.com/a.png")) }), "body.contents[0].url");
        AssertFlex(Message(new FlexBubble { Body = Vertical(new FlexIcon("http://example.com/a.png")) }), "body.contents[0].url");
        AssertFlex(Message(new FlexBubble { Hero = new FlexImage("https://example.com/" + new string('a', 2000)) }), "hero.url");
        Assert.IsTrue(Message(new FlexBubble { Hero = new FlexImage(Image) }).Validate().IsSuccess);
    }

    [TestMethod]
    public void AspectRatio_AndAspectMode_AreChecked()
    {
        Assert.IsTrue(Message(new FlexBubble { Hero = new FlexImage(Image) { AspectRatio = "1.91:1" } }).Validate().IsSuccess);
        AssertFlex(Message(new FlexBubble { Hero = new FlexImage(Image) { AspectRatio = "wide" } }), "hero.aspectRatio");
        AssertFlex(Message(new FlexBubble { Hero = new FlexImage(Image) { AspectRatio = "16/9" } }), "hero.aspectRatio");
        AssertFlex(Message(new FlexBubble { Hero = new FlexImage(Image) { AspectMode = "stretch" } }), "hero.aspectMode");
        AssertFlex(Message(new FlexBubble { Body = Vertical(new FlexIcon(Image) { AspectRatio = "1" }) }), "body.contents[0].aspectRatio");
    }

    [TestMethod]
    public void Sizes_ImageAcceptsFullAndPercent_IconOnlyKeywordsAndPixels()
    {
        Assert.IsTrue(Message(new FlexBubble { Hero = new FlexImage(Image) { Size = "full" } }).Validate().IsSuccess);
        Assert.IsTrue(Message(new FlexBubble { Hero = new FlexImage(Image) { Size = "60%" } }).Validate().IsSuccess);
        AssertFlex(Message(new FlexBubble { Hero = new FlexImage(Image) { Size = "giant" } }), "hero.size");
        Assert.IsTrue(Message(new FlexBubble { Body = Vertical(new FlexIcon(Image) { Size = "20px" }) }).Validate().IsSuccess);
        AssertFlex(Message(new FlexBubble { Body = Vertical(new FlexIcon(Image) { Size = "full" }) }), "body.contents[0].size");
    }

    [TestMethod]
    public void Button_Fields_AreChecked()
    {
        FlexButton Button() => new(new MessageAction("好") { Label = "好" });

        var noAction = (FlexButton)Activator.CreateInstance(typeof(FlexButton), nonPublic: true)!;

        AssertFlex(Message(new FlexBubble { Body = Vertical(noAction) }), "body.contents[0].action");
        AssertFlex(Message(new FlexBubble { Body = Vertical(Set(Button(), b => b.Height = "lg")) }), "body.contents[0].height");
        AssertFlex(Message(new FlexBubble { Body = Vertical(Set(Button(), b => b.Style = "ghost")) }), "body.contents[0].style");
        AssertFlex(Message(new FlexBubble { Body = Vertical(Set(Button(), b => b.Color = "green")) }), "body.contents[0].color");
        AssertFlex(Message(new FlexBubble { Body = Vertical(Set(Button(), b => b.AdjustMode = "grow")) }), "body.contents[0].adjustMode");
        Assert.IsTrue(Message(new FlexBubble { Body = Vertical(Button()) }).Validate().IsSuccess);
    }

    [TestMethod]
    public void Actions_AreCheckedWithTheTwentyCharacterLabelLimit()
    {
        var tooLong = new MessageAction("好") { Label = new string('字', 21) };
        var result = Message(new FlexBubble { Body = Vertical(new FlexButton(tooLong)) }).Validate();

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.InvalidAction, result.Error!.Code);
        StringAssert.Contains(result.Error.Message, "body.contents[0].action");

        var onText = Message(new FlexBubble { Body = Vertical(new FlexText("x") { Action = tooLong }) }).Validate();
        var onBubble = Message(new FlexBubble { Body = Vertical(new FlexText("x")), Action = tooLong }).Validate();
        var onBox = Message(new FlexBubble { Body = WithBox(b => b.Action = tooLong) }).Validate();
        var onImage = Message(new FlexBubble { Hero = new FlexImage(Image) { Action = tooLong } }).Validate();

        Assert.AreEqual(LineErrorCodes.InvalidAction, onText.Error!.Code);
        Assert.AreEqual(LineErrorCodes.InvalidAction, onBubble.Error!.Code);
        Assert.AreEqual(LineErrorCodes.InvalidAction, onBox.Error!.Code);
        Assert.AreEqual(LineErrorCodes.InvalidAction, onImage.Error!.Code);
    }

    [TestMethod]
    public void Raw_ContainerAndComponent_AreNotChecked()
    {
        var rawComponent = FlexTestSupport.Raw("""{"type":"image","url":"http://not-https","size":"giant"}""");
        var inBaseline = new FlexBox { Layout = FlexLayout.Baseline, Contents = [rawComponent] };
        using var document = System.Text.Json.JsonDocument.Parse("""{"type":"bubble","size":"huge"}""");

        Assert.IsTrue(Message(new FlexBubble { Hero = rawComponent, Body = inBaseline }).Validate().IsSuccess);
        Assert.IsTrue(Message(new RawFlexContainer(document.RootElement)).Validate().IsSuccess);
        Assert.IsTrue(Message(new FlexBubble { Body = Vertical(rawComponent) }).Validate().IsSuccess);
    }

    [TestMethod]
    public void RawAction_InsideFlex_IsNotChecked()
    {
        using var document = System.Text.Json.JsonDocument.Parse("""{"type":"message","label":"超過二十個字的標籤超過二十個字的標籤超過二十個字","text":"x"}""");
        var message = Message(new FlexBubble { Body = Vertical(new FlexButton(new RawAction(document.RootElement))) });

        Assert.IsTrue(message.Validate().IsSuccess);
    }

    [TestMethod]
    public void ParsedFlexSimulatorJson_ValidatesLikeTheTypedModel()
    {
        var good = FlexMessage.Parse("alt", """{"type":"bubble","body":{"type":"box","layout":"vertical","contents":[{"type":"text","text":"Hi"}]}}""").GetValueOrThrow();
        var bad = FlexMessage.Parse("alt", """{"type":"bubble","body":{"type":"box","layout":"vertical","contents":[{"type":"text","text":"Hi","size":"huge"}]}}""").GetValueOrThrow();

        Assert.IsTrue(good.Validate().IsSuccess);
        AssertFlex(bad, "body.contents[0].size");
    }

    [TestMethod]
    public void GetRawNodePaths_ListsOnlyRawNodes_WithTheValidationPathFormat()
    {
        var message = FlexMessage.Parse(
            "alt",
            """{"type":"carousel","contents":[{"type":"bubble","body":{"type":"box","layout":"vertical","contents":[{"type":"text","text":"a"}]}},{"type":"bubble","body":{"type":"box","layout":"vertical","contents":[{"type":"text","text":"a"},{"type":"spacer"}]}}]}""")
            .GetValueOrThrow();

        var paths = message.Container!.GetRawNodePaths();

        Assert.AreEqual(1, paths.Count);
        Assert.AreEqual("contents[1].body.contents[1]", paths[0]);
        Assert.AreEqual(0, new FlexBubble { Body = Vertical(new FlexText("x")) }.GetRawNodePaths().Count);
    }

    [TestMethod]
    public void Size_OverTheLimit_Fails_ForTypedAndVerbatimPaths()
    {
        var filler = new string('a', LineMessagingLimits.MaxFlexBubbleBytes);
        var typed = Message(new FlexBubble { Body = Vertical(new FlexText(filler)) });
        var verbatim = FlexMessage.FromJson("alt", $$$"""{"type":"bubble","body":{"type":"box","layout":"vertical","contents":[{"type":"text","text":"{{{filler}}}"}]}}""").GetValueOrThrow();
        var small = FlexMessage.FromJson("alt", """{"type":"bubble","body":{"type":"box","layout":"vertical","contents":[]}}""").GetValueOrThrow();

        AssertFlex(typed, string.Empty);
        StringAssert.Contains(typed.Validate().Error!.Message, "bytes");
        AssertFlex(verbatim, string.Empty);
        Assert.IsTrue(small.Validate().IsSuccess);
    }

    [TestMethod]
    public void Size_CarouselHasALargerLimitThanABubble()
    {
        // 輪播 2 個氣泡、每個 20 KB 左右:單看總量超過氣泡上限,但在輪播上限之內。
        var bubbles = Enumerable.Range(0, 2).Select(_ => new FlexBubble { Body = Vertical(new FlexText(new string('a', 20 * 1024))) }).ToList();

        Assert.IsTrue(Message(new FlexCarousel { Contents = bubbles }).Validate().IsSuccess);
    }

    [TestMethod]
    public void Size_CountsUtf8Bytes_NotEscapedLength()
    {
        // 約 9000 個中文字 = 27000 位元組(未跳脫),低於 30 KB;若以 \uXXXX 計則是 54000。
        var text = new string('字', 9000);

        Assert.IsTrue(Message(new FlexBubble { Body = Vertical(new FlexText(text)) }).Validate().IsSuccess);
    }

    [TestMethod]
    public void Validate_ChecksQuickReplyAndAltTextBeforeTheLayout()
    {
        var message = Message(new FlexBubble());
        message.QuickReply = new LineQuickReply();

        Assert.AreEqual(LineErrorCodes.TooManyQuickReplyItems, message.Validate().Error!.Code);
    }

    [TestMethod]
    public async Task PushAsync_InvalidFlex_FailsWithoutSending()
    {
        var handler = FakeHttpMessageHandler.Json("""{"sentMessages":[]}""");
        var client = new LineMessagingClient(
            new HttpPipelineClient(new HttpClient(handler)),
            Options.Create(new LineMessagingOptions { ChannelAccessToken = "test-channel-access-token" }));

        var result = await client.PushAsync("U1", [Message(new FlexBubble { Hero = new FlexText("x") })]);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.InvalidFlex, result.Error!.Code);
        StringAssert.Contains(result.Error.Message, "hero");
        Assert.AreEqual(0, handler.Requests.Count);
    }

    private static FlexMessage Message(FlexContainer container) => new("alt", container);

    private static FlexBox Vertical(params FlexComponent[] contents) => new() { Layout = FlexLayout.Vertical, Contents = contents };

    private static FlexBox WithBox(Action<FlexBox> configure)
    {
        var box = Vertical(new FlexText("x"));
        configure(box);
        return box;
    }

    private static T Set<T>(T value, Action<T> configure)
    {
        configure(value);
        return value;
    }

    private static List<FlexBubble> Bubbles(int count) =>
        Enumerable.Range(0, count).Select(_ => new FlexBubble { Body = Vertical(new FlexText("x")) }).ToList();

    private static void AssertFlex(FlexMessage message, string pathFragment)
    {
        var result = message.Validate();

        Assert.IsTrue(result.IsFailure, "expected a Flex validation failure at: " + pathFragment);
        Assert.IsNotNull(result.Error);
        Assert.AreEqual(LineErrorCodes.InvalidFlex, result.Error.Code);
        StringAssert.Contains(result.Error.Message, pathFragment);
    }
}
