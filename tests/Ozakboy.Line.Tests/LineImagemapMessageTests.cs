using System.Text.Json;
using Ozakboy.Line.Messaging.Messages;
using Ozakboy.Line.Messaging.Messages.Imagemap;

namespace Ozakboy.Line.Tests;

/// <summary>
/// 圖片地圖訊息的測試:欄位名、固定 1040 的底圖寬度、影片區塊,以及區域數上限。
/// Tests for imagemap messages: field names, the base width fixed at 1040, the video block, and the area cap.
/// </summary>
[TestClass]
public sealed class LineImagemapMessageTests
{
    [TestMethod]
    public void ImagemapMessage_WritesBaseSizeWithFixedWidthAndBothActionKinds()
    {
        var message = new ImagemapMessage("https://example.com/map", "地圖", 700)
        {
            Actions =
            {
                new ImagemapUriAction("https://example.com/a", new LineImagemapArea(0, 0, 520, 700)) { Label = "左" },
                new ImagemapMessageAction("右邊", new LineImagemapArea(520, 0, 520, 700)),
            },
        };

        var json = Serialize(message);

        Assert.AreEqual("imagemap", json.GetProperty("type").GetString());
        Assert.AreEqual("https://example.com/map", json.GetProperty("baseUrl").GetString());
        Assert.AreEqual("地圖", json.GetProperty("altText").GetString());
        Assert.AreEqual(LineMessagingLimits.ImagemapBaseWidth, json.GetProperty("baseSize").GetProperty("width").GetInt32());
        Assert.AreEqual(700, json.GetProperty("baseSize").GetProperty("height").GetInt32());
        Assert.IsFalse(json.TryGetProperty("video", out _));

        var actions = json.GetProperty("actions");
        Assert.AreEqual(2, actions.GetArrayLength());
        Assert.AreEqual("uri", actions[0].GetProperty("type").GetString());
        Assert.AreEqual("左", actions[0].GetProperty("label").GetString());
        Assert.AreEqual("https://example.com/a", actions[0].GetProperty("linkUri").GetString(), "圖片地圖的欄位是 linkUri,不是 uri。");
        Assert.AreEqual(520, actions[0].GetProperty("area").GetProperty("width").GetInt32());
        Assert.AreEqual("message", actions[1].GetProperty("type").GetString());
        Assert.AreEqual("右邊", actions[1].GetProperty("text").GetString());
        Assert.AreEqual(520, actions[1].GetProperty("area").GetProperty("x").GetInt32());
        Assert.IsFalse(actions[1].TryGetProperty("label", out _));
    }

    [TestMethod]
    public void ImagemapMessage_WritesVideoWithExternalLink()
    {
        var message = new ImagemapMessage("https://example.com/map", "地圖", 1040)
        {
            Video = new LineImagemapVideo("https://example.com/v.mp4", "https://example.com/p.jpg", new LineImagemapArea(0, 0, 1040, 585))
            {
                ExternalLinkUri = "https://example.com/more",
                ExternalLinkLabel = "看更多",
            },
            Actions = { new ImagemapMessageAction("點了", new LineImagemapArea(0, 585, 1040, 455)) },
        };

        var video = Serialize(message).GetProperty("video");

        Assert.AreEqual("https://example.com/v.mp4", video.GetProperty("originalContentUrl").GetString());
        Assert.AreEqual("https://example.com/p.jpg", video.GetProperty("previewImageUrl").GetString());
        Assert.AreEqual(585, video.GetProperty("area").GetProperty("height").GetInt32());
        Assert.AreEqual("https://example.com/more", video.GetProperty("externalLink").GetProperty("linkUri").GetString());
        Assert.AreEqual("看更多", video.GetProperty("externalLink").GetProperty("label").GetString());
    }

    [TestMethod]
    public void ImagemapMessage_ExternalLinkNeedsBothFields()
    {
        // 只給一半的 externalLink 不輸出:LINE 要求兩個欄位一起給。
        var message = new ImagemapMessage("https://example.com/map", "地圖", 1040)
        {
            Video = new LineImagemapVideo("https://example.com/v.mp4", "https://example.com/p.jpg", new LineImagemapArea(0, 0, 1040, 585))
            {
                ExternalLinkUri = "https://example.com/more",
            },
            Actions = { new ImagemapMessageAction("點了", new LineImagemapArea(0, 585, 1040, 455)) },
        };

        Assert.IsFalse(Serialize(message).GetProperty("video").TryGetProperty("externalLink", out _));
    }

    [TestMethod]
    public void ImagemapMessage_NoActions_FailsValidation()
    {
        var result = new ImagemapMessage("https://example.com/map", "地圖", 1040).Validate();

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.InvalidImagemap, result.Error.Code);
    }

    [TestMethod]
    public void ImagemapMessage_FiftyOneActions_FailsValidation()
    {
        var message = new ImagemapMessage("https://example.com/map", "地圖", 1040);
        for (var index = 0; index <= LineMessagingLimits.MaxImagemapActions; index++)
        {
            message.Actions.Add(new ImagemapMessageAction("x", new LineImagemapArea(0, 0, 1, 1)));
        }

        Assert.AreEqual(LineErrorCodes.InvalidImagemap, message.Validate().Error!.Code);
    }

    [TestMethod]
    public void ImagemapMessage_FiftyActions_Passes()
    {
        var message = new ImagemapMessage("https://example.com/map", "地圖", 1040);
        for (var index = 0; index < LineMessagingLimits.MaxImagemapActions; index++)
        {
            message.Actions.Add(new ImagemapMessageAction("x", new LineImagemapArea(0, 0, 1, 1)));
        }

        Assert.IsTrue(message.Validate().IsSuccess);
    }

    [TestMethod]
    public void ImagemapMessage_NonPositiveHeight_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ImagemapMessage("https://example.com/map", "地圖", 0));
    }

    private static JsonElement Serialize(LineMessage message) => LineMessageSerializer.ToJsonElements([message])[0];
}
