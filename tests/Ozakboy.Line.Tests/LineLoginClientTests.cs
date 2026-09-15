using System.Net;
using Microsoft.Extensions.Options;
using Ozakboy.Http;
using Ozakboy.Line.Tests.TestSupport;

namespace Ozakboy.Line.Tests;

/// <summary>
/// LINE Login 用戶端的測試。全部離線,不連 LINE。
/// Tests for the LINE Login client. Entirely offline; LINE is never called.
/// </summary>
[TestClass]
public sealed class LineLoginClientTests
{
    private const string RedirectUri = "https://example.com/signin-line";

    [TestMethod]
    public void BuildAuthorizationUrl_IncludesEveryParameterAndEncodesThem()
    {
        var client = CreateClient(FakeHttpMessageHandler.Json("{}"), out _);

        var url = client.BuildAuthorizationUrl(new LineAuthorizationRequest
        {
            RedirectUri = RedirectUri,
            State = "state value/with+specials",
            Nonce = "nonce-1",
            Scopes = ["profile", "openid", "email"],
            BotPrompt = LineBotPrompt.Aggressive,
            ForceConsent = true,
            UiLocales = "zh-TW",
            CodeChallenge = "challenge-1",
            DisableAutoLogin = true,
            DisableIosAutoLogin = true,
        });

        StringAssert.StartsWith(url, LineEndpoints.Authorization + "?response_type=code");
        StringAssert.Contains(url, "&client_id=test-channel-id");
        StringAssert.Contains(url, "&redirect_uri=https%3A%2F%2Fexample.com%2Fsignin-line");
        StringAssert.Contains(url, "&state=state%20value%2Fwith%2Bspecials", "state 必須逐字元編碼,+ 與 / 都不能原樣送出。");
        StringAssert.Contains(url, "&scope=profile%20openid%20email", "多個權限範圍以空白分隔,編碼後為 %20。");
        StringAssert.Contains(url, "&nonce=nonce-1");
        StringAssert.Contains(url, "&prompt=consent");
        StringAssert.Contains(url, "&bot_prompt=aggressive", "bot_prompt 一律小寫,LINE 不接受列舉名稱的大小寫。");
        StringAssert.Contains(url, "&ui_locales=zh-TW");
        StringAssert.Contains(url, "&code_challenge=challenge-1");
        StringAssert.Contains(url, "&code_challenge_method=S256");
        StringAssert.Contains(url, "&disable_auto_login=true");
        StringAssert.Contains(url, "&disable_ios_auto_login=true");
    }

    [TestMethod]
    public void BuildAuthorizationUrl_OmitsOptionalParametersWhenUnset()
    {
        var client = CreateClient(FakeHttpMessageHandler.Json("{}"), out _);

        var url = client.BuildAuthorizationUrl(new LineAuthorizationRequest
        {
            RedirectUri = RedirectUri,
            State = "state-1",
        });

        Assert.IsFalse(url.Contains("nonce=", StringComparison.Ordinal));
        Assert.IsFalse(url.Contains("bot_prompt=", StringComparison.Ordinal));
        Assert.IsFalse(url.Contains("prompt=", StringComparison.Ordinal));
        Assert.IsFalse(url.Contains("code_challenge", StringComparison.Ordinal));
        StringAssert.Contains(url, "&scope=profile%20openid", "沒有指定範圍時採用設定裡的預設。");
    }

    [TestMethod]
    public void BuildAuthorizationUrl_UsesOptionsBotPromptWhenRequestLeavesItUnset()
    {
        var options = new LineLoginOptions
        {
            ChannelId = TestIdToken.ChannelId,
            ChannelSecret = TestIdToken.ChannelSecret,
            BotPrompt = LineBotPrompt.Normal,
        };
        var client = new LineLoginClient(new HttpPipelineClient(new HttpClient(FakeHttpMessageHandler.Json("{}"))), Options.Create(options));

        var url = client.BuildAuthorizationUrl(new LineAuthorizationRequest { RedirectUri = RedirectUri, State = "s" });

        StringAssert.Contains(url, "&bot_prompt=normal");
    }

    [TestMethod]
    public void BuildAuthorizationUrl_NotConfigured_Throws()
    {
        var client = new LineLoginClient(
            new HttpPipelineClient(new HttpClient(FakeHttpMessageHandler.Json("{}"))),
            Options.Create(new LineLoginOptions()));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            client.BuildAuthorizationUrl(new LineAuthorizationRequest { RedirectUri = RedirectUri, State = "s" }));
    }

    [TestMethod]
    public async Task ExchangeCodeAsync_SendsCorrectFormAndParsesResponse()
    {
        var handler = FakeHttpMessageHandler.Json(
            """{"access_token":"at-1","expires_in":2592000,"id_token":"it-1","refresh_token":"rt-1","scope":"profile openid","token_type":"Bearer"}""");
        var client = CreateClient(handler, out _);

        var result = await client.ExchangeCodeAsync("code-1", RedirectUri, "verifier-1");

        Assert.IsTrue(result.IsSuccess);
        Assert.IsTrue(result.TryGetValue(out var tokens));
        Assert.AreEqual("at-1", tokens.AccessToken);
        Assert.AreEqual(2592000, tokens.ExpiresIn);
        Assert.AreEqual("it-1", tokens.IdToken);
        Assert.AreEqual("rt-1", tokens.RefreshToken);
        Assert.AreEqual("profile openid", tokens.Scope);
        Assert.AreEqual("Bearer", tokens.TokenType);

        var form = handler.Last.Form();
        Assert.AreEqual(HttpMethod.Post, handler.Last.Method);
        Assert.AreEqual(LineEndpoints.Token, handler.Last.Uri.ToString());
        Assert.AreEqual("authorization_code", form["grant_type"]);
        Assert.AreEqual("code-1", form["code"]);
        Assert.AreEqual(RedirectUri, form["redirect_uri"]);
        Assert.AreEqual(TestIdToken.ChannelId, form["client_id"]);
        Assert.AreEqual(TestIdToken.ChannelSecret, form["client_secret"]);
        Assert.AreEqual("verifier-1", form["code_verifier"]);
    }

    [TestMethod]
    public async Task ExchangeCodeAsync_WithoutPkce_OmitsCodeVerifier()
    {
        var handler = FakeHttpMessageHandler.Json("""{"access_token":"at-1","expires_in":1,"scope":"","token_type":"Bearer"}""");
        var client = CreateClient(handler, out _);

        await client.ExchangeCodeAsync("code-1", RedirectUri);

        Assert.IsFalse(handler.Last.Form().ContainsKey("code_verifier"));
    }

    [TestMethod]
    public async Task RefreshAsync_SendsRefreshGrant()
    {
        var handler = FakeHttpMessageHandler.Json("""{"access_token":"at-2","expires_in":1,"refresh_token":"rt-2","scope":"","token_type":"Bearer"}""");
        var client = CreateClient(handler, out _);

        var result = await client.RefreshAsync("rt-1");

        Assert.IsTrue(result.IsSuccess);
        var form = handler.Last.Form();
        Assert.AreEqual("refresh_token", form["grant_type"]);
        Assert.AreEqual("rt-1", form["refresh_token"]);
        Assert.AreEqual(TestIdToken.ChannelId, form["client_id"]);
    }

    [TestMethod]
    public async Task RevokeAsync_EmptyBody_Succeeds()
    {
        // 撤銷成功時 LINE 回 200 但沒有內容。把「空內容」當成解析失敗是很容易寫出來的錯,
        // 因此這條路徑單獨驗一次。
        var handler = new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty),
        });
        var client = CreateClient(handler, out _);

        var result = await client.RevokeAsync("at-1");

        Assert.IsTrue(result.IsSuccess);
        var form = handler.Last.Form();
        Assert.AreEqual(LineEndpoints.Revoke, handler.Last.Uri.ToString());
        Assert.AreEqual("at-1", form["access_token"]);
        Assert.AreEqual(TestIdToken.ChannelSecret, form["client_secret"]);
    }

    [TestMethod]
    public async Task VerifyAccessTokenAsync_PutsTokenInQueryAndParsesResponse()
    {
        var handler = FakeHttpMessageHandler.Json("""{"scope":"profile","client_id":"test-channel-id","expires_in":2591977}""");
        var client = CreateClient(handler, out _);

        var result = await client.VerifyAccessTokenAsync("at 1/with+specials");

        Assert.IsTrue(result.IsSuccess);
        Assert.IsTrue(result.TryGetValue(out var info));
        Assert.AreEqual("profile", info.Scope);
        Assert.AreEqual("test-channel-id", info.ClientId);
        Assert.AreEqual(2591977, info.ExpiresIn);
        // 比對 AbsoluteUri 而不是 ToString():Uri.ToString() 會把 %20 還原成空白顯示,
        // 實際送上線路的是 AbsoluteUri 那一份。
        StringAssert.Contains(
            handler.Last.Uri.AbsoluteUri,
            "access_token=at%201%2Fwith%2Bspecials",
            "權杖放進 query 前必須編碼,否則 + 會在伺服端變成空白。");
    }

    [TestMethod]
    public async Task GetProfileAsync_SendsBearerTokenAndParsesResponse()
    {
        var handler = FakeHttpMessageHandler.Json(
            """{"userId":"U1","displayName":"阿明","pictureUrl":"https://example.com/p.jpg","statusMessage":"hi"}""");
        var client = CreateClient(handler, out _);

        var result = await client.GetProfileAsync("at-1");

        Assert.IsTrue(result.IsSuccess);
        Assert.IsTrue(result.TryGetValue(out var profile));
        Assert.AreEqual("U1", profile.UserId);
        Assert.AreEqual("阿明", profile.DisplayName);
        Assert.AreEqual("https://example.com/p.jpg", profile.PictureUrl);
        Assert.AreEqual("hi", profile.StatusMessage);
        Assert.AreEqual("Bearer at-1", handler.Last.Headers["Authorization"]);
    }

    [TestMethod]
    public async Task GetUserInfoAsync_ParsesSubject()
    {
        var handler = FakeHttpMessageHandler.Json("""{"sub":"U1","name":"阿明","picture":"https://example.com/p.jpg"}""");
        var client = CreateClient(handler, out _);

        var result = await client.GetUserInfoAsync("at-1");

        Assert.IsTrue(result.TryGetValue(out var info));
        Assert.AreEqual("U1", info.Subject);
        Assert.AreEqual("阿明", info.Name);
        Assert.AreEqual(LineEndpoints.UserInfo, handler.Last.Uri.ToString());
    }

    [TestMethod]
    public async Task GetFriendshipStatusAsync_ParsesFriendFlag()
    {
        var client = CreateClient(FakeHttpMessageHandler.Json("""{"friendFlag":true}"""), out _);

        var result = await client.GetFriendshipStatusAsync("at-1");

        Assert.IsTrue(result.TryGetValue(out var isFriend));
        Assert.IsTrue(isFriend);
    }

    [TestMethod]
    public async Task LineErrorBody_IsFoldedIntoErrorMessageAndData()
    {
        var handler = new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                """{"message":"The request body has 2 error(s)","details":[{"message":"May not be empty","property":"messages[0].text"},{"message":"Must be one of","property":"messages[0].type"}]}""",
                System.Text.Encoding.UTF8,
                "application/json"),
        });
        var client = CreateClient(handler, out _);

        var result = await client.ExchangeCodeAsync("code-1", RedirectUri);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.ApiError, result.Error.Code);
        StringAssert.Contains(result.Error.Message, "LINE API 400:");
        StringAssert.Contains(result.Error.Message, "The request body has 2 error(s)");

        Assert.IsTrue(result.Error.TryGetData(LineErrorDataKeys.LineMessage, out var lineMessage));
        Assert.AreEqual("The request body has 2 error(s)", lineMessage);

        Assert.IsTrue(result.Error.TryGetData(LineErrorDataKeys.LineDetails, out var details));
        Assert.AreEqual(
            "messages[0].text: May not be empty; messages[0].type: Must be one of",
            details,
            "details 串成一行,屬性與訊息以冒號相連、各筆以分號相隔。");

        Assert.IsTrue(result.Error.TryGetInt64(Ozakboy.Http.HttpErrorDataKeys.StatusCode, out var status));
        Assert.AreEqual(400L, status, "Ozakboy.Http 原本放進去的資料鍵必須保留。");
    }

    [TestMethod]
    public async Task NotFound_KeepsNotFoundCategory()
    {
        // 加工錯誤時保留 Ozakboy.Http 判定的分類,下游才不必為了知道「這是不是找不到」而去解析代碼字串。
        var handler = new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("""{"message":"Not found"}""", System.Text.Encoding.UTF8, "application/json"),
        });
        var client = CreateClient(handler, out _);

        var result = await client.GetProfileAsync("at-1");

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.ApiError, result.Error.Code);
        Assert.AreEqual(ErrorCategory.NotFound, result.Error.Category);
    }

    [TestMethod]
    public async Task TooManyRequests_KeepsRateLimitedCategory()
    {
        var handler = new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("""{"message":"Too many requests"}""", System.Text.Encoding.UTF8, "application/json"),
        });
        var client = CreateClient(handler, out _);

        var result = await client.GetProfileAsync("at-1");

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(ErrorCategory.RateLimited, result.Error.Category);
    }

    [TestMethod]
    public async Task NotConfigured_FailsWithoutSendingAnything()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var client = new LineLoginClient(
            new HttpPipelineClient(new HttpClient(handler)),
            Options.Create(new LineLoginOptions()));

        var result = await client.ExchangeCodeAsync("code-1", RedirectUri);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(LineErrorCodes.NotConfigured, result.Error.Code);
        Assert.AreEqual(ErrorCategory.Validation, result.Error.Category);
        Assert.AreEqual(0, handler.Requests.Count, "憑證沒設定時一個請求都不該送出。");
    }

    [TestMethod]
    public async Task CompleteLoginAsync_HappyPath_ReturnsEverything()
    {
        var idToken = TestIdToken.Create(nonce: "nonce-1", email: "user@example.com");
        var handler = FakeHttpMessageHandler.Routes(
            ("/oauth2/v2.1/token", $$"""{"access_token":"at-1","expires_in":1,"id_token":"{{idToken}}","refresh_token":"rt-1","scope":"profile openid","token_type":"Bearer"}"""),
            ("/v2/profile", """{"userId":"U1","displayName":"阿明"}"""),
            ("/friendship/v1/status", """{"friendFlag":true}"""));
        var client = CreateClient(handler, out _);

        var result = await client.CompleteLoginAsync("code-1", RedirectUri, nonce: "nonce-1");

        Assert.IsTrue(result.IsSuccess, result.IsFailure ? result.Error.ToString() : null);
        Assert.IsTrue(result.TryGetValue(out var login));
        Assert.AreEqual("at-1", login.Tokens.AccessToken);
        Assert.AreEqual("U1", login.Profile.UserId);
        Assert.AreEqual(true, login.IsFriend);
        Assert.IsNotNull(login.IdToken);
        Assert.AreEqual("user@example.com", login.IdToken.Email);
    }

    [TestMethod]
    public async Task CompleteLoginAsync_FriendshipUnavailable_LeavesIsFriendNull()
    {
        // Login channel 沒有設定 Linked OA 時 LINE 對好友狀態端點回 4xx。那是頻道設定的事實,
        // 不該讓一次本來成功的登入變成失敗。
        var handler = new FakeHttpMessageHandler((request, _) =>
        {
            var uri = request.RequestUri!.ToString();
            if (uri.Contains("/friendship/", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.Forbidden)
                {
                    Content = new StringContent("""{"message":"Not available"}""", System.Text.Encoding.UTF8, "application/json"),
                };
            }

            var json = uri.Contains("/v2/profile", StringComparison.Ordinal)
                ? """{"userId":"U1","displayName":"阿明"}"""
                : """{"access_token":"at-1","expires_in":1,"scope":"profile","token_type":"Bearer"}""";

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            };
        });
        var client = CreateClient(handler, out _);

        var result = await client.CompleteLoginAsync("code-1", RedirectUri);

        Assert.IsTrue(result.IsSuccess, "好友狀態查不到不該讓登入失敗。");
        Assert.IsTrue(result.TryGetValue(out var login));
        Assert.IsNull(login.IsFriend, "查不到時必須是 null,而不是 false —— 兩者意思不同。");
    }

    [TestMethod]
    public async Task CompleteLoginAsync_TamperedIdToken_FailsWholeLogin()
    {
        var tampered = TestIdToken.WithBrokenSignature(TestIdToken.Create());
        var handler = FakeHttpMessageHandler.Routes(
            ("/oauth2/v2.1/token", $$"""{"access_token":"at-1","expires_in":1,"id_token":"{{tampered}}","scope":"profile openid","token_type":"Bearer"}"""),
            ("/v2/profile", """{"userId":"U1","displayName":"阿明"}"""),
            ("/friendship/v1/status", """{"friendFlag":false}"""));
        var client = CreateClient(handler, out _);

        var result = await client.CompleteLoginAsync("code-1", RedirectUri);

        Assert.IsTrue(result.IsFailure, "id_token 驗不過時整個登入都必須失敗。");
        Assert.AreEqual(LineErrorCodes.IdTokenInvalidSignature, result.Error.Code);
    }

    [TestMethod]
    public async Task CompleteLoginAsync_NoIdToken_LeavesIdTokenNull()
    {
        var handler = FakeHttpMessageHandler.Routes(
            ("/oauth2/v2.1/token", """{"access_token":"at-1","expires_in":1,"scope":"profile","token_type":"Bearer"}"""),
            ("/v2/profile", """{"userId":"U1","displayName":"阿明"}"""),
            ("/friendship/v1/status", """{"friendFlag":false}"""));
        var client = CreateClient(handler, out _);

        var result = await client.CompleteLoginAsync("code-1", RedirectUri);

        Assert.IsTrue(result.TryGetValue(out var login));
        Assert.IsNull(login.IdToken);
        Assert.AreEqual(false, login.IsFriend);
    }

    [TestMethod]
    public async Task VerifyIdTokenAsync_SendsFormAndMapsPayload()
    {
        var handler = FakeHttpMessageHandler.Json(
            """{"iss":"https://access.line.me","sub":"U1","aud":"test-channel-id","exp":1800000000,"iat":1799999000,"amr":["pwd"],"name":"阿明"}""");
        var client = CreateClient(handler, out _);

        var result = await client.VerifyIdTokenAsync("it-1", "nonce-1", "U1");

        Assert.IsTrue(result.TryGetValue(out var payload));
        Assert.AreEqual("U1", payload.Subject);
        Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(1800000000), payload.ExpiresAt);

        var form = handler.Last.Form();
        Assert.AreEqual("it-1", form["id_token"]);
        Assert.AreEqual(TestIdToken.ChannelId, form["client_id"]);
        Assert.AreEqual("nonce-1", form["nonce"]);
        Assert.AreEqual("U1", form["user_id"]);
        Assert.IsFalse(form.ContainsKey("client_secret"), "遠端驗 id_token 不送 client_secret。");
    }

    /// <summary>
    /// 建立接上假處理器的用戶端。
    /// Creates a client wired to a fake handler.
    /// </summary>
    /// <param name="handler">假處理器。The fake handler.</param>
    /// <param name="options">用到的設定。The options used.</param>
    /// <returns>用戶端。The client.</returns>
    private static LineLoginClient CreateClient(FakeHttpMessageHandler handler, out LineLoginOptions options)
    {
        options = new LineLoginOptions
        {
            ChannelId = TestIdToken.ChannelId,
            ChannelSecret = TestIdToken.ChannelSecret,
        };

        return new LineLoginClient(new HttpPipelineClient(new HttpClient(handler)), Options.Create(options));
    }
}
