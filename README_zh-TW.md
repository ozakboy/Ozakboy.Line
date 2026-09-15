# Ozakboy.Line

.NET 的 LINE Login、Messaging API 與 Webhook 用戶端,ASP.NET Core 一行就接得起來。

[English](README.md) | 繁體中文

```
dotnet add package Ozakboy.Line
dotnet add package Ozakboy.Line.AspNetCore
```

需要 .NET 10。除 Microsoft 官方套件與 `Ozakboy.*` 外零相依。

---

## 為什麼有這個套件

把 LINE 接進 ASP.NET Core 網站,實際上是三件各自獨立的事:用 LINE Login 讓人登入、用官方帳號推播、收 webhook。它們在 LINE 的文件裡分屬不同章節,用的是**兩個不同的頻道**與兩組不同的憑證,而且每一件都有幾個「做錯了不會有錯誤訊息」的細節 —— 差一個字元的 redirect URI、沒有人驗過的 `id_token`、在驗簽之前就被解析掉的 webhook 內容。

這個套件把三件事都做完,而 ASP.NET Core 的整合讓第一件變成 `AddLineLogin()`。

它抽取自實際上線中的幼兒園評價平台 [youmingdeng.ozakboy.life](https://youmingdeng.ozakboy.life):LINE 登入、加好友引導、推播通知與圖文選單都在那裡跑。

---

## 它的行為

**預期之內的失敗以 `Result<T>` 回傳,不擲例外。** 使用者不同意授權、授權碼過期、推播額度用完、對方封鎖了官方帳號 —— 這些在推播系統裡是日常。它們會以帶著固定 `line.*` 代碼與分類的 `Result` 回來,「忘了處理」因此是編譯期看得到的形狀,而不是執行期的意外。真正的程式缺陷(參數為 null、權杖空白)仍然擲出。

**LINE 自己的錯誤內容會被折進錯誤裡。** LINE 對格式不對的請求回的是 `{"message": "...", "details": [{"message": "...", "property": "..."}]}`,而 `message` 常常只有 `The request body has 1 error(s)` 這一句。真正說得出「哪個欄位錯了」的是 `details`,因此它被解析出來放進錯誤資料的 `lineMessage` 與 `lineDetails`,而 HTTP 分類原封不動:404 仍然是 `NotFound`,429 仍然是 `RateLimited`。

**HTTP 一律走 [Ozakboy.Http](https://github.com/ozakboy/Ozakboy.Http)。** 重試、逾時與日誌遮罩都是那條管線的事。channel secret 與存取權杖在啟動時就登記進它的遮罩器,日誌與錯誤訊息裡不會出現原文。

**推播只有在你給了重試鍵時才重試。** 推播是 POST,重送一次就是多發一則 —— 使用者的手機響兩次,而第一次的「失敗」很可能只是回應在路上掉了,訊息其實已經送達。設定 `RetryKey` 之後請求會帶上 `X-Line-Retry-Key`,LINE 以這個鍵去重,管線這時才願意重試。沒有鍵的推播只送一次,就一次。

**`id_token` 在本地驗,不引入任何 JWT 函式庫。** LINE 以 HS256 簽 id_token,金鑰就是 channel secret —— 驗證方與簽章方握有同一個祕密,不需要下載公鑰,也不需要 JWKS 快取。BCL 的 `HMACSHA256` 與 `Base64Url` 就夠了,簽章比對用 `CryptographicOperations.FixedTimeEquals`。簽章、發行者、對象、到期與 nonce 各驗一次,每一項失敗各有自己的錯誤代碼。

**webhook 簽章對著原始位元組驗。** 內容照收到的樣子讀,絕不先解析再序列化回去 —— 欄位順序、空白與跳脫方式在那一趟往返裡都可能改變,簽章從此再也對不上。比對是定時的。

---

## 快速開始

### 1. 用 LINE 登入(ASP.NET Core)

```csharp
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie()
    .AddLineLogin(options =>
    {
        options.ClientId = builder.Configuration["Line:Login:ChannelId"]!;
        options.ClientSecret = builder.Configuration["Line:Login:ChannelSecret"]!;
        options.BotPrompt = LineBotPrompt.Aggressive;   // 順便引導使用者加官方帳號好友
    });
```

```csharp
app.MapGet("/me", (ClaimsPrincipal user) => new
{
    UserId = user.FindFirstValue(LineClaimTypes.UserId),
    Name = user.FindFirstValue(ClaimTypes.Name),
    Picture = user.FindFirstValue(LineClaimTypes.Picture),
    IsFriend = user.FindFirstValue(LineClaimTypes.IsFriend),   // 查不到時這個宣告不存在
}).RequireAuthorization();
```

PKCE 預設開啟。每次登入產生一個 nonce,隨 OAuth state 帶過去、回來時與 `id_token` 比對;驗不過的 token 會讓整個登入失敗,而不是「先讓他登入再說」。回呼路徑(預設 `/signin-line`)要逐字登記在 LINE Developers 後台。

### 2. 只用核心用戶端

不走認證處理器的場合 —— LIFF 應用、背景工作、非 Web 的主機 —— 核心用戶端把同一套流程手動做一遍。

```csharp
builder.Services.AddLineLogin(options =>
{
    options.ChannelId = builder.Configuration["Line:Login:ChannelId"]!;
    options.ChannelSecret = builder.Configuration["Line:Login:ChannelSecret"]!;
});
```

```csharp
var verifier = LinePkce.CreateCodeVerifier();           // 自己保存到回呼時
var url = line.BuildAuthorizationUrl(new LineAuthorizationRequest
{
    RedirectUri = "https://example.com/callback",
    State = state,
    Nonce = nonce,
    CodeChallenge = LinePkce.ComputeCodeChallenge(verifier),
});

// …回呼時:
var login = await line.CompleteLoginAsync(code, redirectUri, verifier, nonce);
if (login.TryGetValue(out var result))
{
    var userId = result.Profile.UserId;
    var isFriend = result.IsFriend;    // Login channel 沒設 Linked OA 時為 null
}
```

`CompleteLoginAsync` 會走完換權杖、查個人檔案、查好友狀態與驗 id_token 四步。四步裡只有好友狀態允許失敗,記為 `IsFriend` 是 `null` —— 那與 `false` 是兩件不同的事。

### 3. 推播、回覆與 webhook

```csharp
builder.Services.AddLineMessaging(options =>
{
    options.ChannelAccessToken = builder.Configuration["Line:Messaging:ChannelAccessToken"]!;
    options.ChannelSecret = builder.Configuration["Line:Messaging:ChannelSecret"]!;
});
```

```csharp
await line.PushTextAsync(userId, "您追蹤的園所有新的裁罰紀錄");

await line.PushAsync(userId, [
    new TextMessage("新紀錄") { QuoteToken = quoteToken },
    new StickerMessage("446", "1988"),
], new LinePushOptions { RetryKey = jobId });   // 同一次邏輯推播固定同一個鍵,重試才不會重複發送
```

```csharp
app.MapLineWebhook("/line/webhook", async (evt, context, ct) =>
{
    if (evt.Type == LineWebhookEventTypes.Message && evt.Message?.Text is { } text)
    {
        var line = context.RequestServices.GetRequiredService<ILineMessagingClient>();
        await line.ReplyTextAsync(evt.ReplyToken!, $"收到:{text}", ct);
    }
});
```

這個端點先驗簽再解析 —— 簽章不對回 401,內容讀不懂回 400 —— 其餘一律回 200。單一事件的處理常式擲出例外只會被記錄,不會擋住同一批的其他事件:回非 2xx 會讓 LINE 重送**整批**,一個壞掉的處理常式就讓整批回 500 的結果,是其他本來成功的事件被反覆重做,而壞掉的那個仍然壞掉。

---

## 動工前值得先知道的幾件 LINE 的事

**Login channel 與 Messaging channel 是兩個不同的頻道。** 各有各的 id 與 secret,在 LINE Developers 後台也是兩個不同的項目。把其中一組填到另一組該在的位置,換來的是一個 400 與完全看不出是這件事的訊息。`AddLineLogin` 與 `AddLineMessaging` 因此各有各的設定,也各註冊一個具名 HTTP 用戶端。

**`bot_prompt` 與好友狀態 API 都需要 Linked OA。** Login channel 沒有連結官方帳號時,LINE 直接忽略 `bot_prompt` —— 登入照走,加好友那一步就是不會出現 —— 好友狀態端點則回 4xx。兩者都不會給你看得到的錯誤,這也是「設定好了卻沒效果」最常見的原因。本套件把查不到的好友狀態當成「未知」,不當成失敗。

**回覆免費,推播計額度。** reply token 不佔月額度,而每一次推播都佔。權杖只能用一次而且效期很短,所以「能回覆就不要推播」才是額度夠不夠用一個月的關鍵。重送事件帶的權杖多半已經失效。

**一次 multicast 最多 500 個收件者**,一次請求最多 5 則訊息。兩者都在送出前檢查,超量的呼叫因此失敗在自己的機器上,而不是在 LINE 那頭浪費一次額度。

**webhook 事件可能收到兩次。** LINE 沒有及時收到 200 就會重送,帶著同一個 `WebhookEventId` 與 `IsRedelivery`。任何有副作用的處理常式 —— 記帳、發訊、扣點 —— 都必須以那個識別碼去重。

**圖文選單要建立與上圖兩個步驟。** `CreateRichMenuAsync` 拿到 id,`UploadRichMenuImageAsync` 把圖片放上去;只建不上圖的選單設得上去,使用者那頭是一片空白。可點擊的區塊也不會畫在圖上 —— 按鈕看起來在哪裡、實際可以按的是哪裡,兩者要對得起來是你的事。

---

## API 一覽

### `ILineLoginClient`

| 成員 | 做什麼 |
| --- | --- |
| `BuildAuthorizationUrl(request)` | 要把瀏覽器導過去的網址。PKCE、nonce、`bot_prompt`、`ui_locales`、強制同意、自動登入開關。 |
| `ExchangeCodeAsync(code, redirectUri, codeVerifier?)` | 授權碼換權杖。 |
| `RefreshAsync(refreshToken)` | 續期,回新的權杖與新的 refresh token。 |
| `RevokeAsync(accessToken)` | 撤銷存取權杖。 |
| `VerifyAccessTokenAsync(accessToken)` | 權限範圍、所屬頻道、剩餘秒數。 |
| `VerifyIdTokenAsync(idToken, nonce?, userId?)` | 交給 LINE 遠端驗。一般請用本地的 `LineIdTokenValidator`。 |
| `GetProfileAsync(accessToken)` | 使用者識別碼、顯示名稱、大頭貼、狀態訊息。 |
| `GetUserInfoAsync(accessToken)` | OpenID Connect 的 userinfo。 |
| `GetFriendshipStatusAsync(accessToken)` | 是否已加官方帳號好友。 |
| `CompleteLoginAsync(code, redirectUri, codeVerifier?, nonce?)` | 以上全部,一次呼叫。 |

`LineIdTokenValidator.Validate(...)` 與 `LinePkce.CreateCodeVerifier()` / `ComputeCodeChallenge()` 是靜態的,不需要用戶端。

### `ILineMessagingClient`

| 分組 | 成員 |
| --- | --- |
| 送出 | `PushAsync` · `PushTextAsync` · `PushRawJsonAsync` · `MulticastAsync` · `BroadcastAsync` · `BroadcastTextAsync` · `BroadcastRawJsonAsync` · `ReplyAsync` · `ReplyTextAsync` · `ReplyRawJsonAsync` |
| 帳號 | `GetQuotaAsync` · `GetQuotaConsumptionAsync` · `GetBotInfoAsync` · `GetProfileAsync` · `GetMessageContentAsync` |
| 圖文選單 | `CreateRichMenuAsync` · `UploadRichMenuImageAsync` · `GetRichMenuListAsync` · `GetRichMenuAsync` · `DeleteRichMenuAsync` · `SetDefaultRichMenuAsync` · `ClearDefaultRichMenuAsync` · `GetDefaultRichMenuIdAsync` · `LinkRichMenuToUserAsync` · `UnlinkRichMenuFromUserAsync` · `GetRichMenuIdOfUserAsync` |

訊息型別:`TextMessage`、`ImageMessage`、`VideoMessage`、`AudioMessage`、`LocationMessage`、`StickerMessage`、`FlexMessage`,以及給尚未建模的東西用的 `RawMessage`。動作:`UriAction`、`MessageAction`、`PostbackAction`、`RawAction`。

`*RawJsonAsync` 這組多載接受單一訊息物件或訊息陣列,兩種都會被包成 `{"messages": […]}`。LINE 新增了本套件還沒跟上的東西時,那就是出口。

### Webhook

| 成員 | 做什麼 |
| --- | --- |
| `LineWebhookSignature.Verify(secret, body, signature)` | 定時比較;標頭缺漏、密鑰空白、簽章不是合法 base64 時一律 `false`。 |
| `LineWebhookParser.Parse(body)` | 解析成事件,沒建模的東西留在 `LineWebhookEvent.Raw`。 |
| `HttpRequest.ReadLineWebhookAsync(secret)` | 讀原始位元組、驗簽、解析。 |
| `MapLineWebhook(pattern, handler)` | 端點本身。逐事件與整批各一個多載。 |

### 錯誤代碼

`line.not_configured` · `line.validation.too_many_recipients` · `line.validation.too_many_messages` · `line.validation.invalid_json` · `line.api.error` · `line.api.invalid_response` · `line.id_token.invalid_format` · `line.id_token.unsupported_algorithm` · `line.id_token.invalid_signature` · `line.id_token.invalid_issuer` · `line.id_token.invalid_audience` · `line.id_token.expired` · `line.id_token.nonce_mismatch` · `line.webhook.invalid_signature` · `line.webhook.invalid_payload`

這些字串是公開契約,發佈後不再更動。

---

## 相容性

- **目標框架:** net10.0。
- **相依:** `Ozakboy.Core.Abstractions`、`Ozakboy.Http` 與 `Microsoft.Extensions.*`。`Ozakboy.Line.AspNetCore` 另外參照共用框架 `Microsoft.AspNetCore.App`。遞移相依裡沒有任何第三方套件,也沒有任何 JWT 函式庫。
- **測試:** 全程離線。測試一次都不連 LINE,憑證一律是佔位符。

## 授權

MIT,見 [LICENSE](LICENSE)。
