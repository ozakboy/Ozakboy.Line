using System.Text.Json;
using Ozakboy.Line.Messaging.Actions;
using Ozakboy.Line.Messaging.Messages;

namespace Ozakboy.Line.Tests;

/// <summary>
/// 從別家 LINE 套件的 bug 學到的兩類測試:特殊字元的文字必須逐字來回,以及每一種動作放進快速回覆後的外層結構。
/// Two kinds of tests learned from bugs in other LINE packages: text with awkward characters must survive a round
/// trip verbatim, and every kind of action must come out right inside a quick reply.
/// </summary>
[TestClass]
public sealed class LineMessageRoundTripTests
{
    /// <summary>
    /// 一段塞滿特殊字元的文字:單引號、雙引號、反斜線、換行、tab、代理對 emoji、結束 script 標籤。
    /// A text packed with awkward characters: single and double quotes, a backslash, newlines, a tab, a
    /// surrogate-pair emoji, and a closing script tag.
    /// </summary>
    private const string Nasty = "it's \"quoted\" back\\slash\r\nline2\nline3\ttab 😀👨\u200D👩\u200D👧 </script><script>alert(1)</script> \u2028 \u0000 end";

    /// <summary>
    /// 不跳脫非 ASCII 字元的序列化設定,讓黃金檔裡的中文可以直接比對。
    /// Serialiser options that leave non-ASCII characters unescaped, so the Chinese in the golden file compares as is.
    /// </summary>
    private static readonly JsonSerializerOptions RelaxedOptions = new(LineJson.Options)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [TestMethod]
    public void TextMessage_AwkwardCharacters_SurviveARoundTrip()
    {
        var json = JsonSerializer.Serialize<LineMessage>(new TextMessage(Nasty), LineJson.Options);

        using var document = JsonDocument.Parse(json);

        Assert.AreEqual(Nasty, document.RootElement.GetProperty("text").GetString(), "文字必須逐字相等。");
        Assert.IsFalse(json.Contains("</script>", StringComparison.Ordinal), "輸出的 JSON 不該含原樣的結束 script 標籤。");
    }

    [TestMethod]
    public void TextV2Message_AwkwardCharacters_SurviveARoundTrip()
    {
        var text = Nasty + " {who}";
        var message = new TextV2Message(text) { Substitution = { ["who"] = LineTextSubstitution.MentionAll() } };

        var json = JsonSerializer.Serialize<LineMessage>(message, LineJson.Options);

        using var document = JsonDocument.Parse(json);
        Assert.AreEqual(text, document.RootElement.GetProperty("text").GetString());
    }

    [TestMethod]
    public void OtherTextCarriers_AwkwardCharacters_SurviveARoundTrip()
    {
        var location = Roundtrip(new LocationMessage(Nasty[..30], Nasty[..40], 1, 2));
        Assert.AreEqual(Nasty[..30], location.GetProperty("title").GetString());
        Assert.AreEqual(Nasty[..40], location.GetProperty("address").GetString());

        var message = Roundtrip(new TextMessage("x") { QuickReply = new LineQuickReply { Items = { new LineQuickReplyItem(new MessageAction(Nasty)) } } });
        Assert.AreEqual(
            Nasty,
            message.GetProperty("quickReply").GetProperty("items")[0].GetProperty("action").GetProperty("text").GetString());

        var postback = Roundtrip(new TextMessage("x") { QuickReply = new LineQuickReply { Items = { new LineQuickReplyItem(new PostbackAction(Nasty, Nasty)) } } });
        var action = postback.GetProperty("quickReply").GetProperty("items")[0].GetProperty("action");
        Assert.AreEqual(Nasty, action.GetProperty("data").GetString());
        Assert.AreEqual(Nasty, action.GetProperty("displayText").GetString());
    }

    [TestMethod]
    public void QuickReply_EveryKindOfAction_KeepsTheOuterActionTypeAndTheOwnType()
    {
        using var raw = JsonDocument.Parse("""{"type":"somethingNew","label":"新","extra":1}""");
        (LineAction Action, string Type)[] cases =
        [
            (new UriAction("https://example.com") { Label = "uri" }, "uri"),
            (new MessageAction("hi") { Label = "message" }, "message"),
            (new PostbackAction("a=1", "顯示") { Label = "postback" }, "postback"),
            (new DatetimePickerAction("d", "date", "2026-10-27") { Label = "dt" }, "datetimepicker"),
            (new CameraAction { Label = "camera" }, "camera"),
            (new CameraRollAction { Label = "roll" }, "cameraRoll"),
            (new LocationAction { Label = "location" }, "location"),
            (new ClipboardAction("複製我") { Label = "clip" }, "clipboard"),
            (new RichMenuSwitchAction("alias", "d") { Label = "switch" }, "richmenuswitch"),
            (new RawAction(raw.RootElement), "somethingNew"),
        ];

        var quickReply = new LineQuickReply();
        foreach (var (action, _) in cases)
        {
            quickReply.Items.Add(new LineQuickReplyItem(action));
        }

        var json = Roundtrip(new TextMessage("選一個") { QuickReply = quickReply });
        var items = json.GetProperty("quickReply").GetProperty("items");

        Assert.AreEqual(cases.Length, items.GetArrayLength());
        for (var index = 0; index < cases.Length; index++)
        {
            Assert.AreEqual("action", items[index].GetProperty("type").GetString(), $"第 {index} 顆:外層的 type 固定是 action。");
            Assert.AreEqual(cases[index].Type, items[index].GetProperty("action").GetProperty("type").GetString(), $"第 {index} 顆:動作自己的型別。");
        }
    }

    [TestMethod]
    public void QuickReply_EveryKindOfAction_MatchesGolden()
    {
        using var raw = JsonDocument.Parse("""{"type":"somethingNew","label":"新"}""");
        var quickReply = new LineQuickReply
        {
            Items =
            {
                new LineQuickReplyItem(new UriAction("https://example.com") { Label = "a" }),
                new LineQuickReplyItem(new MessageAction("m") { Label = "b" }),
                new LineQuickReplyItem(new PostbackAction("p=1") { Label = "c" }),
                new LineQuickReplyItem(new DatetimePickerAction("d", "time", "09:30") { Label = "d" }),
                new LineQuickReplyItem(new CameraAction { Label = "e" }),
                new LineQuickReplyItem(new CameraRollAction { Label = "f" }),
                new LineQuickReplyItem(new LocationAction { Label = "g" }),
                new LineQuickReplyItem(new ClipboardAction("clip") { Label = "h" }),
                new LineQuickReplyItem(new RichMenuSwitchAction("alias", "sw") { Label = "i" }),
                new LineQuickReplyItem(new RawAction(raw.RootElement)),
            },
        };

        Assert.AreEqual(
            """{"type":"text","text":"x","quickReply":{"items":[""" +
            """{"type":"action","action":{"type":"uri","label":"a","uri":"https://example.com"}},""" +
            """{"type":"action","action":{"type":"message","label":"b","text":"m"}},""" +
            """{"type":"action","action":{"type":"postback","label":"c","data":"p=1"}},""" +
            """{"type":"action","action":{"type":"datetimepicker","label":"d","data":"d","mode":"time","initial":"09:30"}},""" +
            """{"type":"action","action":{"type":"camera","label":"e"}},""" +
            """{"type":"action","action":{"type":"cameraRoll","label":"f"}},""" +
            """{"type":"action","action":{"type":"location","label":"g"}},""" +
            """{"type":"action","action":{"type":"clipboard","label":"h","clipboardText":"clip"}},""" +
            """{"type":"action","action":{"type":"richmenuswitch","label":"i","richMenuAliasId":"alias","data":"sw"}},""" +
            """{"type":"action","action":{"type":"somethingNew","label":"新"}}""" +
            """]}}""",
            JsonSerializer.Serialize<LineMessage>(new TextMessage("x") { QuickReply = quickReply }, RelaxedOptions));
        Assert.IsTrue(new TextMessage("x") { QuickReply = quickReply }.Validate().IsSuccess, "九種動作加 RawAction 都能通過本地檢查。");
    }

    /// <summary>
    /// 把訊息序列化再解析成 JSON 元素。
    /// Serialises a message and parses it back into a JSON element.
    /// </summary>
    /// <param name="message">訊息。The message.</param>
    /// <returns>JSON 元素。The JSON element.</returns>
    private static JsonElement Roundtrip(LineMessage message)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize<LineMessage>(message, LineJson.Options));
        return document.RootElement.Clone();
    }
}
