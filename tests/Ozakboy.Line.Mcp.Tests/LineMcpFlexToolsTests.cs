using System.Reflection;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace Ozakboy.Line.Mcp.Tests;

/// <summary>
/// <c>line_validate_flex</c> 的測試:名稱契約、解析、Raw 節點、本地驗證結果與大小。
/// Tests for <c>line_validate_flex</c>: the name contract, parsing, Raw nodes, the local validation result and the size.
/// </summary>
[TestClass]
public sealed class LineMcpFlexToolsTests
{
    private const string Good = """{"type":"bubble","body":{"type":"box","layout":"vertical","contents":[{"type":"text","text":"Hi"}]}}""";

    [TestMethod]
    public void ToolName_IsTheContract_AndReadOnly()
    {
        var method = typeof(LineFlexTools).GetMethod(nameof(LineFlexTools.ValidateFlex))!;
        var attribute = method.GetCustomAttribute<McpServerToolAttribute>();

        Assert.IsNotNull(attribute);
        Assert.AreEqual("line_validate_flex", attribute.Name);
        Assert.IsTrue(attribute.ReadOnly);
    }

    [TestMethod]
    public void ValidLayout_IsParsedTypedAndValid()
    {
        var json = Run(Good);

        Assert.IsTrue(json.GetProperty("ok").GetBoolean());
        Assert.IsTrue(json.GetProperty("parsed").GetBoolean());
        Assert.IsTrue(json.GetProperty("valid").GetBoolean());
        Assert.IsTrue(json.GetProperty("fullyTyped").GetBoolean());
        Assert.AreEqual("bubble", json.GetProperty("containerType").GetString());
        Assert.AreEqual(0, json.GetProperty("rawNodes").GetArrayLength());
        Assert.AreEqual(Good.Length, json.GetProperty("sizeBytes").GetInt32());
        Assert.AreEqual(LineMessagingLimits.MaxFlexBubbleBytes, json.GetProperty("sizeLimitBytes").GetInt32());
    }

    [TestMethod]
    public void RuleViolation_ComesBackWithCodeAndPath()
    {
        var json = Run("""{"type":"bubble","body":{"type":"box","layout":"vertical","contents":[{"type":"image","url":"http://example.com/a.png"}]}}""");

        Assert.IsTrue(json.GetProperty("ok").GetBoolean());
        Assert.IsTrue(json.GetProperty("parsed").GetBoolean());
        Assert.IsFalse(json.GetProperty("valid").GetBoolean());
        Assert.AreEqual("line.validation.invalid_flex", json.GetProperty("code").GetString());
        StringAssert.Contains(json.GetProperty("error").GetString(), "body.contents[0].url");
    }

    [TestMethod]
    public void UnmodelledNodes_AreListedAsRawAndNotValidated()
    {
        // spacer 刻意不建模;含未知欄位的節點整個退回 Raw,因此它的壞 URL 不會被擋。
        var json = Run("""{"type":"bubble","body":{"type":"box","layout":"vertical","contents":[{"type":"spacer","size":"md"},{"type":"image","url":"http://bad","futureField":1}]}}""");

        Assert.IsTrue(json.GetProperty("valid").GetBoolean());
        Assert.IsFalse(json.GetProperty("fullyTyped").GetBoolean());
        Assert.AreEqual(2, json.GetProperty("rawNodes").GetArrayLength());
        Assert.AreEqual("body.contents[0]", json.GetProperty("rawNodes")[0].GetString());
        Assert.AreEqual("body.contents[1]", json.GetProperty("rawNodes")[1].GetString());
        Assert.IsTrue(json.TryGetProperty("note", out _));
    }

    [TestMethod]
    public void WholeMessageObject_IsUnwrapped_AndItsAltTextChecked()
    {
        var tooLong = new string('x', LineMessagingLimits.MaxAltTextLength + 1);
        var wrapped = $$"""{"type":"flex","altText":"{{tooLong}}","contents":{{Good}}}""";

        var json = Run(wrapped);

        Assert.IsTrue(json.GetProperty("parsed").GetBoolean());
        Assert.IsFalse(json.GetProperty("valid").GetBoolean());
        Assert.AreEqual("line.validation.invalid_message", json.GetProperty("code").GetString());
    }

    [TestMethod]
    public void AltTextArgument_IsCheckedWhenGiven()
    {
        var json = Run(Good, new string('x', LineMessagingLimits.MaxAltTextLength + 1));

        Assert.IsFalse(json.GetProperty("valid").GetBoolean());
        Assert.AreEqual("line.validation.invalid_message", json.GetProperty("code").GetString());
    }

    [TestMethod]
    public void InvalidJson_ReportsNotParsed()
    {
        var json = Run("{not json");

        Assert.IsTrue(json.GetProperty("ok").GetBoolean());
        Assert.IsFalse(json.GetProperty("parsed").GetBoolean());
        Assert.IsFalse(json.GetProperty("valid").GetBoolean());
        Assert.AreEqual("line.validation.invalid_json", json.GetProperty("code").GetString());
    }

    [TestMethod]
    public void BlankArgument_IsAnError()
    {
        var json = Run("  ");

        Assert.IsFalse(json.GetProperty("ok").GetBoolean());
    }

    [TestMethod]
    public void Carousel_SizeLimitIsTheCarouselOne()
    {
        var json = Run("""{"type":"carousel","contents":[]}""");

        Assert.AreEqual("carousel", json.GetProperty("containerType").GetString());
        Assert.AreEqual(LineMessagingLimits.MaxFlexCarouselBytes, json.GetProperty("sizeLimitBytes").GetInt32());
        Assert.IsFalse(json.GetProperty("valid").GetBoolean());
    }

    private static JsonElement Run(string contentsJson, string? altText = null)
    {
        using var document = JsonDocument.Parse(LineFlexTools.ValidateFlex(contentsJson, altText));
        return document.RootElement.Clone();
    }
}
