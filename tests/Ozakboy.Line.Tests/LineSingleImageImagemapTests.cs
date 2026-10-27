using System.Text.Json;
using Ozakboy.Line.Messaging.Messages;
using Ozakboy.Line.Messaging.Messages.Imagemap;

namespace Ozakboy.Line.Tests;

/// <summary>
/// <see cref="ImagemapMessage.ForSingleImage"/> 的測試:baseUrl 尾端補 <c>?</c> 的規則、全圖區域、序列化黃金檔與驗證。
/// Tests for <see cref="ImagemapMessage.ForSingleImage"/>: the rule for adding <c>?</c> to the end of baseUrl, the
/// whole-image area, the serialisation golden file, and validation.
/// </summary>
[TestClass]
public sealed class LineSingleImageImagemapTests
{
    private static readonly JsonSerializerOptions RelaxedOptions = new(LineJson.Options)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static ImagemapUriAction Link(LineImagemapArea? area = null) =>
        new("https://example.com/go", area ?? new LineImagemapArea(10, 20, 30, 40)) { Label = "前往" };

    [TestMethod]
    public void ForSingleImage_UrlWithoutQuery_AppendsQuestionMark()
    {
        var message = ImagemapMessage.ForSingleImage("https://cdn.example.com/banner.png", "橫幅", 520, Link()).GetValueOrThrow();

        Assert.AreEqual("https://cdn.example.com/banner.png?", message.BaseUrl);
        Assert.AreEqual(520, message.BaseHeight);
    }

    [TestMethod]
    public void ForSingleImage_UrlWithQuery_AppendsAmpersand()
    {
        var message = ImagemapMessage.ForSingleImage("https://cdn.example.com/banner.png?v=3", "橫幅", 520, Link()).GetValueOrThrow();

        Assert.AreEqual("https://cdn.example.com/banner.png?v=3&", message.BaseUrl);
    }

    [TestMethod]
    [DataRow("https://cdn.example.com/banner.png?")]
    [DataRow("https://cdn.example.com/banner.png?v=3&")]
    public void ForSingleImage_UrlAlreadyEndingInSeparator_IsKept(string url)
    {
        var message = ImagemapMessage.ForSingleImage(url, "橫幅", 520, Link()).GetValueOrThrow();

        Assert.AreEqual(url, message.BaseUrl);
    }

    [TestMethod]
    public void ForSingleImage_UrlWithFragment_FailsAsInvalidImagemap()
    {
        var result = ImagemapMessage.ForSingleImage("https://cdn.example.com/banner.png#top", "橫幅", 520, Link());

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.InvalidImagemap, result.Error!.Code);
    }

    [TestMethod]
    [DataRow("http://cdn.example.com/banner.png")]
    [DataRow("ftp://cdn.example.com/banner.png")]
    [DataRow("")]
    [DataRow("  ")]
    public void ForSingleImage_NonHttpsOrBlankUrl_FailsAsInvalidImagemap(string url)
    {
        var result = ImagemapMessage.ForSingleImage(url, "橫幅", 520, Link());

        Assert.AreEqual(LineErrorCodes.InvalidImagemap, result.Error!.Code);
    }

    [TestMethod]
    [DataRow("", 520)]
    [DataRow("橫幅", 0)]
    [DataRow("橫幅", -1)]
    public void ForSingleImage_BadAltTextOrHeight_FailsInsteadOfThrowing(string altText, int height)
    {
        var result = ImagemapMessage.ForSingleImage("https://cdn.example.com/banner.png", altText, height, Link());

        Assert.AreEqual(LineErrorCodes.InvalidImagemap, result.Error!.Code);
    }

    [TestMethod]
    public void ForSingleImage_AltTextOverTheLimit_FailsViaValidate()
    {
        var result = ImagemapMessage.ForSingleImage(
            "https://cdn.example.com/banner.png", new string('a', LineMessagingLimits.MaxAltTextLength + 1), 520, Link());

        Assert.AreEqual(LineErrorCodes.InvalidImagemap, result.Error!.Code);
    }

    [TestMethod]
    public void ForSingleImage_BadAction_FailsAsInvalidAction()
    {
        var bad = new ImagemapUriAction("javascript:alert(1)", new LineImagemapArea(0, 0, 1, 1));

        var result = ImagemapMessage.ForSingleImage("https://cdn.example.com/banner.png", "橫幅", 520, bad);

        Assert.AreEqual(LineErrorCodes.InvalidAction, result.Error!.Code);
    }

    [TestMethod]
    public void ForSingleImage_NullAction_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => ImagemapMessage.ForSingleImage("https://cdn.example.com/banner.png", "橫幅", 520, null!));
    }

    [TestMethod]
    public void ForSingleImage_ReplacesTheAreaWithTheWholeImageOnACopy()
    {
        var original = Link();

        var message = ImagemapMessage.ForSingleImage("https://cdn.example.com/banner.png", "橫幅", 520, original).GetValueOrThrow();

        var only = message.Actions.Single();
        Assert.AreEqual(new LineImagemapArea(0, 0, 1040, 520), only.Area);
        Assert.AreEqual(new LineImagemapArea(10, 20, 30, 40), original.Area, "傳入的動作不被改動。");
        Assert.AreNotSame(original, only);
        Assert.AreEqual("前往", only.Label);
        Assert.AreEqual("https://example.com/go", ((ImagemapUriAction)only).LinkUri);
    }

    [TestMethod]
    public void ForSingleImage_MessageAction_KeepsItsTextAndLabel()
    {
        var action = new ImagemapMessageAction("我要預約", new LineImagemapArea(5, 5, 5, 5)) { Label = "預約" };

        var message = ImagemapMessage.ForSingleImage("https://cdn.example.com/banner.png", "橫幅", 300, action).GetValueOrThrow();

        var copy = (ImagemapMessageAction)message.Actions.Single();
        Assert.AreEqual("我要預約", copy.Text);
        Assert.AreEqual("預約", copy.Label);
        Assert.AreEqual(new LineImagemapArea(0, 0, 1040, 300), copy.Area);
    }

    [TestMethod]
    public void ForSingleImage_MatchesGolden()
    {
        var message = ImagemapMessage.ForSingleImage(
            "https://cdn.example.com/banner.png", "春季活動", 520, Link()).GetValueOrThrow();

        Assert.AreEqual(
            """{"type":"imagemap","baseUrl":"https://cdn.example.com/banner.png?","altText":"春季活動","baseSize":{"width":1040,"height":520},"actions":[{"type":"uri","label":"前往","linkUri":"https://example.com/go","area":{"x":0,"y":0,"width":1040,"height":520}}]}""",
            JsonSerializer.Serialize(message, RelaxedOptions));
    }

    [TestMethod]
    public void ForSingleImage_ResultPassesValidate()
    {
        var message = ImagemapMessage.ForSingleImage("https://cdn.example.com/banner.png", "橫幅", 520, Link()).GetValueOrThrow();

        Assert.IsTrue(message.Validate().IsSuccess);
    }
}
