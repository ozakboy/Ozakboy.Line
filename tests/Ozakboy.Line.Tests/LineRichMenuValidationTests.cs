using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Ozakboy.Http;
using Ozakboy.Line.Messaging;
using Ozakboy.Line.Messaging.Actions;
using Ozakboy.Line.Messaging.RichMenu;
using Ozakboy.Line.Tests.TestSupport;

namespace Ozakboy.Line.Tests;

/// <summary>
/// 圖文選單區塊動作的送出前本地驗證測試。全部離線,不連 LINE。
/// Tests for the local check of rich menu area actions before sending. Entirely offline; LINE is never called.
/// </summary>
/// <remarks>
/// 區塊動作不合規格時,LINE 回的是只說「請求內容有錯」的 400;選單上有十幾個區塊時,
/// 在本地失敗才說得出是第幾個區塊。
/// A bad area action costs a 400 that says only that the body is wrong; with a dozen areas on a menu, failing
/// locally is the only way to say which one.
/// </remarks>
[TestClass]
public sealed class LineRichMenuValidationTests
{
    [TestMethod]
    public void Validate_AllActionsFine_Succeeds()
    {
        Assert.IsTrue(Menu(new MessageAction("嗨"), new UriAction("https://example.com")).Validate().IsSuccess);
    }

    [TestMethod]
    public void Validate_SecondAreaLabelTooLong_FailsNamingTheArea()
    {
        var menu = Menu(new MessageAction("嗨"), new MessageAction("嗨") { Label = new string('字', 21) });

        var result = menu.Validate();

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.InvalidAction, result.Error!.Code);
        StringAssert.Contains(result.Error.Message, "richMenu.areas[1]");
    }

    [TestMethod]
    public void Validate_LabelExactlyTwenty_Passes()
    {
        Assert.IsTrue(Menu(new MessageAction("嗨") { Label = new string('字', 20) }).Validate().IsSuccess);
    }

    [TestMethod]
    public void Validate_RawAction_IsNotChecked()
    {
        using var document = JsonDocument.Parse("""{"type":"message","label":"超過二十個字的標籤超過二十個字的標籤超過二十個字","text":"x"}""");

        Assert.IsTrue(Menu(new RawAction(document.RootElement)).Validate().IsSuccess, "RawAction 是逃生口,不驗。");
    }

    [TestMethod]
    public async Task CreateRichMenuAsync_BadAction_FailsWithoutSendingAnything()
    {
        var handler = Arrange();
        var client = CreateClient(handler);

        var result = await client.CreateRichMenuAsync(BadMenu());

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.InvalidAction, result.Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task ValidateRichMenuAsync_BadAction_FailsWithoutSendingAnything()
    {
        var handler = Arrange();
        var client = CreateClient(handler);

        var result = await client.ValidateRichMenuAsync(BadMenu());

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.InvalidAction, result.Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task ReplaceRichMenuAsync_BadAction_FailsBeforeCreatingAnything()
    {
        var handler = Arrange();
        var client = CreateClient(handler);

        var result = await client.ReplaceRichMenuAsync(
            BadMenu(),
            [1],
            "image/png",
            new LineRichMenuReplaceOptions { OldRichMenuId = "richmenu-old" });

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.InvalidAction, result.Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count, "不合規格的選單不能建出來再收回。");
    }

    [TestMethod]
    public async Task CreateRichMenuAsync_GoodMenu_StillSends()
    {
        var handler = Arrange();
        var client = CreateClient(handler);

        var result = await client.CreateRichMenuAsync(Menu(new MessageAction("嗨")));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(1, handler.Requests.Count);
    }

    private static LineRichMenu BadMenu() =>
        Menu(new MessageAction("嗨"), new MessageAction("嗨") { Label = new string('字', 21) });

    private static LineRichMenu Menu(params LineAction[] actions)
    {
        var menu = new LineRichMenu { Name = "主選單", ChatBarText = "開啟選單" };
        foreach (var action in actions)
        {
            menu.Areas.Add(new LineRichMenuArea
            {
                Bounds = new LineRichMenuBounds { X = 0, Y = 0, Width = 2500, Height = 1686 },
                Action = action,
            });
        }

        return menu;
    }

    private static FakeHttpMessageHandler Arrange() =>
        new((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"richMenuId":"richmenu-new"}""", System.Text.Encoding.UTF8, "application/json"),
        });

    private static LineMessagingClient CreateClient(FakeHttpMessageHandler handler) =>
        new(
            new HttpPipelineClient(new HttpClient(handler)),
            Options.Create(new LineMessagingOptions { ChannelAccessToken = "test-channel-access-token" }));
}
