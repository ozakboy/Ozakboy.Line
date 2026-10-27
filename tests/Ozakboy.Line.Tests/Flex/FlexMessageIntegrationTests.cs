using System.Text.Json;
using Ozakboy.Line.Messaging;
using Ozakboy.Line.Messaging.Messages;
using Ozakboy.Line.Messaging.Messages.Flex;

namespace Ozakboy.Line.Tests.Flex;

/// <summary>
/// <see cref="FlexMessage"/> 的兩種建構路徑(強型別容器與原樣 JSON)必須輸出相同,且既有檢查不退步。
/// <see cref="FlexMessage"/>'s two construction paths (typed container and verbatim JSON) must write the same, and
/// the existing checks must not regress.
/// </summary>
[TestClass]
public sealed class FlexMessageIntegrationTests
{
    private const string SampleJson = """{"type":"bubble","body":{"type":"box","layout":"vertical","contents":[{"type":"text","text":"Hello","weight":"bold"}]}}""";

    [TestMethod]
    public void TypedAndJsonPaths_WriteTheSameMessage()
    {
        var typed = new FlexMessage("alt", new FlexBubble
        {
            Body = new FlexBox
            {
                Layout = FlexLayout.Vertical,
                Contents = [new FlexText("Hello") { Weight = FlexWeight.Bold }],
            },
        });

        using var document = JsonDocument.Parse(SampleJson);
        var fromElement = new FlexMessage("alt", document.RootElement);
        var fromJson = FlexMessage.FromJson("alt", SampleJson).GetValueOrThrow();
        var parsed = FlexMessage.Parse("alt", SampleJson).GetValueOrThrow();

        var expected = JsonSerializer.Serialize<LineMessage>(typed);

        Assert.AreEqual("""{"type":"flex","altText":"alt","contents":""" + SampleJson + "}", expected);
        Assert.AreEqual(expected, JsonSerializer.Serialize<LineMessage>(fromElement));
        Assert.AreEqual(expected, JsonSerializer.Serialize<LineMessage>(fromJson));
        Assert.AreEqual(expected, JsonSerializer.Serialize<LineMessage>(parsed));
    }

    [TestMethod]
    public void ContainerAndContents_AreMutuallyExclusive()
    {
        var typed = new FlexMessage("alt", new FlexBubble());
        var raw = FlexMessage.FromJson("alt", SampleJson).GetValueOrThrow();

        Assert.IsNotNull(typed.Container);
        Assert.AreEqual(JsonValueKind.Undefined, typed.Contents.ValueKind);
        Assert.IsNull(raw.Container);
        Assert.AreEqual(JsonValueKind.Object, raw.Contents.ValueKind);
        Assert.IsNotNull(FlexMessage.Parse("alt", SampleJson).GetValueOrThrow().Container);
    }

    [TestMethod]
    public void TypedMessage_WritesQuickReplyAndSender()
    {
        var message = new FlexMessage("alt", new FlexBubble())
        {
            QuickReply = new LineQuickReply { Items = { new LineQuickReplyItem(new Ozakboy.Line.Messaging.Actions.MessageAction("好")) } },
            Sender = new LineMessageSender { Name = "客服" },
        };

        var json = JsonSerializer.Serialize<LineMessage>(message);

        StringAssert.Contains(json, "\"quickReply\"");
        StringAssert.Contains(json, "\"sender\"");
        StringAssert.Contains(json, "\"contents\":{\"type\":\"bubble\"}");
    }

    [TestMethod]
    public void Validate_AltTextLimit_StillAppliesToBothPaths()
    {
        var tooLong = new string('x', LineMessagingLimits.MaxAltTextLength + 1);

        var bubble = new FlexBubble { Body = new FlexBox { Layout = FlexLayout.Vertical, Contents = [new FlexText("x")] } };
        var typedFailure = new FlexMessage(tooLong, bubble).Validate();
        var typedOk = new FlexMessage(new string('x', LineMessagingLimits.MaxAltTextLength), bubble).Validate();
        var jsonFailure = FlexMessage.FromJson(tooLong, SampleJson).GetValueOrThrow().Validate();

        Assert.IsTrue(typedFailure.IsFailure);
        Assert.IsNotNull(typedFailure.Error);
        Assert.AreEqual(LineErrorCodes.InvalidMessage, typedFailure.Error.Code);
        Assert.IsTrue(typedOk.IsSuccess);
        Assert.IsTrue(jsonFailure.IsFailure);
    }

    [TestMethod]
    public void Constructor_RejectsBlankAltTextAndNullContainer()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new FlexMessage(" ", new FlexBubble()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new FlexMessage("alt", (FlexContainer)null!));
    }

    [TestMethod]
    public void ComponentConstructors_RejectBlankRequiredValues()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new FlexText(string.Empty));
        Assert.ThrowsExactly<ArgumentException>(() => new FlexSpan(string.Empty));
        Assert.ThrowsExactly<ArgumentException>(() => new FlexImage(" "));
        Assert.ThrowsExactly<ArgumentException>(() => new FlexIcon(" "));
        Assert.ThrowsExactly<ArgumentNullException>(() => new FlexButton(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new FlexVideo("https://e.com/v.mp4", "https://e.com/p.png", null!));
    }
}
