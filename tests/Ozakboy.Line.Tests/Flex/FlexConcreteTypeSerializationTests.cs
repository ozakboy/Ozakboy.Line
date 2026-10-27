using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ozakboy.Line.Messaging.Actions;
using Ozakboy.Line.Messaging.Messages.Flex;

namespace Ozakboy.Line.Tests.Flex;

/// <summary>
/// 以具體型別序列化時,輸出必須與以基底型別序列化相同(<c>[JsonConverter]</c> 不被衍生型別繼承)。
/// Serialising by the concrete type must match serialising by the base type (<c>[JsonConverter]</c> is not inherited).
/// </summary>
[TestClass]
public sealed class FlexConcreteTypeSerializationTests
{
    [TestMethod]
    public void EveryComponent_SerialisedAsConcreteType_MatchesBaseType()
    {
        FlexComponent[] components =
        [
            new FlexBox { Layout = FlexLayout.Vertical, Contents = [new FlexFiller()] },
            new FlexText("a") { Weight = FlexWeight.Bold },
            new FlexImage("https://example.com/a.png") { Size = FlexSize.Full },
            new FlexVideo("https://example.com/v.mp4", "https://example.com/p.png", new FlexImage("https://example.com/a.png")),
            new FlexIcon("https://example.com/i.png"),
            new FlexButton(new MessageAction("a")),
            new FlexSeparator { Margin = FlexSpacing.Md },
            new FlexFiller { Flex = 1 },
            FlexTestSupport.Raw("""{"type":"spacer"}"""),
        ];

        foreach (var component in components)
        {
            var asBase = JsonSerializer.Serialize<FlexComponent>(component);
            var asConcrete = JsonSerializer.Serialize(component, component.GetType());

            Assert.AreEqual(asBase, asConcrete, component.GetType().Name);
            Assert.IsFalse(asConcrete.Contains("Layout", StringComparison.Ordinal), component.GetType().Name);
        }
    }

    [TestMethod]
    public void EveryContainer_SerialisedAsConcreteType_MatchesBaseType()
    {
        using var document = JsonDocument.Parse("""{"type":"future"}""");

        FlexContainer[] containers =
        [
            new FlexBubble { Size = FlexBubbleSize.Mega, Body = new FlexBox { Layout = FlexLayout.Vertical } },
            new FlexCarousel { Contents = [new FlexBubble()] },
            new RawFlexContainer(document.RootElement),
        ];

        foreach (var container in containers)
        {
            var asBase = JsonSerializer.Serialize<FlexContainer>(container);
            var asConcrete = JsonSerializer.Serialize(container, container.GetType());

            Assert.AreEqual(asBase, asConcrete, container.GetType().Name);
        }
    }

    [TestMethod]
    public void HelperObjects_SerialisedAsConcreteType_UseTheExplicitWriters()
    {
        Assert.AreEqual("""{"type":"span","text":"a","weight":"bold"}""", JsonSerializer.Serialize(new FlexSpan("a") { Weight = FlexWeight.Bold }));
        Assert.AreEqual(
            """{"type":"linearGradient","angle":"0deg","startColor":"#000000","endColor":"#FFFFFF"}""",
            JsonSerializer.Serialize(new FlexLinearGradient { Angle = "0deg", StartColor = "#000000", EndColor = "#FFFFFF" }));
        Assert.AreEqual("""{"backgroundColor":"#FFFFFF","separator":true}""", JsonSerializer.Serialize(new FlexBlockStyle { BackgroundColor = "#FFFFFF", Separator = true }));
        Assert.AreEqual("""{"body":{"separator":true}}""", JsonSerializer.Serialize(new FlexBubbleStyles { Body = new FlexBlockStyle { Separator = true } }));
    }

    [TestMethod]
    public void EveryPublicConcreteFlexType_CarriesAJsonConverter()
    {
        // 新增 Flex 型別卻忘了掛 [JsonConverter],直接序列化會退回反射而寫出 PascalCase 的錯誤 JSON。
        // A new Flex type without [JsonConverter] would fall back to reflection and write PascalCase JSON.
        var jsonTypes = typeof(FlexBubble).Assembly.GetTypes()
            .Where(type => type.IsPublic
                && type.IsClass
                && !type.IsAbstract
                && type.Namespace == typeof(FlexBubble).Namespace
                && (typeof(FlexComponent).IsAssignableFrom(type)
                    || typeof(FlexContainer).IsAssignableFrom(type)
                    || type == typeof(FlexSpan)
                    || type == typeof(FlexLinearGradient)
                    || type == typeof(FlexBlockStyle)
                    || type == typeof(FlexBubbleStyles)))
            .ToList();

        Assert.IsTrue(jsonTypes.Count >= 16, "expected at least the 16 concrete Flex model types");

        foreach (var type in jsonTypes)
        {
            Assert.IsNotNull(type.GetCustomAttribute<JsonConverterAttribute>(inherit: false), type.Name);
        }
    }

    [TestMethod]
    public void FlexMessage_SerialisedAsConcreteType_MatchesBaseType()
    {
        var message = new Ozakboy.Line.Messaging.Messages.FlexMessage("alt", new FlexBubble { Body = new FlexBox { Layout = FlexLayout.Vertical } });

        Assert.AreEqual(
            JsonSerializer.Serialize<Ozakboy.Line.Messaging.Messages.LineMessage>(message),
            JsonSerializer.Serialize(message));
    }
}
