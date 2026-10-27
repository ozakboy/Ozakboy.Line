using Ozakboy.Line.Messaging.Messages.Flex;

namespace Ozakboy.Line.Tests.Flex;

/// <summary>
/// 關鍵字常數與量值工廠:值必須逐字等於 LINE 規格裡的字串(尤其是 <c>3xl</c>、<c>1.91:1</c> 這類列舉裝不下的值)。
/// Keyword constants and value factories: values must equal LINE's strings verbatim, notably those an enum cannot hold such as <c>3xl</c> and <c>1.91:1</c>.
/// </summary>
[TestClass]
public sealed class FlexKeywordTests
{
    [TestMethod]
    public void SizeFactories_WriteUnitsLikeLine()
    {
        Assert.AreEqual("20px", FlexSize.Px(20));
        Assert.AreEqual("50%", FlexSize.Percent(50));
        Assert.AreEqual("8px", FlexSpacing.Px(8));
        Assert.AreEqual("2px", FlexBorderWidth.Px(2));
        Assert.AreEqual("6px", FlexCornerRadius.Px(6));
        Assert.AreEqual("-5px", FlexSize.Px(-5));
    }

    [TestMethod]
    public void ColourFactory_WritesUpperCaseHex()
    {
        Assert.AreEqual("#06C755", FlexColor.Rgb(6, 199, 85));
        Assert.AreEqual("#06C75580", FlexColor.Rgb(6, 199, 85, 128));
        Assert.AreEqual("#000000", FlexColor.Rgb(0, 0, 0));
        Assert.AreEqual("#FFFFFFFF", FlexColor.Rgb(255, 255, 255, 255));
    }

    [TestMethod]
    public void AspectRatioFactory_WritesWidthColonHeight()
    {
        Assert.AreEqual("16:9", FlexAspectRatio.Of(16, 9));
        Assert.AreEqual("1.91:1", FlexAspectRatio.Of(1.91, 1));
        Assert.AreEqual("1:1", FlexAspectRatio.Of(1, 1));
    }

    [TestMethod]
    public void Keywords_AreTheStringsLineExpects()
    {
        // 以陣列逐項比對:常數對常數的斷言會被分析器擋下,而且這樣一眼看得出哪個值錯。
        // Compared pairwise from a table: a constant-against-constant assertion is flagged by the analyser, and this shows which value is wrong.
        (string Expected, string Actual)[] table =
        [
            ("3xl", FlexSize.ThreeXl),
            ("4xl", FlexSize.FourXl),
            ("5xl", FlexSize.FiveXl),
            ("full", FlexSize.Full),
            ("xxs", FlexSize.Xxs),
            ("none", FlexSpacing.None),
            ("xxl", FlexSpacing.Xxl),
            ("semi-bold", FlexBorderWidth.SemiBold),
            ("horizontal", FlexLayout.Horizontal),
            ("vertical", FlexLayout.Vertical),
            ("baseline", FlexLayout.Baseline),
            ("start", FlexAlign.Start),
            ("end", FlexAlign.End),
            ("top", FlexGravity.Top),
            ("regular", FlexWeight.Regular),
            ("italic", FlexTextStyle.Italic),
            ("line-through", FlexDecoration.LineThrough),
            ("link", FlexButtonStyle.Link),
            ("secondary", FlexButtonStyle.Secondary),
            ("sm", FlexButtonHeight.Sm),
            ("fit", FlexAspectMode.Fit),
            ("absolute", FlexPosition.Absolute),
            ("shrink-to-fit", FlexAdjustMode.ShrinkToFit),
            ("nano", FlexBubbleSize.Nano),
            ("deca", FlexBubbleSize.Deca),
            ("hecto", FlexBubbleSize.Hecto),
            ("giga", FlexBubbleSize.Giga),
            ("rtl", FlexDirection.Rtl),
            ("flex-start", FlexJustifyContent.FlexStart),
            ("space-evenly", FlexJustifyContent.SpaceEvenly),
            ("flex-end", FlexAlignItems.FlexEnd),
        ];

        foreach (var (expected, actual) in table)
        {
            Assert.AreEqual(expected, actual);
        }
    }
}
