using Ozakboy.Line.Messaging.Actions;
using Ozakboy.Line.Messaging.Messages.Flex;

namespace Ozakboy.Line.Tests.Flex;

/// <summary>
/// 強型別 Flex 模型的序列化黃金檔:用物件初始化器組出版面,輸出必須與樣本 JSON 一致(含欄位順序)。
/// Golden tests of the typed Flex model's serialisation: the layout is assembled with object initialisers and the output
/// must equal the sample JSON, field order included.
/// </summary>
[TestClass]
public sealed class FlexGoldenTests
{
    [TestMethod]
    public void Restaurant_TypedModel_MatchesSample()
    {
        const string Star = "https://example.com/star-gold.png";

        var bubble = new FlexBubble
        {
            Hero = new FlexImage("https://example.com/cafe.png")
            {
                Size = FlexSize.Full,
                AspectRatio = FlexAspectRatio.Banner,
                AspectMode = FlexAspectMode.Cover,
                Action = new UriAction("https://example.com/"),
            },
            Body = new FlexBox
            {
                Layout = FlexLayout.Vertical,
                Contents =
                [
                    new FlexText("Brown Cafe") { Size = FlexSize.Xl, Weight = FlexWeight.Bold },
                    new FlexBox
                    {
                        Layout = FlexLayout.Baseline,
                        Margin = FlexSpacing.Md,
                        Contents =
                        [
                            new FlexIcon(Star) { Size = FlexSize.Sm },
                            new FlexIcon(Star) { Size = FlexSize.Sm },
                            new FlexIcon(Star) { Size = FlexSize.Sm },
                            new FlexIcon(Star) { Size = FlexSize.Sm },
                            new FlexIcon("https://example.com/star-gray.png") { Size = FlexSize.Sm },
                            new FlexText("4.0") { Flex = 0, Margin = FlexSpacing.Md, Size = FlexSize.Sm, Color = "#999999" },
                        ],
                    },
                    new FlexBox
                    {
                        Layout = FlexLayout.Vertical,
                        Margin = FlexSpacing.Lg,
                        Spacing = FlexSpacing.Sm,
                        Contents =
                        [
                            InfoRow("Place", "Flex Tower, 7-7-4 Midori-ku, Tokyo"),
                            InfoRow("Time", "10:00 - 23:00"),
                        ],
                    },
                ],
            },
            Footer = new FlexBox
            {
                Layout = FlexLayout.Vertical,
                Spacing = FlexSpacing.Sm,
                Flex = 0,
                Contents =
                [
                    new FlexButton(new UriAction("https://example.com/call") { Label = "CALL" }) { Height = FlexButtonHeight.Sm, Style = FlexButtonStyle.Link },
                    new FlexButton(new UriAction("https://example.com/") { Label = "WEBSITE" }) { Height = FlexButtonHeight.Sm, Style = FlexButtonStyle.Link },
                    new FlexBox { Layout = FlexLayout.Vertical, Margin = FlexSpacing.Sm },
                ],
            },
        };

        Assert.AreEqual(FlexTestSupport.Sample("flex-restaurant"), FlexTestSupport.Write(bubble));
    }

    [TestMethod]
    public void Receipt_TypedModel_MatchesSample()
    {
        var bubble = new FlexBubble
        {
            Size = FlexBubbleSize.Mega,
            Body = new FlexBox
            {
                Layout = FlexLayout.Vertical,
                Contents =
                [
                    new FlexText("RECEIPT") { Size = FlexSize.Sm, Weight = FlexWeight.Bold, Color = "#1DB446" },
                    new FlexText("Brown Store") { Margin = FlexSpacing.Md, Size = FlexSize.Xxl, Weight = FlexWeight.Bold },
                    new FlexText("Miraina Tower, 4-1-6 Shinjuku, Tokyo") { Size = FlexSize.Xs, Wrap = true, Color = "#AAAAAA" },
                    new FlexSeparator { Margin = FlexSpacing.Xxl },
                    new FlexBox
                    {
                        Layout = FlexLayout.Vertical,
                        Spacing = FlexSpacing.Sm,
                        Margin = FlexSpacing.Xxl,
                        Contents =
                        [
                            PriceRow("Energy Drink", "$2.99"),
                            PriceRow("Chewing Gum", "$0.99"),
                            new FlexSeparator { Margin = FlexSpacing.Xxl },
                            new FlexBox
                            {
                                Layout = FlexLayout.Horizontal,
                                Margin = FlexSpacing.Xxl,
                                Contents =
                                [
                                    new FlexText("ITEMS") { Size = FlexSize.Sm, Color = "#555555" },
                                    new FlexText("2") { Size = FlexSize.Sm, Align = FlexAlign.End, Color = "#111111" },
                                ],
                            },
                            new FlexBox
                            {
                                Layout = FlexLayout.Horizontal,
                                Contents =
                                [
                                    new FlexText("TOTAL") { Size = FlexSize.Sm, Color = "#555555" },
                                    new FlexText("$3.98") { Size = FlexSize.Sm, Align = FlexAlign.End, Color = "#111111" },
                                ],
                            },
                        ],
                    },
                    new FlexSeparator { Margin = FlexSpacing.Xxl },
                    new FlexBox
                    {
                        Layout = FlexLayout.Horizontal,
                        Margin = FlexSpacing.Md,
                        Contents =
                        [
                            new FlexText("PAYMENT ID") { Flex = 0, Size = FlexSize.Xs, Color = "#AAAAAA" },
                            new FlexText("#743289384279") { Size = FlexSize.Xs, Align = FlexAlign.End, Color = "#AAAAAA" },
                        ],
                    },
                ],
            },
            Styles = new FlexBubbleStyles { Footer = new FlexBlockStyle { Separator = true } },
        };

        Assert.AreEqual(FlexTestSupport.Sample("flex-receipt"), FlexTestSupport.Write(bubble));
    }

    [TestMethod]
    public void Carousel_ThreeBubbles_MatchesSample()
    {
        var carousel = new FlexCarousel
        {
            Contents =
            [
                ProductBubble("https://example.com/p1.png", "Brown Cafe", "Flex Tower", "CALL", "https://example.com/1"),
                ProductBubble("https://example.com/p2.png", "Brown&Cony's Lodge", "Hotel", "BOOK", "https://example.com/2"),
                ProductBubble("https://example.com/p3.png", "Sally's Diner", "Dinner", "RESERVE", "https://example.com/3"),
            ],
        };

        Assert.AreEqual(FlexTestSupport.Sample("flex-carousel"), FlexTestSupport.Write(carousel));
    }

    [TestMethod]
    public void Components_EveryField_MatchesSample()
    {
        var bubble = new FlexBubble
        {
            Size = FlexBubbleSize.Giga,
            Direction = FlexDirection.Rtl,
            Header = new FlexBox
            {
                Layout = FlexLayout.Horizontal,
                Contents =
                [
                    new FlexText("標題 Title")
                    {
                        AdjustMode = FlexAdjustMode.ShrinkToFit,
                        Flex = 1,
                        Margin = FlexSpacing.None,
                        Position = FlexPosition.Relative,
                        Size = FlexSize.Lg,
                        Align = FlexAlign.Center,
                        Gravity = FlexGravity.Center,
                        Wrap = false,
                        LineSpacing = "4px",
                        MaxLines = 1,
                        Weight = FlexWeight.Bold,
                        Color = "#FFFFFF",
                        Style = FlexTextStyle.Italic,
                        Decoration = FlexDecoration.Underline,
                        Scaling = true,
                    },
                    new FlexFiller { Flex = 2 },
                ],
                Flex = 1,
                Spacing = FlexSpacing.Md,
                Margin = FlexSpacing.None,
                BackgroundColor = FlexColor.Rgb(6, 199, 85),
                BorderColor = FlexColor.Rgb(0, 0, 0, 51),
                BorderWidth = FlexBorderWidth.Light,
                CornerRadius = FlexCornerRadius.Md,
                Width = FlexSize.Percent(100),
                MaxWidth = FlexSize.Px(400),
                Height = FlexSize.Px(60),
                MaxHeight = FlexSize.Px(80),
                PaddingAll = FlexSpacing.Px(10),
                PaddingTop = FlexSpacing.Px(4),
                PaddingBottom = FlexSpacing.Px(4),
                PaddingStart = FlexSpacing.Px(8),
                PaddingEnd = FlexSpacing.Px(8),
                JustifyContent = FlexJustifyContent.SpaceBetween,
                AlignItems = FlexAlignItems.Center,
                Background = new FlexLinearGradient
                {
                    Angle = "90deg",
                    StartColor = "#FF0000",
                    EndColor = "#0000FF",
                    CenterColor = "#00FF00",
                    CenterPosition = "50%",
                },
                Action = new PostbackAction("a=1") { Label = "header" },
            },
            Hero = new FlexVideo(
                "https://example.com/v.mp4",
                "https://example.com/v.png",
                new FlexImage("https://example.com/alt.png") { Size = FlexSize.Full, AspectRatio = FlexAspectRatio.Banner, AspectMode = FlexAspectMode.Fit })
            {
                AspectRatio = FlexAspectRatio.Banner,
                Action = new UriAction("https://example.com/more") { Label = "More" },
            },
            Body = new FlexBox
            {
                Layout = FlexLayout.Vertical,
                Contents =
                [
                    new FlexText
                    {
                        Contents =
                        [
                            new FlexSpan("Hello ")
                            {
                                Color = "#111111",
                                Size = FlexSize.Md,
                                Weight = FlexWeight.Regular,
                                Style = FlexTextStyle.Normal,
                                Decoration = FlexDecoration.None,
                            },
                            new FlexSpan("World") { Weight = FlexWeight.Bold, Decoration = FlexDecoration.LineThrough },
                        ],
                    },
                    new FlexBox
                    {
                        Layout = FlexLayout.Baseline,
                        Position = FlexPosition.Absolute,
                        OffsetTop = FlexSpacing.Px(10),
                        OffsetBottom = FlexSpacing.Px(10),
                        OffsetStart = FlexSpacing.Px(10),
                        OffsetEnd = FlexSpacing.Px(10),
                        Contents =
                        [
                            new FlexIcon("https://example.com/i.png")
                            {
                                Margin = FlexSpacing.Sm,
                                Position = FlexPosition.Absolute,
                                OffsetTop = FlexSpacing.Px(2),
                                OffsetBottom = FlexSpacing.Px(2),
                                OffsetStart = FlexSpacing.Px(2),
                                OffsetEnd = FlexSpacing.Px(2),
                                Size = FlexSize.Xs,
                                AspectRatio = FlexAspectRatio.Square,
                                Scaling = false,
                            },
                        ],
                    },
                    new FlexImage("https://example.com/a.png")
                    {
                        Flex = 1,
                        Margin = FlexSpacing.Lg,
                        Position = FlexPosition.Relative,
                        OffsetTop = FlexSpacing.Px(1),
                        Align = FlexAlign.Start,
                        Gravity = FlexGravity.Bottom,
                        Size = FlexSize.FiveXl,
                        AspectRatio = FlexAspectRatio.Of(1.91, 1),
                        AspectMode = FlexAspectMode.Cover,
                        BackgroundColor = "#EEEEEE",
                        Action = new MessageAction("hi") { Label = "hi" },
                        Animated = true,
                    },
                    new FlexSeparator { Margin = FlexSpacing.Xl, Color = "#CCCCCC" },
                    FlexTestSupport.Raw("""{"type":"spacer","size":"md"}"""),
                ],
            },
            Footer = new FlexBox
            {
                Layout = FlexLayout.Horizontal,
                Contents =
                [
                    new FlexButton(new MessageAction("ok") { Label = "OK" })
                    {
                        Flex = 1,
                        Margin = FlexSpacing.Sm,
                        Position = FlexPosition.Relative,
                        OffsetStart = FlexSpacing.Px(1),
                        Height = FlexButtonHeight.Md,
                        Style = FlexButtonStyle.Secondary,
                        Color = "#AAAAAA",
                        Gravity = FlexGravity.Center,
                        AdjustMode = FlexAdjustMode.ShrinkToFit,
                        Scaling = true,
                    },
                ],
            },
            Styles = new FlexBubbleStyles
            {
                Header = new FlexBlockStyle { BackgroundColor = "#FFFFFF", Separator = false, SeparatorColor = "#000000" },
                Hero = new FlexBlockStyle { BackgroundColor = "#EEEEEE" },
                Body = new FlexBlockStyle { Separator = true },
                Footer = new FlexBlockStyle { SeparatorColor = "#CCCCCC" },
            },
            Action = new UriAction("https://example.com/bubble") { Label = "Bubble" },
        };

        Assert.AreEqual(FlexTestSupport.Sample("flex-components"), FlexTestSupport.Write(bubble));
    }

    [TestMethod]
    public void EmptyBubble_WritesOnlyType()
    {
        Assert.AreEqual("""{"type":"bubble"}""", FlexTestSupport.Write(new FlexBubble()));
    }

    [TestMethod]
    public void Box_WithoutContents_StillWritesEmptyContentsArray()
    {
        var box = new FlexBox { Layout = FlexLayout.Vertical };

        Assert.AreEqual("""{"type":"box","layout":"vertical","contents":[]}""", System.Text.Json.JsonSerializer.Serialize(box));
    }

    [TestMethod]
    public void ObjectInitialiserShape_FromTheDesignNote_WritesExpectedJson()
    {
        var bubble = new FlexBubble
        {
            Body = new FlexBox
            {
                Layout = FlexLayout.Vertical,
                Contents = [new FlexText("Hello") { Weight = FlexWeight.Bold }],
            },
        };

        Assert.AreEqual(
            """{"type":"bubble","body":{"type":"box","layout":"vertical","contents":[{"type":"text","text":"Hello","weight":"bold"}]}}""",
            FlexTestSupport.Write(bubble));
    }

    [TestMethod]
    public void Text_WithoutTextOrContents_WritesOnlyType()
    {
        Assert.AreEqual("""{"type":"text"}""", System.Text.Json.JsonSerializer.Serialize(new FlexText()));
    }

    [TestMethod]
    public void NonAsciiText_IsEscapedLikeTheOtherMessages()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new FlexText("餐廳"));

        Assert.AreEqual("{\"type\":\"text\",\"text\":\"\\u9910\\u5EF3\"}", json);
    }

    private static FlexBox InfoRow(string label, string value) => new()
    {
        Layout = FlexLayout.Baseline,
        Spacing = FlexSpacing.Sm,
        Contents =
        [
            new FlexText(label) { Flex = 1, Size = FlexSize.Sm, Color = "#AAAAAA" },
            new FlexText(value) { Flex = 5, Size = FlexSize.Sm, Wrap = true, Color = "#666666" },
        ],
    };

    private static FlexBox PriceRow(string name, string price) => new()
    {
        Layout = FlexLayout.Horizontal,
        Contents =
        [
            new FlexText(name) { Flex = 0, Size = FlexSize.Sm, Color = "#555555" },
            new FlexText(price) { Size = FlexSize.Sm, Align = FlexAlign.End, Color = "#111111" },
        ],
    };

    private static FlexBubble ProductBubble(string image, string title, string subtitle, string label, string uri) => new()
    {
        Size = FlexBubbleSize.Micro,
        Hero = new FlexImage(image) { Size = FlexSize.Full, AspectRatio = FlexAspectRatio.Of(320, 213), AspectMode = FlexAspectMode.Cover },
        Body = new FlexBox
        {
            Layout = FlexLayout.Vertical,
            PaddingAll = FlexSpacing.Px(13),
            Contents =
            [
                new FlexText(title) { Size = FlexSize.Sm, Weight = FlexWeight.Bold },
                new FlexText(subtitle) { Margin = FlexSpacing.Sm, Size = FlexSize.Xs, Wrap = true, Color = "#8C8C8C" },
            ],
        },
        Footer = new FlexBox
        {
            Layout = FlexLayout.Vertical,
            Contents =
            [
                new FlexButton(new UriAction(uri) { Label = label })
                {
                    Height = FlexButtonHeight.Sm,
                    Style = FlexButtonStyle.Primary,
                    Color = "#06C755",
                },
            ],
        },
    };
}
