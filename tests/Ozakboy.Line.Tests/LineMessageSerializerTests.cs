using System.Text.Json;
using Ozakboy.Line.Messaging.Messages;

namespace Ozakboy.Line.Tests;

/// <summary>
/// 訊息序列化的測試:每一種訊息型別輸出的欄位必須符合 LINE 規格。
/// Tests for message serialisation: every message type must write the fields LINE's specification asks for.
/// </summary>
/// <remarks>
/// 欄位名寫錯的代價是一個只說「請求內容有錯」的 400,而那種錯誤在整合測試裡很難指回是哪個欄位。
/// 在這一層逐欄位比對,錯的時候直接就知道是哪一個。
/// A wrong field name costs a 400 that says only that the body is wrong, which an integration test cannot point
/// back at a field. Comparing field by field here says which one immediately.
/// </remarks>
[TestClass]
public sealed class LineMessageSerializerTests
{
    [TestMethod]
    public void TextMessage_WritesTypeAndText()
    {
        var json = Serialize(new TextMessage("哈囉"));

        Assert.AreEqual("text", json.GetProperty("type").GetString());
        Assert.AreEqual("哈囉", json.GetProperty("text").GetString());
        Assert.IsFalse(json.TryGetProperty("quoteToken", out _), "沒有設定的欄位不輸出。");
        Assert.IsFalse(json.TryGetProperty("quickReply", out _));
    }

    [TestMethod]
    public void TextMessage_WithQuoteToken_WritesQuoteToken()
    {
        var json = Serialize(new TextMessage("哈囉") { QuoteToken = "qt-1" });

        Assert.AreEqual("qt-1", json.GetProperty("quoteToken").GetString());
    }

    [TestMethod]
    public void ImageMessage_WritesBothUrls()
    {
        var json = Serialize(new ImageMessage("https://example.com/o.jpg", "https://example.com/p.jpg"));

        Assert.AreEqual("image", json.GetProperty("type").GetString());
        Assert.AreEqual("https://example.com/o.jpg", json.GetProperty("originalContentUrl").GetString());
        Assert.AreEqual("https://example.com/p.jpg", json.GetProperty("previewImageUrl").GetString());
    }

    [TestMethod]
    public void VideoMessage_WritesBothUrls()
    {
        var json = Serialize(new VideoMessage("https://example.com/o.mp4", "https://example.com/p.jpg"));

        Assert.AreEqual("video", json.GetProperty("type").GetString());
        Assert.AreEqual("https://example.com/o.mp4", json.GetProperty("originalContentUrl").GetString());
        Assert.AreEqual("https://example.com/p.jpg", json.GetProperty("previewImageUrl").GetString());
    }

    [TestMethod]
    public void AudioMessage_WritesDurationAsNumberNamedDuration()
    {
        // 屬性叫 DurationMilliseconds(讓呼叫端知道單位),但 LINE 的欄位名是 duration。
        // 靠反射轉名字會送出 durationMilliseconds,而 LINE 只會回一個沒指名欄位的 400。
        var json = Serialize(new AudioMessage("https://example.com/a.m4a", 60000));

        Assert.AreEqual("audio", json.GetProperty("type").GetString());
        Assert.AreEqual(60000, json.GetProperty("duration").GetInt32());
        Assert.IsFalse(json.TryGetProperty("durationMilliseconds", out _));
    }

    [TestMethod]
    public void LocationMessage_WritesCoordinatesAsNumbers()
    {
        var json = Serialize(new LocationMessage("台北 101", "台北市信義區", 25.0339, 121.5645));

        Assert.AreEqual("location", json.GetProperty("type").GetString());
        Assert.AreEqual("台北 101", json.GetProperty("title").GetString());
        Assert.AreEqual("台北市信義區", json.GetProperty("address").GetString());
        Assert.AreEqual(25.0339, json.GetProperty("latitude").GetDouble());
        Assert.AreEqual(121.5645, json.GetProperty("longitude").GetDouble());
    }

    [TestMethod]
    public void StickerMessage_WritesPackageAndStickerIds()
    {
        var json = Serialize(new StickerMessage("446", "1988"));

        Assert.AreEqual("sticker", json.GetProperty("type").GetString());
        Assert.AreEqual("446", json.GetProperty("packageId").GetString());
        Assert.AreEqual("1988", json.GetProperty("stickerId").GetString());
    }

    [TestMethod]
    public void FlexMessage_WritesAltTextAndContentsVerbatim()
    {
        using var contents = JsonDocument.Parse("""{"type":"bubble","body":{"type":"box","layout":"vertical"}}""");
        var json = Serialize(new FlexMessage("看不到 Flex 時顯示這句", contents.RootElement));

        Assert.AreEqual("flex", json.GetProperty("type").GetString());
        Assert.AreEqual("看不到 Flex 時顯示這句", json.GetProperty("altText").GetString());
        Assert.AreEqual("bubble", json.GetProperty("contents").GetProperty("type").GetString());
        Assert.AreEqual("vertical", json.GetProperty("contents").GetProperty("body").GetProperty("layout").GetString());
    }

    [TestMethod]
    public void FlexMessage_FromJson_InvalidJson_Fails()
    {
        var result = FlexMessage.FromJson("替代文字", "{not json");

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.InvalidJson, result.Error.Code);
    }

    [TestMethod]
    public void FlexMessage_SurvivesTheSourceDocumentBeingDisposed()
    {
        // JsonElement 的生命週期綁在產生它的 JsonDocument 上。沒有 Clone 的話,
        // 這裡的序列化會擲出 ObjectDisposedException,而那會發生在送出訊息的當下。
        FlexMessage message;
        using (var contents = JsonDocument.Parse("""{"type":"bubble"}"""))
        {
            message = new FlexMessage("替代文字", contents.RootElement);
        }

        var json = Serialize(message);

        Assert.AreEqual("bubble", json.GetProperty("contents").GetProperty("type").GetString());
    }

    [TestMethod]
    public void RawMessage_WritesTheObjectVerbatim()
    {
        using var contents = JsonDocument.Parse("""{"type":"imagemap","baseUrl":"https://example.com/map","altText":"地圖"}""");
        var message = new RawMessage(contents.RootElement);

        var json = Serialize(message);

        Assert.AreEqual("imagemap", message.Type, "型別字串直接從 JSON 讀出來。");
        Assert.AreEqual("imagemap", json.GetProperty("type").GetString());
        Assert.AreEqual("https://example.com/map", json.GetProperty("baseUrl").GetString());
        Assert.AreEqual("地圖", json.GetProperty("altText").GetString());
    }

    [TestMethod]
    public void RawMessage_WithoutTypeField_Throws()
    {
        using var contents = JsonDocument.Parse("""{"text":"沒有 type"}""");

        Assert.ThrowsExactly<ArgumentException>(() => new RawMessage(contents.RootElement));
    }

    [TestMethod]
    public void QuickReply_IsWrittenWhenSet()
    {
        // 快速回覆的完整測試在 LineQuickReplyTests;這裡只確認它掛在訊息上時會被寫出來。
        var quickReply = new Messaging.LineQuickReply
        {
            Items = { new Messaging.LineQuickReplyItem(new Messaging.Actions.MessageAction("好") { Label = "好" }) },
        };

        var json = Serialize(new TextMessage("要嗎?") { QuickReply = quickReply });

        Assert.AreEqual(
            "message",
            json.GetProperty("quickReply").GetProperty("items")[0].GetProperty("action").GetProperty("type").GetString());
    }

    [TestMethod]
    public void Sender_IsWrittenWhenSet()
    {
        var json = Serialize(new TextMessage("哈囉")
        {
            Sender = new Messaging.LineMessageSender { Name = "客服小幫手", IconUrl = "https://example.com/icon.png" },
        });

        Assert.AreEqual("客服小幫手", json.GetProperty("sender").GetProperty("name").GetString());
        Assert.AreEqual("https://example.com/icon.png", json.GetProperty("sender").GetProperty("iconUrl").GetString());
    }

    [TestMethod]
    public void Sender_OnlyName_OmitsIconUrl()
    {
        var json = Serialize(new StickerMessage("446", "1988") { Sender = new Messaging.LineMessageSender { Name = "小幫手" } });

        Assert.AreEqual("小幫手", json.GetProperty("sender").GetProperty("name").GetString());
        Assert.IsFalse(json.GetProperty("sender").TryGetProperty("iconUrl", out _));
    }

    [TestMethod]
    public void Sender_Empty_IsNotWritten()
    {
        // 兩個欄位都空的 sender 對 LINE 沒有意義,不寫出。
        var json = Serialize(new TextMessage("哈囉") { Sender = new Messaging.LineMessageSender() });

        Assert.IsFalse(json.TryGetProperty("sender", out _));
    }

    [TestMethod]
    public void RawMessage_IgnoresSenderAndQuickReply()
    {
        // 原樣訊息整份由呼叫端提供,再寫一次 sender 會產生重複欄位。
        using var contents = JsonDocument.Parse("""{"type":"text","text":"原樣"}""");
        var json = Serialize(new RawMessage(contents.RootElement)
        {
            Sender = new Messaging.LineMessageSender { Name = "小幫手" },
        });

        Assert.IsFalse(json.TryGetProperty("sender", out _));
    }

    [TestMethod]
    public void VideoMessage_WithTrackingId_WritesTrackingId()
    {
        var json = Serialize(new VideoMessage("https://example.com/o.mp4", "https://example.com/p.jpg") { TrackingId = "track-1" });

        Assert.AreEqual("track-1", json.GetProperty("trackingId").GetString());
    }

    [TestMethod]
    public void VideoMessage_WithoutTrackingId_OmitsTrackingId()
    {
        var json = Serialize(new VideoMessage("https://example.com/o.mp4", "https://example.com/p.jpg"));

        Assert.IsFalse(json.TryGetProperty("trackingId", out _));
    }

    [TestMethod]
    public void WriteMessages_ProducesOneElementPerMessage()
    {
        var elements = LineMessageSerializer.ToJsonElements([new TextMessage("一"), new StickerMessage("446", "1988")]);

        Assert.AreEqual(2, elements.Length);
        Assert.AreEqual("text", elements[0].GetProperty("type").GetString());
        Assert.AreEqual("sticker", elements[1].GetProperty("type").GetString());
    }

    /// <summary>
    /// 把一則訊息序列化成 JSON 元素。
    /// Serialises one message into a JSON element.
    /// </summary>
    /// <param name="message">訊息。The message.</param>
    /// <returns>序列化結果。The serialised element.</returns>
    private static JsonElement Serialize(LineMessage message) => LineMessageSerializer.ToJsonElements([message])[0];
}
