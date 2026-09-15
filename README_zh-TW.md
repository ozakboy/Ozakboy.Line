# Ozakboy.Line

.NET 的 LINE Login、Messaging API 與 Webhook 用戶端,ASP.NET Core 一行就接得起來。

[English](README.md) | 繁體中文

```
dotnet add package Ozakboy.Line
dotnet add package Ozakboy.Line.AspNetCore
dotnet add package Ozakboy.Line.Mcp
```

| 套件 | 裝來做什麼 |
| --- | --- |
| `Ozakboy.Line` | 核心。LINE Login、Messaging API、Webhook 驗簽與解析,加上訊息範本與關鍵字自動回覆。 |
| `Ozakboy.Line.AspNetCore` | 整合。`AddLineLogin()` 認證方案與 `MapLineWebhook()` 端點。 |
| `Ozakboy.Line.Mcp` | 選配。把上面那些包成 MCP 工具,讓外部 AI 操作官方帳號 —— 預設只能提案,不能發送。 |

需要 .NET 10。核心與整合套件除 Microsoft 官方套件與 `Ozakboy.*` 外零相依;MCP 套件另外相依官方的 `ModelContextProtocol.AspNetCore`(理由見「相容性」)。

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

## 快速回覆

掛在訊息下方的一列按鈕,使用者按了(或自己打了字)整列就消失。它不是選單 —— 要一直在的入口請用圖文選單。

```csharp
await line.PushAsync(userId, [
    new TextMessage("要看哪一區的資料?")
    {
        QuickReply = new LineQuickReply
        {
            Items =
            {
                new LineQuickReplyItem(new MessageAction("台北市") { Label = "台北市" }),
                new LineQuickReplyItem(new PostbackAction("area=tpe", "已選台北市") { Label = "不留痕跡" }),
                new LineQuickReplyItem(new LocationAction { Label = "傳我的位置" }),
                new LineQuickReplyItem(new DatetimePickerAction("when", DatetimePickerAction.ModeDate) { Label = "選日期" }),
            },
        },
    },
]);
```

動作有十種:`MessageAction`、`PostbackAction`、`UriAction`、`DatetimePickerAction`、`CameraAction`、`CameraRollAction`、`LocationAction`、`ClipboardAction`、`RichMenuSwitchAction`,以及尚未建模時用的 `RawAction`。`UriAction.AltUriDesktop` 給桌機版另一個位址(LIFF 或 `line://` 這類只有手機認得的位址,桌機開起來是空白頁);`PostbackAction.InputOption` 決定按完之後鍵盤與圖文選單怎麼反應。

**上限是 13 顆按鈕**,而且超量時 LINE 不是「只顯示前 13 個」,是整則訊息退回。送出前會逐則檢查,超量的呼叫因此失敗在自己的機器上,錯誤訊息還會說出實際是幾顆。

---

## 訊息範本

一組可重複使用、可帶變數的 LINE 訊息。存的是**訊息陣列的 JSON** 而不是一段純文字,所以圖片、Flex 版面、快速回覆都能存進範本;改文案也不必動程式碼。

```csharp
services.AddLineTemplates(o => o.UseJsonFile("data/line-templates.json"));   // 不設定就是記憶體
```

```csharp
var store = provider.GetRequiredService<ILineTemplateStore>();

await store.UpsertAsync(new LineMessageTemplate
{
    Name = "裁罰通知",
    MessagesJson = """[{"type":"text","text":"{{name}} 有新的裁罰紀錄,文號 {{docNo}}。"}]""",
});

var rendered = LineTemplateRenderer.Render(template.MessagesJson, new Dictionary<string, string>
{
    ["name"] = "○○幼兒園",
    ["docNo"] = "北市教兒字第 1130001 號",
});

if (rendered.IsSuccess)
{
    await line.PushRawJsonAsync(userId, rendered.GetValueOrThrow());
}
```

佔位符是 `{{名稱}}`,名稱限英文字母或底線開頭。代入的值會**先做 JSON 字串跳脫**再替換 —— 少了這一步,一個暱稱裡的引號就足以把整份 JSON 弄壞,而症狀是 LINE 回一個沒指名欄位的 400。缺變數時直接回 `line.validation.missing_template_variables` 失敗並列出缺哪幾個,不會把 `{{name}}` 原樣送到使用者眼前。

儲存體有記憶體與 JSON 檔兩種內建實作(檔案版寫入原子化、讀寫共用一把鎖);有資料庫的話自己實作 `ILineTemplateStore` 就接得上,本套件不碰任何 ORM。

---

## 關鍵字自動回覆

規則是**資料**而不是程式碼:整條規則存進 store,由後台或 MCP 工具編輯,改一句歡迎詞不必重新部署。

```csharp
services.AddLineMessaging(o => { /* … */ });
services.AddLineAutoReply(
    o => o.Enabled = true,
    s => s.UseJsonFile("data/line-auto-replies.json"));

app.MapLineWebhook("/line/webhook", o => o.AutoReply = true, async (evt, context, ct) =>
{
    // 自動回覆已經跑過了,結果在這裡:
    var outcome = context.Items[LineWebhookItems.AutoReplyOutcome] as LineAutoReplyOutcome;
    if (outcome?.Kind == LineAutoReplyOutcomeKind.Replied)
    {
        return;   // 規則回過了,這裡就不用再回一次
    }

    // …宿主自己的處理
});
```

比對方式有六種:`Exact`、`StartsWith`、`Contains`、`Regex`,加上不看文字的 `Follow`(加好友時的歡迎訊息)與 `Fallback`(收到文字但沒有其他規則命中時的回覆)。把歡迎訊息與備援回覆也做成規則,好處是**規則表成為唯一的來源** —— 後台改歡迎訊息就是 upsert 那一條,不必再去翻設定檔或環境變數裡有沒有另一份。

挑選順序是先看 `Priority`(小的先),同分再看比對方式的明確程度(`Exact` > `StartsWith` > `Contains` > `Regex`)。第二層排序很重要:規則多起來之後,一條寫得寬鬆的 `Contains` 很容易把一堆精準的 `Exact` 全部吃掉,而那種問題從規則列表上看不出來 —— 每一條單獨看都是對的。

回覆內容可用 `{{text}}`(使用者原訊息)與 `{{displayName}}`(顯示名稱,只有規則真的用到時才會去查個人檔案)。正規表示式的比對有 100 毫秒上限,逾時或表示式無效一律視為「不符」而不是擲出例外 —— 一條寫壞的規則不該讓整個 webhook 回 500,那會讓 LINE 重送整批事件。

自動回覆一律走 **reply token** 而不是推播:回覆不計額度,而自動回覆是所有功能裡最會消耗額度的那一個。

---

## 讓外部 AI 操作:`Ozakboy.Line.Mcp`

把上面這些包成一組 MCP 工具,讓 Claude、自訂連接器或任何 MCP 用戶端查詢帳號狀態、擬廣播、改規則與範本、替換圖文選單。

```csharp
builder.Services.AddLineMessaging(o => { /* … */ });
builder.Services.AddLineTemplates();
builder.Services.AddLineAutoReply();

builder.Services.AddLineMcpServer(
    o =>
    {
        o.ApiKey = builder.Configuration["LINE_MCP_API_KEY"];   // 沒設定 = 整個端點關閉
        o.SendMode = LineMcpSendMode.Review;                    // 預設值
        o.MaxSendRequestsPerDay = 10;
    },
    s => s.UseJsonFileOutbox("data/line-mcp-outbox.json"));

// 兩道閘都必須在 UseRouting 之前
app.UseLineMcpWellKnownNotFound();
app.UseLineMcpKeyGate();
app.UseRouting();
// …
app.MapLineMcp();
```

**鐵則:AI 不得直接發送。** 預設的 `Review` 模式下,`line_send_push` / `line_send_multicast` / `line_send_broadcast` / `line_replace_rich_menu` 一則訊息都不會送出 —— 它們把內容寫進待發佇列,狀態 `PendingReview`,等宿主的人核准。核准的 API(`ILineMcpOutboxService.ApproveAndSendAsync`)**不是任何一個工具**,只給宿主程式呼叫:待審制度的價值就在於提案的一方沒有核准權。`Direct` 模式會直接送,但仍然寫一筆紀錄 —— 沒有紀錄的話,事後沒有任何地方查得出「那則訊息是誰、什麼時候、為什麼發的」。

每日還有一道建立上限(預設 10 筆,依 `TimeZoneId` 的當地日期計算)。這是防暴走的閘:AI 在迴圈裡重試同一個工具是很常見的失敗模式,沒有上限時那個迴圈會把待審佇列灌到沒有人願意去看。

**金鑰閘。** 對外網址是 `{prefix}/{ApiKey}`(預設 prefix 為 `/mcp/line`),金鑰走路徑而不是標頭,因為多數 MCP 連接器只給使用者一個填網址的欄位。金鑰以定時比較驗證,**失敗一律回 404 而不是 401/403** —— 401/403 等於告訴對方「這裡確實有東西」,而這個端點的存在本身就不必對外說。沒設定 `ApiKey` = 功能關閉,整個子樹都是 404;金鑰輪替 = 改設定重啟。

`UseLineMcpWellKnownNotFound()` 讓 `/.well-known/` 一律回乾淨的 JSON 404:MCP 連接器連線前會探測 `/.well-known/oauth-protected-resource`,那些請求若落到 MVC 拿到 HTML 登入頁或 302,連接器會判定這個站需要 OAuth 而連不上。⚠ 這道閘攔的是**整個 `/.well-known` 子樹**,日後要放 `security.txt` 得在這一行之前處理掉。

宿主已經有自己的 MCP server 時,用 `WithLineTools()` 把這一組掛上去就好:

```csharp
builder.Services.AddLineMcp(o => o.ApiKey = key);
builder.Services.AddMcpServer()
    .WithHttpTransport(o => o.Stateless = true)
    .WithTools<MyOwnTools>()
    .WithLineTools();
```

工具清單:

| 類別 | 工具 |
| --- | --- |
| 查詢 | `line_get_bot_info` · `line_get_quota` · `line_get_profile` |
| 送出(走待審) | `line_send_push` · `line_send_multicast` · `line_send_broadcast` |
| 待發佇列 | `line_list_outbox` · `line_get_outbox` · `line_cancel_outbox` |
| 圖文選單 | `line_list_rich_menus` · `line_get_default_rich_menu` · `line_list_rich_menu_aliases` · `line_validate_rich_menu` · `line_replace_rich_menu`(走待審) |
| 自動回覆 | `line_list_auto_replies` · `line_upsert_auto_reply` · `line_delete_auto_reply` · `line_test_auto_reply` |
| 訊息範本 | `line_list_templates` · `line_get_template` · `line_upsert_template` · `line_delete_template` · `line_render_template` |

規則與範本的工具**不走待審**,因為它們不會自己送訊息:改壞一條規則的後果是回錯話,而不是主動打擾全體好友,改回來也只是再 upsert 一次。要完全禁止的話有 `AllowRuleEdits` / `AllowTemplateEdits` / `AllowRichMenuChanges` 三個開關。宿主沒有 `AddLineTemplates` / `AddLineAutoReply` 時,那幾個工具仍然掛得上,只是呼叫時回一句「未啟用」—— 這比工具根本不出現清楚。

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
| 圖文選單 | `CreateRichMenuAsync` · `UploadRichMenuImageAsync` · `GetRichMenuListAsync` · `GetRichMenuAsync` · `DeleteRichMenuAsync` · `SetDefaultRichMenuAsync` · `ClearDefaultRichMenuAsync` · `GetDefaultRichMenuIdAsync` · `LinkRichMenuToUserAsync` · `UnlinkRichMenuFromUserAsync` · `GetRichMenuIdOfUserAsync` · `ValidateRichMenuAsync` |
| 選單替換與別名 | `ReplaceRichMenuAsync` · `CreateRichMenuAliasAsync` · `UpdateRichMenuAliasAsync` · `DeleteRichMenuAliasAsync` · `GetRichMenuAliasAsync` · `GetRichMenuAliasListAsync` · `LinkRichMenuToUsersAsync` · `UnlinkRichMenuFromUsersAsync` |

訊息型別:`TextMessage`、`ImageMessage`、`VideoMessage`、`AudioMessage`、`LocationMessage`、`StickerMessage`、`FlexMessage`,以及給尚未建模的東西用的 `RawMessage`。動作:`UriAction`、`MessageAction`、`PostbackAction`、`DatetimePickerAction`、`CameraAction`、`CameraRollAction`、`LocationAction`、`ClipboardAction`、`RichMenuSwitchAction`、`RawAction`。快速回覆:`LineQuickReply` / `LineQuickReplyItem`。

`ReplaceRichMenuAsync` 把「建立 → 上傳圖片 → 設預設 → 指向別名 → 刪舊選單」串成一次呼叫。失敗處理刻意不對稱:**圖片上傳失敗會刪掉剛建的新選單**(沒有圖片的選單比沒有選單更糟),但**刪舊選單失敗只記錄、整體仍算成功**(新選單已經上線,回報失敗只會讓呼叫端重做而多出一個選單)。

`*RawJsonAsync` 這組多載接受單一訊息物件或訊息陣列,兩種都會被包成 `{"messages": […]}`。LINE 新增了本套件還沒跟上的東西時,那就是出口。

### 訊息範本與自動回覆

| 成員 | 做什麼 |
| --- | --- |
| `LineTemplateVariables.Extract(json)` | 抽出範本用到的變數名,去重保序。 |
| `LineTemplateRenderer.Render(json, values)` | 代入變數並驗證結果仍是 1~5 則的合法訊息陣列。 |
| `LineTemplateRenderer.RenderMessages(json, values)` | 同上,但回 `RawMessage` 清單。 |
| `LineTemplateRenderer.Validate(json)` | 只驗證,不代入。存進 store 之前用。 |
| `ILineTemplateStore` | `ListAsync` · `GetAsync` · `UpsertAsync` · `DeleteAsync`。內建記憶體與 JSON 檔兩種實作。 |
| `LineAutoReplyMatcher.Match(rules, text)` | 挑出該回覆的規則(只看四種文字模式)。 |
| `LineAutoReplyMatcher.FindFirst(rules, mode)` | 取 `Follow` / `Fallback` 規則中優先序最小的一條。 |
| `LineAutoReplyMatcher.Validate(rule)` | 樣式、正規表示式、回覆內容三件事一起驗。 |
| `ILineAutoReplyStore` | 形狀與 `ILineTemplateStore` 相同。 |
| `ILineAutoReplyService.HandleAsync(evt)` | 對一個 webhook 事件套用規則,回 `Skipped` / `NoMatch` / `Replied`。 |

### MCP(`Ozakboy.Line.Mcp`)

| 成員 | 做什麼 |
| --- | --- |
| `AddLineMcp(configure, stores?)` | 設定、待發佇列、待發項目服務、抓圖用的具名 HTTP 用戶端。 |
| `AddLineMcpServer(configure, stores?)` | 上面全部,加一個只掛 LINE 工具的 stateless MCP server。 |
| `IMcpServerBuilder.WithLineTools()` | 把六組工具加進宿主既有的 MCP server。 |
| `UseLineMcpKeyGate(prefix?)` | 金鑰閘,**必須在 `UseRouting` 之前**。 |
| `UseLineMcpWellKnownNotFound()` | `/.well-known/` 一律乾淨 404。 |
| `MapLineMcp(prefix?)` | MCP 端點,prefix 要與金鑰閘用同一個。 |
| `ILineMcpOutboxService` | `ApproveAndSendAsync` · `RejectAsync` · `CancelAsync`。**只給宿主呼叫,不是 MCP 工具。** |
| `ILineMcpOutboxStore` | 待發佇列的儲存體;內建記憶體與 JSON 檔兩種實作。 |

待發項目狀態:`PendingReview` → `Approved` / `Sent` / `Failed` / `Rejected` / `Canceled`。狀態只往前走,已送出的項目不會回到待審 —— 能回到待審的已送項目,意味著同一則訊息可能被核准兩次。

### Webhook

| 成員 | 做什麼 |
| --- | --- |
| `LineWebhookSignature.Verify(secret, body, signature)` | 定時比較;標頭缺漏、密鑰空白、簽章不是合法 base64 時一律 `false`。 |
| `LineWebhookParser.Parse(body)` | 解析成事件,沒建模的東西留在 `LineWebhookEvent.Raw`。 |
| `HttpRequest.ReadLineWebhookAsync(secret)` | 讀原始位元組、驗簽、解析。 |
| `MapLineWebhook(pattern, handler)` | 端點本身。逐事件與整批各一個多載。 |
| `MapLineWebhook(pattern, configure, handler)` | 同上,另可用 `LineWebhookEndpointOptions.AutoReply` 先跑一次自動回覆。 |
| `LineWebhookItems.AutoReplyOutcome` | 自動回覆結果在 `HttpContext.Items` 裡的鍵名。 |

### 錯誤代碼

`line.not_configured` · `line.validation.too_many_recipients` · `line.validation.too_many_messages` · `line.validation.invalid_json` · `line.validation.too_many_quick_reply_items` · `line.validation.missing_template_variables` · `line.validation.invalid_auto_reply_rule` · `line.mcp.outbox_not_found` · `line.mcp.outbox_invalid_status` · `line.mcp.image_fetch_failed` · `line.api.error` · `line.api.invalid_response` · `line.id_token.invalid_format` · `line.id_token.unsupported_algorithm` · `line.id_token.invalid_signature` · `line.id_token.invalid_issuer` · `line.id_token.invalid_audience` · `line.id_token.expired` · `line.id_token.nonce_mismatch` · `line.webhook.invalid_signature` · `line.webhook.invalid_payload`

這些字串是公開契約,發佈後不再更動。

---

## 相容性

- **目標框架:** net10.0。
- **相依:** `Ozakboy.Core.Abstractions`、`Ozakboy.Http` 與 `Microsoft.Extensions.*`。`Ozakboy.Line.AspNetCore` 另外參照共用框架 `Microsoft.AspNetCore.App`。核心與整合兩個套件的遞移相依裡沒有任何第三方套件,也沒有任何 JWT 函式庫。
- **MCP 套件的相依例外:** `Ozakboy.Line.Mcp` 相依 `ModelContextProtocol.AspNetCore` —— MCP 的官方 C# SDK,由 modelcontextprotocol 組織與 Microsoft 共同維護。這是相依政策的**唯一明列例外**,理由是 MCP 是一份有版本的線路協定,自己實作等於自己維護一份會隨協定改版而落後的實作,而落後的症狀是「連接器連得上但工具清單是空的」。不想帶這條相依的話,不裝這個套件即可,核心與整合套件完全不受影響。
- **測試:** 全程離線。測試一次都不連 LINE,憑證一律是佔位符。

## 授權

MIT,見 [LICENSE](LICENSE)。
