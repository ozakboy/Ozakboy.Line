using System.Text.Json;
using Ozakboy.Line.Messaging.Messages;

namespace Ozakboy.Line.Tests;

/// <summary>
/// 文字訊息的 emoji 與 <c>textV2</c>(mention、emoji 替換)的序列化與本地檢查測試。
/// Tests for serialising and locally checking text messages with emojis and <c>textV2</c> (mentions and emoji
/// substitutions).
/// </summary>
[TestClass]
public sealed class LineTextMessageTests
{
    [TestMethod]
    public void TextMessage_WithEmojis_MatchesGolden()
    {
        var message = new TextMessage("$ hi $")
        {
            Emojis =
            {
                new LineTextEmoji(0, "5ac1bfd5040ab15980c9b435", "001"),
                new LineTextEmoji(5, "5ac1bfd5040ab15980c9b435", "002"),
            },
        };

        Assert.AreEqual(
            """{"type":"text","text":"$ hi $","emojis":[{"index":0,"productId":"5ac1bfd5040ab15980c9b435","emojiId":"001"},{"index":5,"productId":"5ac1bfd5040ab15980c9b435","emojiId":"002"}]}""",
            Serialize(message));
        Assert.IsTrue(message.Validate().IsSuccess);
    }

    [TestMethod]
    public void TextMessage_WithoutEmojis_OmitsTheArray()
    {
        Assert.AreEqual("""{"type":"text","text":"hi"}""", Serialize(new TextMessage("hi")));
    }

    [TestMethod]
    public void TextMessage_EmojiIndexNotOnADollar_FailsWithIndexInMessage()
    {
        var message = new TextMessage("hello $") { Emojis = { new LineTextEmoji(2, "p", "e") } };

        var result = message.Validate();

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.InvalidMessage, result.Error!.Code);
        StringAssert.Contains(result.Error.Message, "index=2");
    }

    [TestMethod]
    public void TextMessage_EmojiIndexBeyondTheText_Fails()
    {
        var message = new TextMessage("$") { Emojis = { new LineTextEmoji(1, "p", "e") } };

        Assert.AreEqual(LineErrorCodes.InvalidMessage, message.Validate().Error!.Code);
    }

    [TestMethod]
    public void TextMessage_TooManyEmojis_Fails()
    {
        var message = new TextMessage(new string('$', 21));
        for (var index = 0; index < 21; index++)
        {
            message.Emojis.Add(new LineTextEmoji(index, "p", "e"));
        }

        var result = message.Validate();

        Assert.AreEqual(LineErrorCodes.InvalidMessage, result.Error!.Code);
        StringAssert.Contains(result.Error.Message, "21");
        StringAssert.Contains(result.Error.Message, "20");
    }

    [TestMethod]
    public void LineTextEmoji_BlankIdsOrNegativeIndex_Throw()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new LineTextEmoji(0, " ", "001"));
        Assert.ThrowsExactly<ArgumentException>(() => new LineTextEmoji(0, "p", ""));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new LineTextEmoji(-1, "p", "001"));
    }

    [TestMethod]
    public void TextV2Message_MatchesGolden()
    {
        var message = new TextV2Message("Hi {user1} {everyone} {laugh}")
        {
            QuoteToken = "qt-1",
            Substitution =
            {
                ["user1"] = LineTextSubstitution.Mention("U1"),
                ["everyone"] = LineTextSubstitution.MentionAll(),
                ["laugh"] = LineTextSubstitution.Emoji("5ac1bfd5040ab15980c9b435", "001"),
            },
        };

        Assert.AreEqual(
            """{"type":"textV2","text":"Hi {user1} {everyone} {laugh}","substitution":{"user1":{"type":"mention","mentionee":{"type":"user","userId":"U1"}},"everyone":{"type":"mention","mentionee":{"type":"all"}},"laugh":{"type":"emoji","productId":"5ac1bfd5040ab15980c9b435","emojiId":"001"}},"quoteToken":"qt-1"}""",
            Serialize(message));
        Assert.IsTrue(message.Validate().IsSuccess);
    }

    [TestMethod]
    public void TextV2Message_WithQuickReplyAndSender_WritesBoth()
    {
        var message = new TextV2Message("Hi {a}")
        {
            Substitution = { ["a"] = LineTextSubstitution.MentionAll() },
            QuickReply = new LineQuickReply { Items = { new LineQuickReplyItem(new Messaging.Actions.MessageAction("好")) } },
            Sender = new LineMessageSender { Name = "客服" },
        };

        using var document = JsonDocument.Parse(Serialize(message));

        Assert.IsTrue(document.RootElement.TryGetProperty("quickReply", out _));
        Assert.AreEqual("客服", document.RootElement.GetProperty("sender").GetProperty("name").GetString());
        Assert.IsTrue(message.Validate().IsSuccess);
    }

    [TestMethod]
    public void TextV2Message_OverQuickReplyLimit_FailsWithQuickReplyCode()
    {
        var message = new TextV2Message("plain") { QuickReply = new LineQuickReply() };

        Assert.AreEqual(LineErrorCodes.TooManyQuickReplyItems, message.Validate().Error!.Code, "快速回覆的規則與 TextMessage 一致。");
    }

    [TestMethod]
    public void TextV2Message_ReferenceWithoutSubstitution_Fails()
    {
        var message = new TextV2Message("Hi {ghost}");

        var result = message.Validate();

        Assert.AreEqual(LineErrorCodes.InvalidMessage, result.Error!.Code);
        StringAssert.Contains(result.Error.Message, "{ghost}");
    }

    [TestMethod]
    public void TextV2Message_UnusedSubstitution_Fails()
    {
        var message = new TextV2Message("Hi") { Substitution = { ["extra"] = LineTextSubstitution.MentionAll() } };

        var result = message.Validate();

        Assert.AreEqual(LineErrorCodes.InvalidMessage, result.Error!.Code);
        StringAssert.Contains(result.Error.Message, "extra");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("with space")]
    [DataRow("dash-key")]
    [DataRow("中文")]
    [DataRow("a.b")]
    public void TextV2Message_InvalidSubstitutionKey_Fails(string key)
    {
        var message = new TextV2Message("Hi") { Substitution = { [key] = LineTextSubstitution.MentionAll() } };

        var result = message.Validate();

        Assert.AreEqual(LineErrorCodes.InvalidMessage, result.Error!.Code);
        StringAssert.Contains(result.Error.Message, "英數字與底線");
    }

    [TestMethod]
    public void TextV2Message_KeyWithDigitsAndUnderscore_Passes()
    {
        var message = new TextV2Message("Hi {user_1} and {2nd}")
        {
            Substitution = { ["user_1"] = LineTextSubstitution.MentionAll(), ["2nd"] = LineTextSubstitution.Emoji("p", "001") },
        };

        Assert.IsTrue(message.Validate().IsSuccess);
    }

    [TestMethod]
    public void TextV2Message_BracesThatAreNotReferences_AreOrdinaryText()
    {
        // 空的大括號、含空白的大括號、沒收尾的大括號都不是引用,不該被當成缺少替換項。
        var message = new TextV2Message("{} { x } {unclosed and { }") ;

        Assert.IsTrue(message.Validate().IsSuccess);
    }

    [TestMethod]
    public void TextV2Message_SameKeyReferencedTwice_Passes()
    {
        var message = new TextV2Message("{a} and {a}") { Substitution = { ["a"] = LineTextSubstitution.MentionAll() } };

        Assert.IsTrue(message.Validate().IsSuccess);
    }

    [TestMethod]
    public void TextV2Message_TooManyMentions_Fails()
    {
        var text = string.Join(' ', Enumerable.Range(0, 21).Select(i => "{m" + i + "}"));
        var message = new TextV2Message(text);
        for (var index = 0; index < 21; index++)
        {
            message.Substitution["m" + index] = LineTextSubstitution.Mention("U" + index);
        }

        var result = message.Validate();

        Assert.AreEqual(LineErrorCodes.InvalidMessage, result.Error!.Code);
        StringAssert.Contains(result.Error.Message, "21");
    }

    [TestMethod]
    public void LineTextSubstitution_BlankArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentException>(() => LineTextSubstitution.Mention(" "));
        Assert.ThrowsExactly<ArgumentException>(() => LineTextSubstitution.Emoji("", "001"));
        Assert.ThrowsExactly<ArgumentException>(() => LineTextSubstitution.Emoji("p", " "));
    }

    [TestMethod]
    public void TextV2Message_TypeStringIsTextV2()
    {
        Assert.AreEqual("textV2", new TextV2Message("x").Type);
    }

    /// <summary>
    /// 序列化成 JSON 字串。
    /// Serialises to a JSON string.
    /// </summary>
    /// <param name="message">訊息。The message.</param>
    /// <returns>JSON 字串。The JSON string.</returns>
    private static string Serialize(LineMessage message) => JsonSerializer.Serialize(message, LineJson.Options);
}
