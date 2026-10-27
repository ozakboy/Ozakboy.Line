using System.Text.Json;
using Ozakboy.Line.Messaging.Actions;
using Ozakboy.Line.Messaging.Messages;
using Ozakboy.Line.Messaging.Messages.Flex;

namespace Ozakboy.Line.Tests.Flex;

/// <summary>
/// 讀回的測試:依 <c>type</c> 分派、未知節點原樣保留、讀回再寫出逐位元組相同。
/// Reading tests: dispatch on <c>type</c>, unknown nodes kept verbatim, and read-then-write is byte-identical.
/// </summary>
[TestClass]
public sealed class FlexRoundTripTests
{
    [TestMethod]
    public void Samples_ParseThenWrite_AreByteIdentical()
    {
        string[] names = ["flex-restaurant", "flex-receipt", "flex-carousel", "flex-components"];

        foreach (var name in names)
        {
            var canonical = FlexTestSupport.Sample(name);
            var parsed = FlexMessage.Parse("alt", canonical);

            Assert.IsTrue(parsed.IsSuccess, name);
            Assert.IsNotNull(parsed.GetValueOrThrow().Container, name);
            Assert.AreEqual(canonical, FlexTestSupport.Write(parsed.GetValueOrThrow().Container!), name);
        }
    }

    [TestMethod]
    public void Samples_AreFullyTyped_ExceptTheSpacer()
    {
        foreach (var name in new[] { "flex-restaurant", "flex-receipt", "flex-carousel" })
        {
            var container = FlexMessage.Parse("alt", FlexTestSupport.Sample(name)).GetValueOrThrow().Container!;

            Assert.IsFalse(container is RawFlexContainer, name);
            Assert.IsFalse(FlexTestSupport.Components(container).Any(component => component is RawFlexComponent), name);
        }

        var components = FlexMessage.Parse("alt", FlexTestSupport.Sample("flex-components")).GetValueOrThrow().Container!;
        var raws = FlexTestSupport.Components(components).OfType<RawFlexComponent>().ToList();

        Assert.AreEqual(1, raws.Count);
        Assert.AreEqual("spacer", raws[0].Type);
    }

    [TestMethod]
    public void Restaurant_ParsesIntoTheExpectedTypes()
    {
        var bubble = (FlexBubble)FlexMessage.Parse("alt", FlexTestSupport.Sample("flex-restaurant")).GetValueOrThrow().Container!;

        Assert.IsInstanceOfType<FlexImage>(bubble.Hero);
        Assert.AreEqual("full", ((FlexImage)bubble.Hero!).Size);
        Assert.AreEqual(FlexLayout.Vertical, bubble.Body!.Layout);
        Assert.AreEqual(3, bubble.Body.Contents.Count);
        Assert.IsInstanceOfType<FlexText>(bubble.Body.Contents[0]);
        Assert.AreEqual("Brown Cafe", ((FlexText)bubble.Body.Contents[0]).Text);
        Assert.AreEqual(3, bubble.Footer!.Contents.Count);
        Assert.IsInstanceOfType<FlexButton>(bubble.Footer.Contents[0]);
        Assert.IsNull(bubble.Header);
    }

    [TestMethod]
    public void Carousel_ParsesIntoThreeBubbles()
    {
        var carousel = (FlexCarousel)FlexMessage.Parse("alt", FlexTestSupport.Sample("flex-carousel")).GetValueOrThrow().Container!;

        Assert.AreEqual(3, carousel.Contents.Count);
        Assert.AreEqual(FlexBubbleSize.Micro, carousel.Contents[2].Size);
    }

    [TestMethod]
    public void Action_ReadsBackAsRawAction_AndKeepsAllFields()
    {
        var bubble = (FlexBubble)FlexMessage.Parse("alt", FlexTestSupport.Sample("flex-components")).GetValueOrThrow().Container!;

        Assert.IsInstanceOfType<RawAction>(bubble.Action);
        Assert.AreEqual("uri", bubble.Action!.Type);
        Assert.AreEqual("Bubble", bubble.Action.Label);
        Assert.IsNotNull(bubble.Header!.Background);
        Assert.AreEqual("50%", bubble.Header.Background.CenterPosition);
    }

    [TestMethod]
    public void Video_ReadsBackAsUriAction()
    {
        var bubble = (FlexBubble)FlexMessage.Parse("alt", FlexTestSupport.Sample("flex-components")).GetValueOrThrow().Container!;
        var video = (FlexVideo)bubble.Hero!;

        Assert.AreEqual("More", video.Action!.Label);
        Assert.AreEqual("https://example.com/more", video.Action.Uri);
        Assert.IsInstanceOfType<FlexImage>(video.AltContent);
    }

    [TestMethod]
    public void UnknownContainerType_BecomesRawContainer_AndRoundTrips()
    {
        const string Json = """{"type":"future","x":[1,2,{"y":null}]}""";

        var container = FlexMessage.Parse("alt", Json).GetValueOrThrow().Container;

        Assert.IsInstanceOfType<RawFlexContainer>(container);
        Assert.AreEqual("future", container!.Type);
        Assert.AreEqual(Json, FlexTestSupport.Write(container));
    }

    [TestMethod]
    public void UnknownComponentType_InsideBox_BecomesRawComponent_AndRoundTrips()
    {
        const string Json = """{"type":"bubble","body":{"type":"box","layout":"vertical","contents":[{"type":"text","text":"a"},{"type":"hologram","depth":3},{"type":"text","text":"b"}]}}""";

        var bubble = (FlexBubble)FlexMessage.Parse("alt", Json).GetValueOrThrow().Container!;

        Assert.IsInstanceOfType<FlexText>(bubble.Body!.Contents[0]);
        Assert.IsInstanceOfType<RawFlexComponent>(bubble.Body.Contents[1]);
        Assert.IsInstanceOfType<FlexText>(bubble.Body.Contents[2]);
        Assert.AreEqual(Json, FlexTestSupport.Write(bubble));
    }

    [TestMethod]
    public void UnmodelledFieldOnComponent_KeepsThatComponentVerbatim_NotDropped()
    {
        const string Json = """{"type":"bubble","body":{"type":"box","layout":"vertical","contents":[{"type":"text","text":"a","newField":"keep me"},{"type":"text","text":"b"}]}}""";

        var bubble = (FlexBubble)FlexMessage.Parse("alt", Json).GetValueOrThrow().Container!;

        Assert.IsInstanceOfType<RawFlexComponent>(bubble.Body!.Contents[0]);
        Assert.IsInstanceOfType<FlexText>(bubble.Body.Contents[1]);
        Assert.AreEqual(Json, FlexTestSupport.Write(bubble));
    }

    [TestMethod]
    public void WrongValueKind_KeepsThatComponentVerbatim()
    {
        const string Json = """{"type":"bubble","body":{"type":"box","layout":"vertical","contents":[{"type":"text","text":"a","flex":"1"}]}}""";

        var bubble = (FlexBubble)FlexMessage.Parse("alt", Json).GetValueOrThrow().Container!;

        Assert.IsInstanceOfType<RawFlexComponent>(bubble.Body!.Contents[0]);
        Assert.AreEqual(Json, FlexTestSupport.Write(bubble));
    }

    [TestMethod]
    public void UnmodelledFieldOnBodyBox_FallsBackToRawBubble()
    {
        const string Json = """{"type":"bubble","body":{"type":"box","layout":"vertical","contents":[],"shadow":"md"}}""";

        var container = FlexMessage.Parse("alt", Json).GetValueOrThrow().Container;

        Assert.IsInstanceOfType<RawFlexContainer>(container);
        Assert.AreEqual(Json, FlexTestSupport.Write(container!));
    }

    [TestMethod]
    public void UnmodelledFieldOnOneBubble_FallsBackToRawCarousel()
    {
        const string Json = """{"type":"carousel","contents":[{"type":"bubble"},{"type":"bubble","brandNew":true}]}""";

        var container = FlexMessage.Parse("alt", Json).GetValueOrThrow().Container;

        Assert.IsInstanceOfType<RawFlexContainer>(container);
        Assert.AreEqual(Json, FlexTestSupport.Write(container!));
    }

    [TestMethod]
    public void BoxWithoutLayout_InsideContents_BecomesRawComponent()
    {
        const string Json = """{"type":"bubble","body":{"type":"box","layout":"vertical","contents":[{"type":"box","contents":[]}]}}""";

        var bubble = (FlexBubble)FlexMessage.Parse("alt", Json).GetValueOrThrow().Container!;

        Assert.IsInstanceOfType<RawFlexComponent>(bubble.Body!.Contents[0]);
    }

    [TestMethod]
    public void ButtonWithoutAction_BecomesRawComponent()
    {
        const string Json = """{"type":"bubble","footer":{"type":"box","layout":"vertical","contents":[{"type":"button"}]}}""";

        var bubble = (FlexBubble)FlexMessage.Parse("alt", Json).GetValueOrThrow().Container!;

        Assert.IsInstanceOfType<RawFlexComponent>(bubble.Footer!.Contents[0]);
    }

    [TestMethod]
    public void VideoWithNonUriAction_BecomesRawComponent_AndRoundTrips()
    {
        const string Json = """{"type":"bubble","hero":{"type":"video","url":"https://e.com/v.mp4","previewUrl":"https://e.com/p.png","altContent":{"type":"image","url":"https://e.com/a.png"},"action":{"type":"message","text":"x"}}}""";

        var bubble = (FlexBubble)FlexMessage.Parse("alt", Json).GetValueOrThrow().Container!;

        Assert.IsInstanceOfType<RawFlexComponent>(bubble.Hero);
        Assert.AreEqual(Json, FlexTestSupport.Write(bubble));
    }

    [TestMethod]
    public void NullValues_AreTreatedAsAbsent()
    {
        var bubble = (FlexBubble)FlexMessage.Parse("alt", """{"type":"bubble","size":null,"body":null}""").GetValueOrThrow().Container!;

        Assert.IsNull(bubble.Size);
        Assert.IsNull(bubble.Body);
        Assert.AreEqual("""{"type":"bubble"}""", FlexTestSupport.Write(bubble));
    }

    [TestMethod]
    public void Parse_InvalidJson_FailsWithInvalidJsonCode()
    {
        var result = FlexMessage.Parse("alt", "{not json");

        Assert.IsTrue(result.IsFailure);
        Assert.IsNotNull(result.Error);
        Assert.AreEqual(LineErrorCodes.InvalidJson, result.Error.Code);
    }

    [TestMethod]
    public void Parse_StructuralProblems_FailWithInvalidJsonCode()
    {
        string[] inputs =
        [
            "[]",
            """{"layout":"vertical"}""",
            """{"type":3}""",
            """{"type":"bubble","body":{"type":"box","layout":"vertical","contents":[1]}}""",
            """{"type":"bubble","body":{"type":"box","layout":"vertical","contents":[{"text":"no type"}]}}""",
        ];

        foreach (var input in inputs)
        {
            var result = FlexMessage.Parse("alt", input);

            Assert.IsTrue(result.IsFailure, input);
            Assert.IsNotNull(result.Error, input);
            Assert.AreEqual(LineErrorCodes.InvalidJson, result.Error.Code, input);
        }
    }

    [TestMethod]
    public void Parse_BlankArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentException>(() => FlexMessage.Parse(" ", "{}"));
        Assert.ThrowsExactly<ArgumentException>(() => FlexMessage.Parse("alt", " "));
    }

    [TestMethod]
    public void JsonSerializer_DeserialisesByBaseAndConcreteType()
    {
        var json = FlexTestSupport.Sample("flex-restaurant");

        var asBase = JsonSerializer.Deserialize<FlexContainer>(json);
        var asBubble = JsonSerializer.Deserialize<FlexBubble>(json);

        Assert.IsInstanceOfType<FlexBubble>(asBase);
        Assert.IsNotNull(asBubble);
        Assert.AreEqual(json, JsonSerializer.Serialize(asBubble));
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<FlexCarousel>(json));
    }

    [TestMethod]
    public void JsonSerializer_DeserialisesComponentsAndHelpers()
    {
        var box = JsonSerializer.Deserialize<FlexBox>("""{"type":"box","layout":"horizontal","contents":[{"type":"filler"}]}""");
        var span = JsonSerializer.Deserialize<FlexSpan>("""{"type":"span","text":"a","weight":"bold"}""");
        var gradient = JsonSerializer.Deserialize<FlexLinearGradient>("""{"type":"linearGradient","angle":"0deg","startColor":"#000000","endColor":"#FFFFFF"}""");
        var styles = JsonSerializer.Deserialize<FlexBubbleStyles>("""{"body":{"separator":true}}""");

        Assert.IsNotNull(box);
        Assert.IsInstanceOfType<FlexFiller>(box.Contents[0]);
        Assert.AreEqual("a", span!.Text);
        Assert.AreEqual("0deg", gradient!.Angle);
        Assert.IsTrue(styles!.Body!.Separator);
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<FlexSpan>("""{"type":"span","text":"a","extra":1}"""));
    }

    [TestMethod]
    public void RawNodes_AreCloned_SoTheyOutliveTheirDocument()
    {
        RawFlexComponent raw;
        using (var document = JsonDocument.Parse("""{"type":"spacer","size":"md"}"""))
        {
            raw = new RawFlexComponent(document.RootElement);
        }

        Assert.AreEqual("spacer", raw.Type);
        Assert.AreEqual("""{"type":"spacer","size":"md"}""", raw.Contents.GetRawText());
    }

    [TestMethod]
    public void RawNodes_RejectNonObjectsAndMissingType()
    {
        using var array = JsonDocument.Parse("[]");
        using var noType = JsonDocument.Parse("{}");

        Assert.ThrowsExactly<ArgumentException>(() => new RawFlexComponent(array.RootElement));
        Assert.ThrowsExactly<ArgumentException>(() => new RawFlexComponent(noType.RootElement));
        Assert.ThrowsExactly<ArgumentException>(() => new RawFlexContainer(array.RootElement));
        Assert.ThrowsExactly<ArgumentException>(() => new RawFlexContainer(noType.RootElement));
    }
}
