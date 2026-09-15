using System.Net;
using Microsoft.Extensions.Options;
using Ozakboy.Http;
using Ozakboy.Line.Messaging.Actions;
using Ozakboy.Line.Messaging.RichMenu;
using Ozakboy.Line.Tests.TestSupport;

namespace Ozakboy.Line.Tests;

/// <summary>
/// 「以新換舊」、別名與批次連結的測試。全部離線,不連 LINE。
/// Tests for replacing a rich menu, its aliases, and the bulk links. Entirely offline; LINE is never called.
/// </summary>
/// <remarks>
/// 換選單是四到五個 API 呼叫串起來的流程,而它的價值正好在「順序對、失敗時收得乾淨」。
/// 這些測試驗的就是那個順序與那些收尾。
/// Replacing a menu is four or five API calls in sequence, and what it is worth rests on getting the order right
/// and cleaning up on failure. These tests are about that order and that cleanup.
/// </remarks>
[TestClass]
public sealed class LineRichMenuReplaceTests
{
    private const string AccessToken = "test-channel-access-token";

    [TestMethod]
    public async Task ReplaceRichMenuAsync_HappyPath_CallsEndpointsInOrder()
    {
        var handler = Arrange();
        var client = CreateClient(handler);

        var result = await client.ReplaceRichMenuAsync(
            Menu(),
            [1, 2, 3],
            "image/png",
            new LineRichMenuReplaceOptions { SetAsDefault = true, AliasId = "main", OldRichMenuId = "richmenu-old" });

        Assert.IsTrue(result.IsSuccess, result.IsFailure ? result.Error!.Message : string.Empty);
        Assert.AreEqual("richmenu-new", result.GetValueOrThrow());

        var calls = handler.Requests.Select(request => $"{request.Method} {request.Uri.AbsolutePath}").ToList();
        CollectionAssert.AreEqual(
            new List<string>
            {
                "POST /v2/bot/richmenu",
                "POST /v2/bot/richmenu/richmenu-new/content",
                "POST /v2/bot/user/all/richmenu/richmenu-new",
                "GET /v2/bot/richmenu/alias/main",
                "POST /v2/bot/richmenu/alias/main",
                "DELETE /v2/bot/richmenu/richmenu-old",
            },
            calls,
            "順序是:建立 → 上傳圖片 → 設預設 → 指向別名 → 刪舊。");
    }

    [TestMethod]
    public async Task ReplaceRichMenuAsync_WithoutOptions_OnlyCreatesAndUploads()
    {
        var handler = Arrange();
        var client = CreateClient(handler);

        await client.ReplaceRichMenuAsync(Menu(), [1], "image/png");

        Assert.AreEqual(2, handler.Requests.Count, "三個可選步驟都沒要求時,就只是建立加上傳。");
    }

    [TestMethod]
    public async Task ReplaceRichMenuAsync_UploadFails_DeletesTheNewMenu()
    {
        // 沒有圖片的選單掛上去是一片空白,比沒有選單更糟,而它已經佔掉一個選單額度。
        var handler = Arrange(uploadStatus: HttpStatusCode.BadRequest);
        var client = CreateClient(handler);

        var result = await client.ReplaceRichMenuAsync(Menu(), [1], "image/png");

        Assert.IsTrue(result.IsFailure);

        var calls = handler.Requests.Select(request => $"{request.Method} {request.Uri.AbsolutePath}").ToList();
        CollectionAssert.AreEqual(
            new List<string>
            {
                "POST /v2/bot/richmenu",
                "POST /v2/bot/richmenu/richmenu-new/content",
                "DELETE /v2/bot/richmenu/richmenu-new",
            },
            calls,
            "圖片傳不上去就把剛建的選單收回來,不留半成品。");
    }

    [TestMethod]
    public async Task ReplaceRichMenuAsync_DeletingTheOldMenuFails_StillSucceeds()
    {
        // 新選單已經上線,回報失敗只會讓呼叫端重做一次,而重做的結果是又多一個選單。
        var handler = Arrange(deleteOldStatus: HttpStatusCode.InternalServerError);
        var client = CreateClient(handler);

        var result = await client.ReplaceRichMenuAsync(
            Menu(),
            [1],
            "image/png",
            new LineRichMenuReplaceOptions { OldRichMenuId = "richmenu-old" });

        Assert.IsTrue(result.IsSuccess, "刪舊選單是盡力而為,失敗不影響整體結果。");
        Assert.AreEqual("richmenu-new", result.GetValueOrThrow());
    }

    [TestMethod]
    public async Task ReplaceRichMenuAsync_AliasDoesNotExist_CreatesIt()
    {
        var handler = Arrange(aliasExists: false);
        var client = CreateClient(handler);

        await client.ReplaceRichMenuAsync(Menu(), [1], "image/png", new LineRichMenuReplaceOptions { AliasId = "main" });

        var aliasCalls = handler.Requests
            .Where(request => request.Uri.AbsolutePath.Contains("/alias", StringComparison.Ordinal))
            .Select(request => $"{request.Method} {request.Uri.AbsolutePath}")
            .ToList();

        CollectionAssert.AreEqual(
            new List<string> { "GET /v2/bot/richmenu/alias/main", "POST /v2/bot/richmenu/alias" },
            aliasCalls,
            "查不到別名就建立一個,建立打的是不帶別名的位址。");
    }

    [TestMethod]
    public async Task CreateRichMenuAliasAsync_WritesBothFields()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        await client.CreateRichMenuAliasAsync("main", "richmenu-1");

        Assert.AreEqual(HttpMethod.Post, handler.Last.Method);
        Assert.AreEqual("/v2/bot/richmenu/alias", handler.Last.Uri.AbsolutePath);

        var json = handler.Last.Json();
        Assert.AreEqual("main", json.GetProperty("richMenuAliasId").GetString());
        Assert.AreEqual("richmenu-1", json.GetProperty("richMenuId").GetString());
    }

    [TestMethod]
    public async Task UpdateRichMenuAliasAsync_PostsToTheAliasPath()
    {
        // 更新別名是 POST 到 /alias/{aliasId},不是 PUT。照 REST 的直覺寫 PUT 會得到 404。
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        await client.UpdateRichMenuAliasAsync("main", "richmenu-2");

        Assert.AreEqual(HttpMethod.Post, handler.Last.Method);
        Assert.AreEqual("/v2/bot/richmenu/alias/main", handler.Last.Uri.AbsolutePath);
        Assert.AreEqual("richmenu-2", handler.Last.Json().GetProperty("richMenuId").GetString());
    }

    [TestMethod]
    public async Task GetRichMenuAliasListAsync_ReadsTheAliasesField()
    {
        var handler = FakeHttpMessageHandler.Json("""{"aliases":[{"richMenuAliasId":"main","richMenuId":"richmenu-1"}]}""");
        var client = CreateClient(handler);

        var aliases = await client.GetRichMenuAliasListAsync();

        Assert.IsTrue(aliases.IsSuccess);
        Assert.AreEqual(1, aliases.GetValueOrThrow().Count);
        Assert.AreEqual("main", aliases.GetValueOrThrow()[0].RichMenuAliasId);
        Assert.AreEqual("richmenu-1", aliases.GetValueOrThrow()[0].RichMenuId);
    }

    [TestMethod]
    public async Task DeleteRichMenuAliasAsync_UsesDelete()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        await client.DeleteRichMenuAliasAsync("main");

        Assert.AreEqual(HttpMethod.Delete, handler.Last.Method);
        Assert.AreEqual("/v2/bot/richmenu/alias/main", handler.Last.Uri.AbsolutePath);
    }

    [TestMethod]
    public async Task LinkRichMenuToUsersAsync_WritesRichMenuIdAndUserIds()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        await client.LinkRichMenuToUsersAsync(["U1", "U2"], "richmenu-1");

        Assert.AreEqual("/v2/bot/richmenu/bulk/link", handler.Last.Uri.AbsolutePath);

        var json = handler.Last.Json();
        Assert.AreEqual("richmenu-1", json.GetProperty("richMenuId").GetString());
        Assert.AreEqual(2, json.GetProperty("userIds").GetArrayLength());
    }

    [TestMethod]
    public async Task LinkRichMenuToUsersAsync_OverFiveHundred_FailsWithoutSending()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);
        var userIds = Enumerable.Range(0, LineMessagingLimits.RichMenuBulkUsers + 1).Select(index => $"U{index}").ToList();

        var result = await client.LinkRichMenuToUsersAsync(userIds, "richmenu-1");

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.TooManyRecipients, result.Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count, "超量的請求在本地就擋下來,一個位元組都不送。");
    }

    [TestMethod]
    public async Task LinkRichMenuToUsersAsync_ExactlyFiveHundred_IsAccepted()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);
        var userIds = Enumerable.Range(0, LineMessagingLimits.RichMenuBulkUsers).Select(index => $"U{index}").ToList();

        Assert.IsTrue((await client.LinkRichMenuToUsersAsync(userIds, "richmenu-1")).IsSuccess, "500 是上限,不是超過上限。");
    }

    [TestMethod]
    public async Task UnlinkRichMenuFromUsersAsync_WritesOnlyUserIds()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        await client.UnlinkRichMenuFromUsersAsync(["U1"]);

        Assert.AreEqual("/v2/bot/richmenu/bulk/unlink", handler.Last.Uri.AbsolutePath);

        var json = handler.Last.Json();
        Assert.AreEqual(1, json.GetProperty("userIds").GetArrayLength());
        Assert.IsFalse(json.TryGetProperty("richMenuId", out _), "解除連結不指定選單。");
    }

    [TestMethod]
    public async Task ValidateRichMenuAsync_PostsTheMenuToTheValidateEndpoint()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = CreateClient(handler);

        await client.ValidateRichMenuAsync(Menu());

        Assert.AreEqual(HttpMethod.Post, handler.Last.Method);
        Assert.AreEqual("/v2/bot/richmenu/validate", handler.Last.Uri.AbsolutePath);
        Assert.AreEqual("主選單", handler.Last.Json().GetProperty("name").GetString());
    }

    [TestMethod]
    public async Task NotConfigured_FailsWithoutSendingAnything()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = new Messaging.LineMessagingClient(
            new HttpPipelineClient(new HttpClient(handler)),
            Options.Create(new Messaging.LineMessagingOptions()));

        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.ReplaceRichMenuAsync(Menu(), [1], "image/png")).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.GetRichMenuAliasListAsync()).Error!.Code);
        Assert.AreEqual(LineErrorCodes.NotConfigured, (await client.LinkRichMenuToUsersAsync(["U1"], "richmenu-1")).Error!.Code);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    /// <summary>
    /// 安排整條換選單流程會打到的所有端點。
    /// Arranges every endpoint the replacement flow touches.
    /// </summary>
    /// <param name="uploadStatus">圖片上傳端點的狀態碼。The upload endpoint's status code.</param>
    /// <param name="deleteOldStatus">刪舊選單的狀態碼。The old menu deletion's status code.</param>
    /// <param name="aliasExists">別名是否已經存在。Whether the alias already exists.</param>
    /// <returns>假處理器。The fake handler.</returns>
    private static FakeHttpMessageHandler Arrange(
        HttpStatusCode uploadStatus = HttpStatusCode.OK,
        HttpStatusCode deleteOldStatus = HttpStatusCode.OK,
        bool aliasExists = true) =>
        new((request, _) =>
        {
            var path = request.RequestUri!.AbsolutePath;

            if (path.EndsWith("/content", StringComparison.Ordinal))
            {
                return Answer(uploadStatus, "{}");
            }

            if (request.Method == HttpMethod.Delete && path.EndsWith("richmenu-old", StringComparison.Ordinal))
            {
                return Answer(deleteOldStatus, "{}");
            }

            if (request.Method == HttpMethod.Get && path.Contains("/alias/", StringComparison.Ordinal))
            {
                return aliasExists
                    ? Answer(HttpStatusCode.OK, """{"richMenuAliasId":"main","richMenuId":"richmenu-old"}""")
                    : Answer(HttpStatusCode.NotFound, """{"message":"alias not found"}""");
            }

            if (request.Method == HttpMethod.Post && path == "/v2/bot/richmenu")
            {
                return Answer(HttpStatusCode.OK, """{"richMenuId":"richmenu-new"}""");
            }

            return Answer(HttpStatusCode.OK, "{}");
        });

    /// <summary>
    /// 建立一個回應。
    /// Builds a response.
    /// </summary>
    /// <param name="status">狀態碼。The status code.</param>
    /// <param name="json">內容。The body.</param>
    /// <returns>回應。The response.</returns>
    private static HttpResponseMessage Answer(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
    };

    /// <summary>
    /// 建立一個最小的圖文選單定義。
    /// Builds a minimal rich menu definition.
    /// </summary>
    /// <returns>選單。The menu.</returns>
    private static LineRichMenu Menu()
    {
        var menu = new LineRichMenu { Name = "主選單", ChatBarText = "開啟選單" };
        menu.Areas.Add(new LineRichMenuArea
        {
            Bounds = new LineRichMenuBounds { X = 0, Y = 0, Width = 2500, Height = 1686 },
            Action = new MessageAction("嗨"),
        });

        return menu;
    }

    /// <summary>
    /// 建立直接接上假處理器的用戶端。
    /// Creates a client wired straight to a fake handler.
    /// </summary>
    /// <param name="handler">假處理器。The fake handler.</param>
    /// <returns>用戶端。The client.</returns>
    private static Messaging.LineMessagingClient CreateClient(FakeHttpMessageHandler handler) =>
        new(
            new HttpPipelineClient(new HttpClient(handler)),
            Options.Create(new Messaging.LineMessagingOptions { ChannelAccessToken = AccessToken }));
}
