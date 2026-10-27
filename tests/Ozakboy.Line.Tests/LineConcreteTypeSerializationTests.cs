using System.Text.Json;
using Ozakboy.Line.Messaging.Actions;
using Ozakboy.Line.Messaging.Messages;
using Ozakboy.Line.Messaging.Messages.Template;

namespace Ozakboy.Line.Tests;

/// <summary>
/// 以具體型別作為泛型參數序列化時,輸出必須與以基底型別序列化相同。
/// Serialising with a concrete type as the generic argument must produce the same output as serialising with the
/// base type.
/// </summary>
/// <remarks>
/// <c>JsonSerializer.Serialize(new TextMessage("x"))</c> 的型別參數推導出來是 <c>TextMessage</c>,
/// 而 <c>[JsonConverter]</c> 掛在基底類別上並不會套用到衍生型別 —— 沒有這些測試時,反射會寫出
/// 屬性名全是 PascalCase 的錯誤 JSON,而且沒有任何錯誤。
/// <c>JsonSerializer.Serialize(new TextMessage("x"))</c> infers <c>TextMessage</c> as its type argument, and a
/// <c>[JsonConverter]</c> on the base class is not applied to derived types — without these tests, reflection would
/// write wrong JSON with PascalCase property names and no error at all.
/// </remarks>
[TestClass]
public sealed class LineConcreteTypeSerializationTests
{
    [TestMethod]
    public void TextMessage_SerialisedAsConcreteType_MatchesBaseType()
    {
        var message = new TextMessage("嗨");

        Assert.AreEqual(JsonSerializer.Serialize<LineMessage>(message), JsonSerializer.Serialize(message));
    }

    [TestMethod]
    public void EveryMessageKind_SerialisedAsConcreteType_MatchesBaseType()
    {
        using var flex = JsonDocument.Parse("""{"type":"bubble"}""");
        using var raw = JsonDocument.Parse("""{"type":"text","text":"x"}""");

        LineMessage[] messages =
        [
            new TextMessage("a"),
            new StickerMessage("1", "1"),
            new ImageMessage("https://example.com/o.png", "https://example.com/p.png"),
            new VideoMessage("https://example.com/v.mp4", "https://example.com/p.png"),
            new AudioMessage("https://example.com/a.m4a", 1000),
            new LocationMessage("t", "a", 1, 2),
            new FlexMessage("alt", flex.RootElement),
            new RawMessage(raw.RootElement),
            new TextV2Message("{a}") { Substitution = { ["a"] = LineTextSubstitution.MentionAll() } },
            new ImagemapMessage("https://example.com/m", "alt", 1040),
            new TemplateMessage("alt", new ConfirmTemplate("t", new MessageAction("是"), new MessageAction("否"))),
        ];

        foreach (var message in messages)
        {
            var asBase = JsonSerializer.Serialize<LineMessage>(message);
            var asConcrete = SerializeRuntime(message);

            Assert.AreEqual(asBase, asConcrete, message.GetType().Name);
        }
    }

    [TestMethod]
    public void EveryAction_SerialisedAsConcreteType_MatchesBaseType()
    {
        LineAction[] actions =
        [
            new MessageAction("a"),
            new UriAction("https://example.com"),
            new PostbackAction("a=1"),
            new ClipboardAction("c"),
            new DatetimePickerAction("d", "date"),
            new RichMenuSwitchAction("alias", "d"),
            new CameraAction(),
            new CameraRollAction(),
            new LocationAction(),
        ];

        foreach (var action in actions)
        {
            var asBase = JsonSerializer.Serialize<LineAction>(action);
            var asConcrete = SerializeRuntime(action);

            Assert.AreEqual(asBase, asConcrete, action.GetType().Name);
        }
    }

    [TestMethod]
    public void ActionAndMessage_SerialisedAsConcreteGenericArgument_MatchesBaseType()
    {
        var action = new MessageAction("a") { Label = "標籤" };

        Assert.AreEqual(JsonSerializer.Serialize<LineAction>(action), JsonSerializer.Serialize(action));
    }

    /// <summary>
    /// 以執行時期型別序列化,等同呼叫端寫 <c>Serialize(concreteInstance)</c>。
    /// Serialises by the runtime type, the same as a caller writing <c>Serialize(concreteInstance)</c>.
    /// </summary>
    /// <param name="value">值。The value.</param>
    /// <returns>JSON。The JSON.</returns>
    private static string SerializeRuntime(object value) => JsonSerializer.Serialize(value, value.GetType());
}
