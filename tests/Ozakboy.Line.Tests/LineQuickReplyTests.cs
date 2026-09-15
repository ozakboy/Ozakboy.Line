using System.Text.Json;
using Ozakboy.Line.Messaging;
using Ozakboy.Line.Messaging.Actions;
using Ozakboy.Line.Messaging.Messages;

namespace Ozakboy.Line.Tests;

/// <summary>
/// 快速回覆與各種動作的序列化測試。
/// Tests for serialising quick replies and each kind of action.
/// </summary>
/// <remarks>
/// 動作的欄位名寫錯,LINE 回的是一個只說「請求內容有錯」的 400,指不回是哪一個欄位。
/// 在這一層逐欄位比對,錯的時候直接知道是哪一個。
/// A wrong field name on an action costs a 400 from LINE saying only that the body is wrong, with nothing to
/// point back at the field. Comparing field by field here says which one immediately.
/// </remarks>
[TestClass]
public sealed class LineQuickReplyTests
{
    [TestMethod]
    public void QuickReply_OnTextMessage_WritesItemsArray()
    {
        var message = new TextMessage("要選哪一個?")
        {
            QuickReply = Reply(new MessageAction("好") { Label = "好" }),
        };

        var json = Serialize(message);
        var items = json.GetProperty("quickReply").GetProperty("items");

        Assert.AreEqual(1, items.GetArrayLength());
        Assert.AreEqual("action", items[0].GetProperty("type").GetString(), "外層的 type 固定是 action,不是動作自己的型別。");
        Assert.AreEqual("message", items[0].GetProperty("action").GetProperty("type").GetString());
        Assert.AreEqual("好", items[0].GetProperty("action").GetProperty("text").GetString());
        Assert.IsFalse(items[0].TryGetProperty("imageUrl", out _), "沒設定的欄位不輸出。");
    }

    [TestMethod]
    public void QuickReply_OnFlexMessage_WritesItemsArray()
    {
        using var contents = JsonDocument.Parse("""{"type":"bubble"}""");
        var message = new FlexMessage("替代文字", contents.RootElement)
        {
            QuickReply = Reply(new PostbackAction("a=1")),
        };

        var json = Serialize(message);

        Assert.AreEqual("flex", json.GetProperty("type").GetString());
        Assert.AreEqual(1, json.GetProperty("quickReply").GetProperty("items").GetArrayLength());
    }

    [TestMethod]
    public void QuickReplyItem_WithImageUrl_WritesIt()
    {
        var message = new TextMessage("嗨")
        {
            QuickReply = new LineQuickReply
            {
                Items = { new LineQuickReplyItem(new MessageAction("好")) { ImageUrl = "https://example.com/i.png" } },
            },
        };

        var item = Serialize(message).GetProperty("quickReply").GetProperty("items")[0];

        Assert.AreEqual("https://example.com/i.png", item.GetProperty("imageUrl").GetString());
    }

    [TestMethod]
    public void QuickReply_Empty_IsNotWritten()
    {
        var message = new TextMessage("嗨") { QuickReply = new LineQuickReply() };

        Assert.IsFalse(Serialize(message).TryGetProperty("quickReply", out _), "空的按鈕列不輸出,LINE 會退回整則訊息。");
    }

    [TestMethod]
    public void Validate_ThirteenItems_Passes()
    {
        var quickReply = new LineQuickReply();
        for (var index = 0; index < LineMessagingLimits.MaxQuickReplyItems; index++)
        {
            quickReply.Items.Add(new LineQuickReplyItem(new MessageAction($"選項 {index}")));
        }

        Assert.IsTrue(quickReply.Validate().IsSuccess, "13 個是上限,不是超過上限。");
    }

    [TestMethod]
    public void Validate_FourteenItems_Fails()
    {
        var quickReply = new LineQuickReply();
        for (var index = 0; index <= LineMessagingLimits.MaxQuickReplyItems; index++)
        {
            quickReply.Items.Add(new LineQuickReplyItem(new MessageAction($"選項 {index}")));
        }

        var result = quickReply.Validate();

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.TooManyQuickReplyItems, result.Error!.Code);
        StringAssert.Contains(result.Error.Message, "14", "訊息要說出實際數量,不能只說超過上限。");
    }

    [TestMethod]
    public void Validate_NoItems_Fails()
    {
        // 掛一個空的快速回覆幾乎都是迴圈沒跑到或條件寫反了,靜靜送出一則沒有按鈕的訊息只會讓那個 bug 活更久。
        var result = new LineQuickReply().Validate();

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.TooManyQuickReplyItems, result.Error!.Code);
    }

    [TestMethod]
    public void DatetimePickerAction_WritesEveryField()
    {
        var json = SerializeAction(new DatetimePickerAction(
            "when",
            DatetimePickerAction.ModeDatetime,
            initial: "2026-09-15T10:00",
            max: "2026-12-31T23:59",
            min: "2026-01-01T00:00")
        {
            Label = "選時間",
        });

        Assert.AreEqual("datetimepicker", json.GetProperty("type").GetString());
        Assert.AreEqual("選時間", json.GetProperty("label").GetString());
        Assert.AreEqual("when", json.GetProperty("data").GetString());
        Assert.AreEqual("datetime", json.GetProperty("mode").GetString());
        Assert.AreEqual("2026-09-15T10:00", json.GetProperty("initial").GetString());
        Assert.AreEqual("2026-12-31T23:59", json.GetProperty("max").GetString());
        Assert.AreEqual("2026-01-01T00:00", json.GetProperty("min").GetString());
    }

    [TestMethod]
    public void DatetimePickerAction_OmitsUnsetOptionalFields()
    {
        var json = SerializeAction(new DatetimePickerAction("when", DatetimePickerAction.ModeDate));

        Assert.IsFalse(json.TryGetProperty("initial", out _));
        Assert.IsFalse(json.TryGetProperty("max", out _));
        Assert.IsFalse(json.TryGetProperty("min", out _));
    }

    [TestMethod]
    public void DatetimePickerAction_UnknownMode_Throws()
    {
        // 模式字串打錯在 JSON 上看不出問題,只有 LINE 認得出來 —— 所以在建構時就擋。
        Assert.ThrowsExactly<ArgumentException>(() => new DatetimePickerAction("when", "dateTime"));
    }

    [TestMethod]
    public void CameraCameraRollAndLocationActions_WriteOnlyTypeAndLabel()
    {
        Assert.AreEqual("camera", SerializeAction(new CameraAction()).GetProperty("type").GetString());
        Assert.AreEqual("cameraRoll", SerializeAction(new CameraRollAction()).GetProperty("type").GetString());
        Assert.AreEqual("location", SerializeAction(new LocationAction()).GetProperty("type").GetString());

        var camera = SerializeAction(new CameraAction { Label = "拍照" });
        Assert.AreEqual("拍照", camera.GetProperty("label").GetString());
        Assert.AreEqual(2, camera.EnumerateObject().Count(), "這三種動作沒有專屬欄位。");
    }

    [TestMethod]
    public void ClipboardAction_WritesClipboardText()
    {
        var json = SerializeAction(new ClipboardAction("ABC-123"));

        Assert.AreEqual("clipboard", json.GetProperty("type").GetString());
        Assert.AreEqual("ABC-123", json.GetProperty("clipboardText").GetString());
    }

    [TestMethod]
    public void RichMenuSwitchAction_WritesAliasAndData()
    {
        var json = SerializeAction(new RichMenuSwitchAction("menu-b", "switch=b"));

        Assert.AreEqual("richmenuswitch", json.GetProperty("type").GetString());
        Assert.AreEqual("menu-b", json.GetProperty("richMenuAliasId").GetString());
        Assert.AreEqual("switch=b", json.GetProperty("data").GetString());
    }

    [TestMethod]
    public void UriAction_WithAltUriDesktop_WritesNestedObject()
    {
        // LINE 的欄位是巢狀的 altUri.desktop,不是平的 altUriDesktop。
        var json = SerializeAction(new UriAction("https://liff.line.me/x") { AltUriDesktop = "https://example.com/x" });

        Assert.AreEqual("https://liff.line.me/x", json.GetProperty("uri").GetString());
        Assert.AreEqual("https://example.com/x", json.GetProperty("altUri").GetProperty("desktop").GetString());
    }

    [TestMethod]
    public void UriAction_WithoutAltUriDesktop_OmitsIt()
    {
        Assert.IsFalse(SerializeAction(new UriAction("https://example.com")).TryGetProperty("altUri", out _));
    }

    [TestMethod]
    public void PostbackAction_WithInputOptionAndFillInText_WritesBoth()
    {
        var json = SerializeAction(new PostbackAction("a=1", "已選擇")
        {
            InputOption = PostbackAction.InputOptionOpenKeyboard,
            FillInText = "我要訂 ",
        });

        Assert.AreEqual("a=1", json.GetProperty("data").GetString());
        Assert.AreEqual("已選擇", json.GetProperty("displayText").GetString());
        Assert.AreEqual("openKeyboard", json.GetProperty("inputOption").GetString());
        Assert.AreEqual("我要訂 ", json.GetProperty("fillInText").GetString());
    }

    [TestMethod]
    public void PostbackAction_WithoutInputOption_OmitsBothFields()
    {
        var json = SerializeAction(new PostbackAction("a=1"));

        Assert.IsFalse(json.TryGetProperty("inputOption", out _));
        Assert.IsFalse(json.TryGetProperty("fillInText", out _));
    }

    /// <summary>
    /// 建立一個只有一顆按鈕的快速回覆。
    /// Builds a quick reply with one button.
    /// </summary>
    /// <param name="action">按鈕的動作。The button's action.</param>
    /// <returns>快速回覆。The quick reply.</returns>
    private static LineQuickReply Reply(LineAction action) => new() { Items = { new LineQuickReplyItem(action) } };

    /// <summary>
    /// 把訊息序列化成 JSON 元素。
    /// Serialises a message into a JSON element.
    /// </summary>
    /// <param name="message">訊息。The message.</param>
    /// <returns>JSON 元素。The JSON element.</returns>
    private static JsonElement Serialize(LineMessage message)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(message, LineJson.Options));
        return document.RootElement.Clone();
    }

    /// <summary>
    /// 把動作序列化成 JSON 元素。
    /// Serialises an action into a JSON element.
    /// </summary>
    /// <param name="action">動作。The action.</param>
    /// <returns>JSON 元素。The JSON element.</returns>
    private static JsonElement SerializeAction(LineAction action)
    {
        var message = new TextMessage("x") { QuickReply = Reply(action) };
        return Serialize(message).GetProperty("quickReply").GetProperty("items")[0].GetProperty("action").Clone();
    }
}
