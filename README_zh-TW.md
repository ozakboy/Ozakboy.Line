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

## 範本與圖片地圖訊息

再補兩種 LINE 的訊息型別,建模的目的是讓 LINE 挑剔的欄位名變成編譯器的事。範本是四種固定版面之一;圖片地圖是一張大圖,上面劃出可點擊的區域。

```csharp
await line.PushAsync(userId, [
    new TemplateMessage("要看哪一區?", new ButtonsTemplate("選一個")
    {
        Title = "地區",
        ThumbnailImageUrl = "https://example.com/districts.jpg",
        Actions = { new MessageAction("台北市") { Label = "台北市" }, new PostbackAction("area=ntpc") { Label = "新北市" } },
    }),
    new TemplateMessage("確認?", new ConfirmTemplate("要訂閱通知嗎?", new MessageAction("要"), new MessageAction("不要"))),
]);

await line.PushAsync(userId, [
    new ImagemapMessage("https://example.com/map", "地圖", baseHeight: 1040)
    {
        Actions =
        {
            new ImagemapUriAction("https://example.com/north", new LineImagemapArea(0, 0, 1040, 520)),
            new ImagemapMessageAction("南區", new LineImagemapArea(0, 520, 1040, 520)),
        },
    },
]);
```

`CarouselTemplate` 收最多十個 `CarouselColumn`,`ImageCarouselTemplate` 收最多十個 `ImageCarouselColumn`。**上限在送出前檢查**:四個按鈕、確認範本恰好兩個動作、十欄且每欄最多三個動作並各欄一致、五十塊圖片地圖區域。違反的呼叫失敗在自己的機器上,代碼是 `line.validation.invalid_template` 或 `line.validation.invalid_imagemap`,訊息說得出實際數量 —— LINE 那頭是整則退回,而 400 什麼都不說。

圖片地圖有兩件事要先知道:LINE 抓的是 `{baseUrl}/{width}` 而不是 `baseUrl` 本身,所以那個位址底下要放多個尺寸的檔案、結尾不能有斜線;底圖寬度固定 1040,所以建構子只收高度。動作的位址欄位是 `linkUri`,不是快速回覆動作的 `uri` —— 這兩組動作在這裡是不同的型別,原因就在這。

每則訊息還可以帶 `Sender`(`name`、`iconUrl`),只改那一則顯示的名稱與頭像;`VideoMessage.TrackingId` 讓 LINE 在使用者看完影片時送 `videoPlayComplete` 事件,由 `evt.VideoPlayComplete.TrackingId` 讀回。

---

## Flex Message

Flex 訊息可以用 C# 組出來,也可以從 LINE Flex Message Simulator 產生的 JSON 讀回;兩條路最後是同一套強型別模型,送出之前都會被檢查。

```csharp
using Ozakboy.Line.Messaging.Actions;
using Ozakboy.Line.Messaging.Messages.Flex;

var card = new FlexBubble
{
    Size = FlexBubbleSize.Mega,
    Hero = new FlexImage("https://example.com/ramen.jpg")
    {
        Size = FlexSize.Full,
        AspectRatio = FlexAspectRatio.Of(20, 13),
        AspectMode = FlexAspectMode.Cover,
    },
    Body = new FlexBox
    {
        Layout = FlexLayout.Vertical,
        Spacing = FlexSpacing.Sm,
        Contents =
        [
            new FlexText("拉麵一番") { Weight = FlexWeight.Bold, Size = FlexSize.Xl },
            new FlexBox
            {
                Layout = FlexLayout.Baseline,
                Margin = FlexSpacing.Md,
                Contents =
                [
                    new FlexIcon("https://example.com/star.png") { Size = FlexSize.Sm },
                    new FlexText("4.5") { Size = FlexSize.Sm, Color = FlexColor.Rgb(0x99, 0x99, 0x99), Margin = FlexSpacing.Md, Flex = 0 },
                ],
            },
        ],
    },
    Footer = new FlexBox
    {
        Layout = FlexLayout.Vertical,
        Contents = [new FlexButton(new UriAction("https://example.com/book") { Label = "訂位" }) { Style = FlexButtonStyle.Primary }],
    },
};

var message = new FlexMessage("拉麵一番,評價 4.5", card);
await line.PushAsync(userId, [message]);   // 先跑 Validate();版面寫壞就一個請求都不送
```

所有帶值的欄位都是 `string` —— `"3xl"`、`"20px"`、`"50%"`、`"1.91:1"`、`"#112233CC"` —— 關鍵字與工廠放在靜態類別(`FlexSize`、`FlexSpacing`、`FlexColor`、`FlexAspectRatio`、`FlexBorderWidth`、`FlexCornerRadius`、`FlexLayout` …)。列舉裝不下這些值,而且 LINE 日後新增的每個值都得等套件發版。

**讀回 Simulator 的 JSON。**

```csharp
var read = FlexMessage.Parse("altText", jsonFromSimulator);   // bubble 或 carousel
var message = read.GetValueOrThrow();                          // message.Container 是強型別的節點樹
var raw = message.Container!.GetRawNodePaths();                // 模型在哪裡放棄,例如 "contents[1].body.contents[0]"
```

節點含有模型不認得的欄位、值的型別不符或 `type` 未知(已廢止的 `spacer` 也在內)時,**整個節點**以 `RawFlexComponent` / `RawFlexContainer` 保存並原樣寫回,所以讀回再寫出不掉資料。版面裡的動作讀回時是 `RawAction`。`FlexMessage.FromJson` 與 `JsonElement` 建構子維持原樣:JSON 原封不動送出。

**`Validate()` 檢查什麼**(`line.validation.invalid_flex`,訊息指出路徑,例如 `contents[2].body.contents[0].url`;一個請求都不送):

| 範圍 | 規則 |
| --- | --- |
| 輪播 | 1 到 12 個氣泡 |
| 氣泡 | header / hero / body / footer 至少一個;`size` 是七個關鍵字之一 |
| Hero 與影片 | hero 是 image、box 或 video;video 只能當 hero,`altContent` 是 image 或 box,動作是 URI 動作 |
| 方塊 | `layout` 必填且是 horizontal / vertical / baseline;baseline 方塊只能放 text、icon、filler;`flex` 不得為負 |
| 文字 | `text` 與 `contents`(span)恰好擇一;`maxLines` 不得為負 |
| 值 | size / align / gravity / weight / style / decoration / position / aspectMode / 按鈕 height 與 style / justifyContent / alignItems 的關鍵字;margin、spacing、padding、offset、width、height、cornerRadius、borderWidth 收關鍵字、`{n}px`,或(width、height、padding、offset)`{n}%`;顏色 `#RRGGBB` 或 `#RRGGBBAA`;長寬比 `w:h`;漸層角度 `{n}deg` |
| 媒體 | image、icon、video 位址是 https,不超過 2000 字 |
| 動作 | 各自走一般的動作檢查,label 最多 20 |
| 大小 | 版面 JSON 不超過 30 KB(輪播 50 KB),以未跳脫的 UTF-8 位元組計 |

**`RawFlexComponent`、`RawFlexContainer`、`RawAction` 與原樣 JSON 路徑不檢查**(整體大小除外):它們是 LINE 新增了模型還沒跟上的東西時的出路,所以從 Simulator 讀回的版面只驗到模型涵蓋的範圍,`GetRawNodePaths()`(或 MCP 工具 `line_validate_flex`)會告訴你在哪裡中斷。有兩件事刻意留給 LINE:含 video 的氣泡能不能放進輪播,以及本套件不知道的任何上限。訊息範本渲染出來的結果若含 `flex` 訊息,維持原狀 —— 渲染時不解析它 —— 想檢查可以用 `FlexMessage.Parse`。

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

規則可以用 `ChannelName` 綁定一個 channel;空值代表所有 channel,0.4.0 之前存下的規則讀回來也是空值,所以升級不會讓任何一條規則突然不回話。比對時只考慮適用於目前 channel 的規則 —— 上面的挑選順序不變 —— 回覆與 `{{displayName}}` 的查詢都走該 channel 的用戶端。`HandleAsync(evt)` 是預設 channel,`HandleAsync(evt, channelName)` 是其他 channel。

自動回覆一律走 **reply token** 而不是推播:回覆不計額度,而自動回覆是所有功能裡最會消耗額度的那一個。

---

## 群組與聊天室

```csharp
var summary = await line.GetGroupSummaryAsync(groupId);          // 名稱與圖片
var count = await line.GetGroupMemberCountAsync(groupId);        // 不含官方帳號自己
var who = await line.GetGroupMemberProfileAsync(groupId, userId); // 不要求是好友

string? next = null;
do
{
    var page = (await line.GetGroupMemberIdsAsync(groupId, next)).GetValueOrThrow();
    // …page.UserIds
    next = page.Next;
}
while (next is not null);

await line.LeaveGroupAsync(groupId);
```

聊天室有同一組方法,只少了摘要 —— 聊天室沒有名稱也沒有圖片。好友清單 `GetFollowerIdsAsync(start, limit)` 的分頁方式相同,三者都回 `LineUserIdsPage`,雖然 LINE 一邊叫 `memberIds`、一邊叫 `userIds`。好友清單與成員清單只開放給已認證或進階帳號,一般帳號 LINE 回 403 —— 那是帳號等級的事實,不是程式錯誤。

---

## 分眾推播與成效

分眾推播送給一部分好友,對象由你上傳的受眾或 LINE 推估的屬性決定。它是**非同步**的:呼叫回的是 LINE 的請求識別碼,有沒有送出、送給幾個人要看進度端點。

```csharp
var created = await line.CreateUploadAudienceGroupAsync("台北家長", userIds);   // 單次最多 10,000 人
await line.AddAudienceGroupMembersAsync(created.GetValueOrThrow().AudienceGroupId, moreUserIds);

var requestId = await line.NarrowcastAsync(
    [new TextMessage("有新的裁罰紀錄")],
    new LineNarrowcastOptions
    {
        Recipient = LineNarrowcastRecipient.And(
            LineNarrowcastRecipient.Audience(audienceGroupId),
            LineNarrowcastRecipient.Not(LineNarrowcastRecipient.Redelivery(previousRequestId))),
        MaxRecipients = 1000,
        RetryKey = jobId,
    });

var progress = await line.GetNarrowcastProgressAsync(requestId.GetValueOrThrow());
// progress.Phase:waiting → sending → succeeded | failed;SuccessCount、FailureCount、TargetCount
```

`Recipient` 與 `Demographic` 是 `JsonElement` 運算樹而不是完整模型 —— 兩者都是可巢狀的 `and` / `or` / `not`,而 LINE 還在為它們增加葉節點型別 —— 常用的形狀由 `LineNarrowcastRecipient` 組。剛建立的受眾是 `IN_PROGRESS`,要有足夠成員(LINE 目前要求 100 人)才會變 `READY`;`GetAudienceGroupAsync`、`GetAudienceGroupListAsync` 與 `DeleteAudienceGroupAsync` 補齊這一組。

`ValidateMessagesAsync(LineMessageValidationTarget.Push, messages)` 打 LINE 的驗證端點,只驗訊息物件、不送出、不計額度 —— 存範本之前用得上。本地上限先擋,LINE 的 400 回來時出錯的欄位在 `lineDetails`。

三個洞察端點看發生了什麼:`GetMessageDeliveryInsightAsync(date)` 看各管道的傳送數,`GetFollowersInsightAsync(date)` 看好友、可觸及與封鎖數,`GetDemographicInsightAsync()` 看性別、年齡、地區、作業系統與加好友時間。狀態不是 `ready` 時所有數字是 `null` —— 還沒統計或查不到 —— 而不是 0。

---

## 載入動畫與已讀

```csharp
await line.StartLoadingAnimationAsync(userId, loadingSeconds: 20);   // 5 到 60,5 的倍數
// …做慢的事;官方帳號一送出訊息動畫就停
await line.MarkAsReadAsync(userId);
```

動畫只在一對一聊天、而且使用者正開著那個聊天室時才看得到。秒數不在 5–60 或不是 5 的倍數,在本地就以 `line.validation.invalid_loading_seconds` 失敗。`MarkAsReadAsync` 只在帳號的已讀模式(`LineBotInfo.MarkAsReadMode`)是 `manual` 時有意義。

---

## textV2:提及與 emoji

`TextMessage` 可以用索引放 LINE emoji;`TextV2Message` 是較新的文字訊息,用 `{key}` 佔位符搭配 substitution 對照表 —— 要提及(mention)某個人,只有這個辦法。

```csharp
await line.PushAsync(groupId, [
    new TextV2Message("大家好 {everyone},{reporter} 回報了一筆新紀錄 {sparkle}")
    {
        Substitution =
        {
            ["everyone"] = LineTextSubstitution.MentionAll(),
            ["reporter"] = LineTextSubstitution.Mention(userId),
            ["sparkle"]  = LineTextSubstitution.Emoji("5ac1bfd5040ab15980c9b435", "001"),
        },
    },
]);

await line.PushAsync(userId, [
    new TextMessage("$ 新紀錄") { Emojis = { new LineTextEmoji(0, "5ac1bfd5040ab15980c9b435", "001") } },
]);
```

佔位符的位置由 LINE 自己找,前面有代理對或中日韓字元也不會像字元索引那樣偏掉。文字裡的每個 `{key}` 都必須在 `Substitution` 裡,每個鍵也至少要被引用一次;鍵只能是英數字與底線。違反任何一條,在本地就以 `line.validation.invalid_message` 失敗。收到的提及、emoji、引用訊息與多圖,在 `LineWebhookMessage` 上都是強型別(`Mention`、`Emojis`、`QuotedMessageId`、`ImageSet`)。

---

## 超量發送

推播一次最多 5 則訊息,群發一次最多 500 位收件人。`PushManyAsync` 與 `MulticastManyAsync` 收任意數量,替你切成合規的請求。

```csharp
var sent = await line.MulticastManyAsync(allUserIds, messages, new LinePushOptions { RetryKey = jobId });

if (sent.TryGetValue(out var ok))
{
    // ok.BatchCount, ok.TotalMessages, ok.SentMessages
}
else
{
    var error = sent.Error;
    // error.Data[LineBulkSendErrorDataKeys.SentBatches]       已經送出幾批
    // error.Data[LineBulkSendErrorDataKeys.FailedBatchIndex]  失敗的是哪一批,從 0 起算
}
```

- **各批依序送出,第一批失敗就停。** 錯誤保留失敗那一批自己的代碼與分類 —— 429 仍是 `RateLimited` —— 並附上 `sentBatches` 與 `failedBatchIndex`。群發的索引是 `收件人批次 × 訊息批數 + 訊息批次`。
- **整個輸入先驗證,再送第一批。** 第四批裡有一則壞訊息,是在第一批送出之前就發現,而不是之後。
- **零則訊息是 `too_many_messages`、零位收件人是 `line.validation.no_recipients`**,不會安靜地回成功。
- **`RetryKey` 逐批推導。** 每一批的鍵是 `SHA256(你的鍵 16 位元組 + 批次索引 4 位元組 big-endian)` 的前 16 位元組,再設上 UUID v5 與 RFC 4122 的位元(`LineBulkSendRetryKey.Derive`)。所以整個呼叫用同一個鍵重試時,不會重複 LINE 已經收下的批次;單批也一樣會推導。沒給 `RetryKey` 就完全不重試。
- **刻意沒有 `ReplyManyAsync`。** 回覆權杖只能用一次而且很快過期,回覆沒辦法分散到好幾個請求:`ReplyAsync` 一次最多 5 則,超過的部分該走推播。

---

## 統計與洞察

```csharp
var count = await line.GetMessageDeliveryCountAsync(LineMessageDeliveryType.Push, new DateOnly(2026, 10, 26));
// count.Status:Ready | Unready | OutOfService | Unknown;不是 Ready 時 count.Success 為 null

await line.PushAsync(userId, messages, new LinePushOptions { CustomAggregationUnits = ["autumn_sale"] });

var byUnit = await line.GetMessageEventInsightByUnitAsync("autumn_sale", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 30));
var bySend = await line.GetMessageEventInsightAsync(requestId);     // 單次發送的 X-Line-Request-Id
var used   = await line.GetAggregationUnitUsageAsync();             // 本月用了幾個單位
var names  = await line.GetAggregationUnitNamesAsync(limit: 100);
```

LINE 以 **JST(UTC+9)**計日,所以台灣時間凌晨 0 到 1 點算的是前一天。不是 `Ready` 的數字是 `null`,那不是 0。統計單位在送出前檢查:每次請求最多 1 個、最多 30 個英數字元或底線(`line.validation.invalid_aggregation_unit`);`CustomAggregationUnits` 在推播、群發、廣播與分眾推播都能用。`GetMessageEventInsightByUnitAsync` 要求 `from` 不晚於 `to`、相隔最多 30 天(`line.validation.invalid_date_range`)。

圖文選單另有批次端點 —— 一個請求替最多 1000 位使用者換連結或解除連結,回傳請求識別碼:

```csharp
var requestId = await line.ReplaceOrUnlinkRichMenusInBatchAsync([
    LineRichMenuBatchOperation.Link("richmenu-old", "richmenu-new"),
    LineRichMenuBatchOperation.Unlink("richmenu-retired"),
]);
var progress = await line.GetRichMenuBatchProgressAsync(requestId.GetValueOrThrow());
```

`ValidateRichMenuBatchAsync` 做同樣的檢查但不套用。作業數須為 1 到 1000(`line.validation.invalid_rich_menu_batch`)。

---

## Webhook 位址、帳號連結與訊息內容

```csharp
var current = await line.GetWebhookEndpointAsync();                 // 位址與是否啟用
await line.SetWebhookEndpointAsync("https://example.com/line/webhook");
var test = await line.TestWebhookEndpointAsync();                   // 或 TestWebhookEndpointAsync("https://…")
// test.GetValueOrThrow().Success / StatusCode / Reason —— 見下
```

位址必須是 https 且不超過 500 字元,否則在本地就以 `line.validation.invalid_webhook_endpoint` 失敗。**測試時對方伺服器沒回 2xx,呼叫本身仍算成功**:LINE 回報的是對方說了什麼,所以要看 `Success`、`StatusCode` 與 `Reason`,而不是 `Result`。

`IssueLinkTokenAsync(userId)` 回帳號連結流程導向時要帶的 link token。`GetMessageContentPreviewAsync(messageId)` 下載圖片或影片的縮圖,`GetMessageContentTranscodingStatusAsync(messageId)` 說明影片是否已可下載;套件不認得的狀態字串會以 `line.api.invalid_response` 失敗,而不是用猜的。

---

## LIFF

`ILineLiffClient` 讓你的伺服器管理一個 LINE Login 頻道下的 LIFF 應用程式。

```csharp
builder.Services.AddLineLiff(o =>
    o.ChannelAccessToken = builder.Configuration["Line:Login:ChannelAccessToken"]!);   // 是 LOGIN 頻道的權杖

var added = await liff.AddAsync(new LineLiffApp
{
    View = new LineLiffView { Type = LineLiffViewType.Tall, Url = "https://example.com/booking" },
    Description = "預約頁",
    BotPrompt = "normal",
});
var liffId = added.GetValueOrThrow();   // 開啟網址:https://liff.line.me/{liffId}

var all = await liff.GetAllAsync();     // 404(沒有任何應用程式)是成功的空清單
await liff.DeleteAsync(liffId);
```

⚠ **LIFF 用的是 LINE Login 頻道的權杖,不是 Messaging API 的。** 所以 `AddLineLiff` 有自己的 `LineLiffOptions`,也註冊自己的具名 `HttpClient`(`LineHttpClientNames.Liff`);把 Messaging 頻道的權杖交給它,是本套件偵測不出來的搞混,而 LINE 的拒絕也不會點出這件事。憑證空白時以 `line.not_configured` 失敗且不送出。`UpdateAsync(liffId, app)` 只送你有設的欄位、絕不送 `liffId`,不合規格的應用程式在本地以 `line.validation.invalid_liff_app` 失敗。另有 `AddLineLiff(IConfiguration)` 多載,從設定檔綁定選項。

---

## 多 channel

一個站台、多個官方帳號。每個 channel 用名稱註冊;未具名的註冊是**預設 channel**,名稱是空字串。

```csharp
builder.Services.AddLineMessaging("shop", o =>
{
    o.ChannelAccessToken = builder.Configuration["Line:Shop:ChannelAccessToken"]!;
    o.ChannelSecret = builder.Configuration["Line:Shop:ChannelSecret"]!;
});
builder.Services.AddLineMessaging("care", builder.Configuration.GetSection("Line:Care"));   // 或從設定檔區段綁定
builder.Services.AddLineLiff("shop", o => o.ChannelAccessToken = builder.Configuration["Line:Shop:LoginToken"]!);
```

```csharp
public sealed class Notifier(ILineMessagingClientFactory lines)
{
    public Task NotifyAsync(string channel, string userId, string text) =>
        lines.Get(channel).PushTextAsync(userId, text);     // 名稱沒註冊時 Get 會擲例外
}
```

- `ILineMessagingClientFactory` 與 `ILineLiffClientFactory` 提供 `Get(name)`、`Contains(name)` 與 `ChannelNames`。同一個名稱永遠拿到同一個 singleton 用戶端;`ILineMessagingClient.ChannelName` 說明這個用戶端綁在哪個 channel(預設是空字串)。
- **具名**註冊只註冊成 keyed 服務(`GetRequiredKeyedService<ILineMessagingClient>(name)`);未具名的則同時是 keyed `""` 與一般的 `ILineMessagingClient`,而且是同一個實例。同名再註冊一次會**整個取代**先前那個,換 channel 時舊的密鑰不會殘留。
- 每個 channel 有自己的具名 `HttpClient`(`LineHttpClientNames.ForMessaging(name)` / `ForLiff(name)`),`line.not_configured` 也是逐 channel 成立:沒給憑證的具名 channel 什麼都不送。

### 每個 channel 一條 webhook 路由

```csharp
app.MapLineWebhook(
    "/line/webhook/{channel}",
    handler: async (payload, http, ct) => { /* … */ },
    new LineWebhookEndpointOptions { ChannelRouteParameter = "channel" });   // 或用 ChannelName = "shop" 固定一個
```

密鑰從 `IOptionsMonitor<LineMessagingOptions>.Get(name)` 取,所以每個 channel 各驗各的。**名稱不在 `ChannelNames` 內,在碰任何東西之前就回 401** —— 不讀內容、不取選項,也絕不拿空密鑰去驗簽。簽章驗過之後,名稱放在 `HttpContext.Items[LineWebhookItems.ChannelName]`(預設 channel 是空字串),處理常式的 `LineWebhookContext.ChannelName` 帶著它,`LineWebhookContext.Messaging` 就是該 channel 的用戶端。`ChannelName` 與 `ChannelRouteParameter` 互斥,兩者都設會在掛端點當下擲例外。兩者都不設時完全沒變:仍是預設 channel。自動回覆、維護中的回覆與錯誤通知,都走事件所屬的那個 channel。

---

## 事件處理常式

繼承 `LineWebhookHandler`,不必自己寫那串 `if`。

```csharp
public sealed class ShopHandler(IBookingService bookings) : LineWebhookHandler
{
    protected override Task OnTextMessageAsync(LineWebhookEvent evt, LineWebhookContext context, CancellationToken ct) =>
        context.ReplyTextAsync($"收到:{evt.Message!.Text}", ct);

    protected override async Task OnPostbackAsync(LineWebhookEvent evt, LineWebhookContext context, CancellationToken ct)
    {
        var data = await context.ResolvePostbackDataAsync(ct);          // 暫存代碼會還原,一般資料原樣回傳
        if (data.IsSuccess)
        {
            await bookings.HandleAsync(evt.Source.UserId!, data.GetValueOrThrow(), ct);
        }
    }

    protected override Task OnFollowAsync(LineWebhookEvent evt, LineWebhookContext context, CancellationToken ct) =>
        context.ReplyTextAsync("歡迎加入!", ct);
}
```

```csharp
builder.Services.AddLineWebhookHandler<ShopHandler>();      // scoped,可以直接注入 DbContext
app.MapLineWebhook<ShopHandler>("/line/webhook");           // 或 (…, new LineWebhookEndpointOptions { … })
```

- message 事件先進 `OnMessageAsync`,再進細分的方法(`OnTextMessageAsync`、`OnImageMessageAsync` …);這個版本不認得的型別走 `OnUnknownEventAsync`。共有 21 個 `On*Async`,預設都不做事。
- 基底類別不吞例外;隔離是端點的責任,所以狀態碼契約不變 —— 驗簽失敗 401、內容讀不懂 400、其餘一律 200。
- `context.ReplyAsync` / `ReplyTextAsync` 在一個事件上**只能送一次**。任何通過本地檢查而送出的回覆都算用掉權杖,即使 LINE 隨後拒絕;第二次在本地回 `line.webhook.reply_token_used` 且不送,沒有權杖的事件回 `line.webhook.no_reply_token` —— 兩者都不擲例外。要多送請走 `context.Messaging` 推播。
- `context.Items` 是這個事件專用的暫存字典,`context.AutoReplyOutcome` 是自動回覆對這個事件做了什麼。

---

## 維護模式與錯誤通知

```csharp
app.MapLineWebhook<ShopHandler>("/line/webhook", new LineWebhookEndpointOptions
{
    Maintenance =
    {
        Enabled = true,
        AdminUserIds = { "Uadmin…" },
        ReplyText = "系統維護中,請稍後再試。",
    },
    ErrorNotification =
    {
        Enabled = true,
        AdminUserIds = { "Uadmin…" },
        IncludeStackTrace = false,
    },
});
```

- **維護模式**看 `Source.UserId`,群組事件也一樣;沒有 `UserId` 的事件視為非管理員。被擋下的事件既不進處理常式也不跑自動回覆,以 `ReplyText` 回覆 —— 除非它沒有回覆權杖或 `mode` 是 `standby`(別的模組正在接手對話),那種情況就直接丟掉不回。本次請求有事件被擋下時,`HttpContext.Items[LineWebhookItems.MaintenanceSkipped]` 為 `true`。
- **錯誤通知**在處理常式擲出例外時,推播簡短說明給每一位管理員。每一次推播各自失敗,整體上限十秒、整則不超過 5000 字,而且絕不改變 200。
- 選項在掛端點當下複製一份。任一功能開啟卻白名單是空的、`ReplyText` 空白,或處理常式 / 用戶端沒有註冊,掛端點當下就擲例外 —— 在啟動時,而不是第一位使用者傳訊息時。兩者都適用逐事件的多載與 `MapLineWebhook<THandler>`;整批內容的多載由你自己分工。

---

## postback 超長資料

`PostbackAction` 的資料上限是 300 字元。把長資料暫存進儲存體,按鈕上只放一個短代碼。

```csharp
builder.Services.AddLinePostbackData();                                              // 記憶體
// builder.Services.AddLinePostbackData(o => o.UseJsonFile("data/line-postbacks.json"));   // 重啟後仍在

var action = await PostbackAction.CreateStoredAsync(store, "查看詳情", longJson, displayText: "查看詳情");
// …之後在處理常式裡:
var data = await context.ResolvePostbackDataAsync(ct);    // 找不到或超過有效期限時為 line.postback.expired
```

- 代碼是 `ozl:` 加 32 個小寫十六進位字元,共 36 字;前綴與長度是固定的。`CreateStoredAsync` 連短資料也照樣暫存,呼叫端不必再依長度分支。
- 儲存體找不到的代碼與已過期的代碼是**同一個**失敗 `line.postback.expired`。資料超過儲存體上限(`LinePostbackDataOptions.MaxDataLength`)以 `line.validation.postback_data_too_long` 失敗。`ILinePostbackDataStore.PurgeExpiredAsync` 清掉過期的項目。
- 儲存體一律是 singleton(JSON 版實作 `IDisposable`)。還原出來的值和一般的 `Data` 一樣**沒有簽章**:不要只靠它判斷誰能做什麼 —— 要拿事件的 `Source.UserId` 回頭查自己的資料。

---

## 單圖 imagemap(實驗性)

一般的 imagemap 需要同一張圖準備 240 / 300 / 460 / 700 / 1040 像素寬的多種尺寸。`ImagemapMessage.ForSingleImage` 只用一張圖,整張都是點擊範圍。

```csharp
var map = ImagemapMessage.ForSingleImage(
    "https://example.com/banner.png", "春季活動", height: 520,
    new ImagemapUriAction("https://example.com/spring", new LineImagemapArea(0, 0, 1040, 520)));   // 範圍會被換成整張圖
await line.PushAsync(userId, [map.GetValueOrThrow()]);
```

位址會補上一個 `?`(已經帶 query 的補 `&`),尾端本來就是 `?` 或 `&` 就不補,含 `#` 的位址直接拒絕。**這依賴 LINE 會在底圖位址後面接上 `/{width}`、而你的伺服器會忽略多出來的 query;LINE 沒有保證過這件事,所以標為實驗性。**要依賴它之前請先在真機上確認。底圖寬度固定是 1040,原圖至少要有這麼寬。

---

## 送出之前檢查了什麼

下表每一條都在你的機器上以具名代碼失敗,而且**一個請求都不送** —— LINE 自己對其中大多數的回應,是一個只說「請求內容有 1 個錯誤」的 400。

| 項目 | 上限 | 代碼 |
| --- | --- | --- |
| 每次請求的訊息數 | 1 到 5 | `too_many_messages` |
| 群發收件人 | 1 到 500 | `too_many_recipients` |
| 快速回覆按鈕 | 1 到 13 | `too_many_quick_reply_items` |
| `text` / `textV2` | 至多 5000 字元 | `invalid_message` |
| `altText` | 至多 400 字元 | `invalid_message` |
| 位置的 `title` / `address` | 各至多 100 字元 | `invalid_message` |
| 媒體網址 | https,至多 2000 字元 | `invalid_message` |
| 色碼 | `#RRGGBB` | `invalid_message` |
| `textV2` 佔位符 | 每個 `{key}` 都在 `Substitution`、每個鍵都被引用 | `invalid_message` |
| 動作 `label` | 至多 20(圖片地圖 50、圖片輪播 12) | `invalid_action` |
| `postback` data | 至多 300 字元 | `invalid_action` |
| `uri` 動作 | 協定須在白名單內 | `invalid_action` |
| Flex 版面 | 結構、值的格式、位址與大小,訊息指出路徑;Raw 節點不檢查 | `invalid_flex` |
| 圖文選單區塊動作 | 逐一檢查,訊息指出是哪個區塊 | `invalid_action` |
| 範本與圖片地圖的形狀 | 見上文 | `invalid_template` / `invalid_imagemap` |
| 統計單位 | 每次請求 1 個、30 個英數字元或底線 | `invalid_aggregation_unit` |
| 統計區間 | `from` ≤ `to`、最多 30 天 | `invalid_date_range` |
| 圖文選單批次 | 1 到 1000 個作業 | `invalid_rich_menu_batch` |
| Webhook 位址 | https,至多 500 字元 | `invalid_webhook_endpoint` |
| LIFF 應用程式 | 規格要求的必填欄位 | `invalid_liff_app` |
| 超量發送 | 至少一則訊息、至少一位收件人 | `too_many_messages` / `no_recipients` |

`RawMessage` 與 `RawAction` **不驗**:它們是 LINE 新增了本套件還沒跟上的東西時的出路,內容對不對由你負責。MCP 工具讀進來的圖文選單也一樣,它的動作一律是 `RawAction`。

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

**鐵則:AI 不得直接發送。** 預設的 `Review` 模式下,`line_send_push` / `line_send_multicast` / `line_send_broadcast` / `line_replace_rich_menu` / `line_create_liff_app` / `line_delete_liff_app` 什麼都不會執行 —— 它們把內容寫進待發佇列,狀態 `PendingReview`,等宿主的人核准。核准的 API(`ILineMcpOutboxService.ApproveAndSendAsync`)**不是任何一個工具**,只給宿主程式呼叫:待審制度的價值就在於提案的一方沒有核准權。`Direct` 模式會直接送,但仍然寫一筆紀錄 —— 沒有紀錄的話,事後沒有任何地方查得出「那則訊息是誰、什麼時候、為什麼發的」。

每日還有一道建立上限(預設 10 筆,依 `TimeZoneId` 的當地日期計算)。這是防暴走的閘:AI 在迴圈裡重試同一個工具是很常見的失敗模式,沒有上限時那個迴圈會把待審佇列灌到沒有人願意去看。

**金鑰閘。** 對外網址是 `{prefix}/{ApiKey}`(預設 prefix 為 `/mcp/line`),金鑰走路徑而不是標頭,因為多數 MCP 連接器只給使用者一個填網址的欄位。金鑰以定時比較驗證,**失敗一律回 404 而不是 401/403** —— 401/403 等於告訴對方「這裡確實有東西」,而這個端點的存在本身就不必對外說。沒設定 `ApiKey` = 功能關閉,整個子樹都是 404;金鑰輪替 = 改設定重啟。

`UseLineMcpWellKnownNotFound()` 讓 `/.well-known/` 一律回乾淨的 JSON 404:MCP 連接器連線前會探測 `/.well-known/oauth-protected-resource`,那些請求若落到 MVC 拿到 HTML 登入頁或 302,連接器會判定這個站需要 OAuth 而連不上。⚠ 這道閘攔的是**整個 `/.well-known` 子樹**,日後要放 `security.txt` 得在這一行之前處理掉。

一個 MCP 端點對應一個 channel:`LineMcpOptions.ChannelName`(沒設就是空字串,即預設 channel)決定所有工具與待發服務用哪個官方帳號的用戶端,`line_get_bot_info` 會以 `channelName` 回報它。名稱沒有註冊時,所有工具回 `line.not_configured` 且什麼都不送 —— 絕不退回別的帳號,送出類工具也在提出時就拒絕,而不是排進一個永遠核准不了的佇列。要操作多個帳號,各掛一個端點、各用自己的金鑰。預設 channel 優先用直接註冊的 `ILineMessagingClient`,所以沒有工廠的舊宿主照常可用。`line_upsert_auto_reply` 可選填規則的 `channelName`,`line_test_auto_reply` 可以拿一段文字試跑某個 channel 的規則。

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
| 查詢 | `line_get_bot_info` · `line_get_quota` · `line_get_profile` · `line_get_webhook_info` · `line_test_webhook` · `line_get_delivery_stats` · `line_get_message_event_insight` |
| 送出(走待審) | `line_send_push` · `line_send_multicast` · `line_send_broadcast` |
| 待發佇列 | `line_list_outbox` · `line_get_outbox` · `line_cancel_outbox` |
| 圖文選單 | `line_list_rich_menus` · `line_get_default_rich_menu` · `line_list_rich_menu_aliases` · `line_validate_rich_menu` · `line_replace_rich_menu`(走待審) |
| 自動回覆 | `line_list_auto_replies` · `line_upsert_auto_reply` · `line_delete_auto_reply` · `line_test_auto_reply` |
| 訊息範本 | `line_list_templates` · `line_get_template` · `line_upsert_template` · `line_delete_template` · `line_render_template` |
| Flex | `line_validate_flex` |
| LIFF | `line_list_liff_apps` · `line_create_liff_app`(走待審)· `line_delete_liff_app`(走待審) |

規則與範本的工具**不走待審**,因為它們不會自己送訊息:改壞一條規則的後果是回錯話,而不是主動打擾全體好友,改回來也只是再 upsert 一次。要完全禁止的話有 `AllowRuleEdits` / `AllowTemplateEdits` / `AllowRichMenuChanges` 三個開關。宿主沒有 `AddLineTemplates` / `AddLineAutoReply` 時,那幾個工具仍然掛得上,只是呼叫時回一句「未啟用」—— 這比工具根本不出現清楚。

`line_validate_flex` 同樣唯讀:給它一份 Flex 版面(bubble、carousel 或整則 `flex` 訊息),它回答能否解析為強型別、哪些節點仍是 Raw(因此不被驗證)、帶版面路徑的本地驗證結果,以及 JSON 大小與其上限 —— 讓外部 AI 在排進佇列之前先自檢。它不送出任何東西、不計每日額度。

0.3.0 的四個新讀取工具(`line_get_webhook_info`、`line_test_webhook`、`line_get_delivery_stats`、`line_get_message_event_insight`)不改變任何東西,也不計每日額度。`line_get_delivery_stats` 收一個 `yyyy-MM-dd` 日期,一次回覆、推播、群發、廣播四種管道;統計尚未備妥的管道回報 `unready` 且數量為 null,不是 0。LIFF 工具需要 `ILineLiffClient`,宿主沒呼叫 `AddLineLiff` 時回 `line.not_configured`。**建立與刪除 LIFF 應用程式與 `line_replace_rich_menu` 一樣走待審佇列**:`Review` 模式下只把計畫排進佇列(待發種類 `LiffCreate` 與 `LiffDelete`),由人核准後 `ILineMcpOutboxService.ApproveAndSendAsync` 才呼叫 `ILineLiffClient` —— 刪除會讓所有指向它的連結失效,不能讓 AI 單獨決定。`Direct` 模式先執行再記錄,執行時寫一行日誌;未呼叫 `AddLineLiff` 時核准會把項目標為 `Failed`(`line.not_configured`)。`line_list_liff_apps` 仍是直接、唯讀的查詢。三個送出工具多了選填的 `customAggregationUnit`,提出時檢查、隨待發項目一起存,核准時才套用。

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
| 好友、群組與聊天室 | `GetFollowerIdsAsync` · `GetGroupSummaryAsync` · `GetGroupMemberCountAsync` · `GetGroupMemberIdsAsync` · `LeaveGroupAsync` · `GetGroupMemberProfileAsync` · `GetRoomMemberCountAsync` · `GetRoomMemberIdsAsync` · `LeaveRoomAsync` · `GetRoomMemberProfileAsync` |
| 聊天與驗證 | `StartLoadingAnimationAsync` · `MarkAsReadAsync` · `ValidateMessagesAsync` · `ValidateMessagesRawJsonAsync` |
| 分眾推播與受眾 | `NarrowcastAsync` · `GetNarrowcastProgressAsync` · `CreateUploadAudienceGroupAsync` · `AddAudienceGroupMembersAsync` · `GetAudienceGroupAsync` · `GetAudienceGroupListAsync` · `DeleteAudienceGroupAsync` |
| 成效洞察 | `GetMessageDeliveryInsightAsync` · `GetFollowersInsightAsync` · `GetDemographicInsightAsync` |
| 超量發送 | `PushManyAsync` · `MulticastManyAsync`(配 `LineBulkSendResult`、`LineBulkSendRetryKey.Derive`、`LineBulkSendErrorDataKeys`) |
| 統計與統計單位 | `GetMessageDeliveryCountAsync` · `GetMessageEventInsightAsync` · `GetMessageEventInsightByUnitAsync` · `GetAggregationUnitUsageAsync` · `GetAggregationUnitNamesAsync`(單位經 `LinePushOptions.CustomAggregationUnits` 設定,分眾推播也有) |
| 圖文選單批次 | `ReplaceOrUnlinkRichMenusInBatchAsync` · `ValidateRichMenuBatchAsync` · `GetRichMenuBatchProgressAsync`(配 `LineRichMenuBatchOperation.Link` / `Unlink` / `UnlinkAll`) |
| Webhook 位址 | `GetWebhookEndpointAsync` · `SetWebhookEndpointAsync` · `TestWebhookEndpointAsync` |
| 帳號連結與內容 | `IssueLinkTokenAsync` · `GetMessageContentPreviewAsync` · `GetMessageContentTranscodingStatusAsync` |
| 身分 | `ChannelName`(用戶端綁定的 channel;預設為空字串) |

訊息型別:`TextMessage`(含 `Emojis`)、`TextV2Message`(配 `LineTextSubstitution`)、`ImageMessage`、`VideoMessage`、`AudioMessage`、`LocationMessage`、`StickerMessage`、`FlexMessage`(強型別版面在 `Ozakboy.Line.Messaging.Messages.Flex`,見上文)、`TemplateMessage`(配 `ButtonsTemplate`、`ConfirmTemplate`、`CarouselTemplate`、`ImageCarouselTemplate`)、`ImagemapMessage`(配 `ImagemapUriAction`、`ImagemapMessageAction`、`LineImagemapVideo`),以及給尚未建模的東西用的 `RawMessage`。每則訊息都有 `QuickReply`、`Sender` 與 `Validate()`。動作:`UriAction`、`MessageAction`、`PostbackAction`、`DatetimePickerAction`、`CameraAction`、`CameraRollAction`、`LocationAction`、`ClipboardAction`、`RichMenuSwitchAction`、`RawAction`。快速回覆:`LineQuickReply` / `LineQuickReplyItem`。

`ReplaceRichMenuAsync` 把「建立 → 上傳圖片 → 設預設 → 指向別名 → 刪舊選單」串成一次呼叫。失敗處理刻意不對稱:**圖片上傳失敗會刪掉剛建的新選單**(沒有圖片的選單比沒有選單更糟),但**刪舊選單失敗只記錄、整體仍算成功**(新選單已經上線,回報失敗只會讓呼叫端重做而多出一個選單)。

`*RawJsonAsync` 這組多載接受單一訊息物件或訊息陣列,兩種都會被包成 `{"messages": […]}`。LINE 新增了本套件還沒跟上的東西時,那就是出口。

### `ILineLiffClient`

| 成員 | 作用 |
| --- | --- |
| `AddAsync(app)` | 建立 LIFF 應用程式,回傳 `liffId`。 |
| `UpdateAsync(liffId, app)` | 修改有設定的欄位;絕不送 `liffId`。 |
| `GetAllAsync()` | 列出應用程式;404 是成功的空清單。 |
| `DeleteAsync(liffId)` | 刪除應用程式。 |

以 `AddLineLiff(options)` 或 `AddLineLiff(configuration)` 註冊,走自己的具名 `HttpClient`,用 Login 頻道的權杖。

### 多 channel、事件處理常式與 postback 暫存

| 成員 | 做什麼 |
| --- | --- |
| `AddLineMessaging(name, …)` · `AddLineLiff(name, …)` | 註冊具名 channel(只註冊 keyed);同名再註冊會取代。 |
| `ILineMessagingClientFactory` · `ILineLiffClientFactory` | `Get(name)` · `Contains(name)` · `ChannelNames`;空字串是預設 channel。 |
| `LineHttpClientNames.ForMessaging(name)` · `ForLiff(name)` | 某個 channel 的具名 `HttpClient`。 |
| `LineWebhookHandler` | 有 21 個 `On*Async` 的抽象基底;message 事件先進 `OnMessageAsync`,不認得的型別走 `OnUnknownEventAsync`。 |
| `LineWebhookContext` | `Event` · `ChannelName` · `Messaging` · `AutoReplyOutcome` · `Items` · `ReplyAsync` · `ReplyTextAsync` · `ResolvePostbackDataAsync`。 |
| `AddLineWebhookHandler<T>()` · `MapLineWebhook<T>(pattern, options?)` | 註冊處理常式(scoped)並掛上分派給它的端點。 |
| `LineWebhookEndpointOptions` | `AutoReply` · `ChannelName` · `ChannelRouteParameter` · `Maintenance` · `ErrorNotification`。 |
| `ILinePostbackDataStore` | `StoreAsync` · `GetAsync` · `PurgeExpiredAsync`,記憶體或 JSON 檔實作;以 `AddLinePostbackData` 註冊。 |
| `PostbackAction.CreateStoredAsync` · `LinePostbackDataResolver.ResolveAsync` · `LinePostbackDataToken` | 把長資料暫存在短代碼後面,再把代碼還原成資料。 |
| `ImagemapMessage.ForSingleImage(url, altText, height, action)` | 單圖 imagemap(實驗性)。 |

### Flex 訊息

| 成員 | 做什麼 |
| --- | --- |
| `FlexBubble` · `FlexCarousel` | 兩種容器;`FlexCarousel.Contents` 放 1 到 12 個氣泡。 |
| `FlexBox` · `FlexText`(+ `FlexSpan`)· `FlexImage` · `FlexVideo` · `FlexIcon` · `FlexButton` · `FlexSeparator` · `FlexFiller` | 元件;所有帶值的欄位都是 `string`。 |
| `FlexLinearGradient` · `FlexBubbleStyles` · `FlexBlockStyle` | 方塊漸層與各區域的氣泡樣式。 |
| `FlexSize` · `FlexSpacing` · `FlexColor` · `FlexAspectRatio` · `FlexBorderWidth` · `FlexCornerRadius` · `FlexLayout` · … | 關鍵字常數與量值工廠(`Px`、`Percent`、`Rgb`、`Of`)。 |
| `new FlexMessage(altText, container)` · `FlexMessage.Container` | 強型別路徑;原樣 JSON 路徑的 `Container` 為 `null`。 |
| `FlexMessage.Parse(altText, json)` | 把版面 JSON 讀成強型別模型;不認得的部分保留為 Raw。 |
| `FlexContainer.GetRawNodePaths()` | 以 `RawFlexComponent` / `RawFlexContainer` 保存的節點路徑。 |
| `RawFlexContainer` · `RawFlexComponent` | 逃生口;原樣送出、不驗證。 |
| `FlexMessage.Validate()` | 本地檢查;`line.validation.invalid_flex` 並附路徑。 |

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
| `LineAutoReplyMatcher.ForChannel(rules, channelName)` | 適用於某個 channel 的規則:`ChannelName` 為空,或與之相同。 |
| `ILineAutoReplyService.HandleAsync(evt)` | 把規則套用到預設 channel 的一個 webhook 事件:`Skipped`、`NoMatch` 或 `Replied`。 |
| `ILineAutoReplyService.HandleAsync(evt, channelName)` | 同上,但針對其他 channel;名稱未註冊為 `line.not_configured`。 |

### MCP(`Ozakboy.Line.Mcp`)

| 成員 | 做什麼 |
| --- | --- |
| `AddLineMcp(configure, stores?)` | 設定、待發佇列、待發項目服務、抓圖用的具名 HTTP 用戶端。 |
| `AddLineMcpServer(configure, stores?)` | 上面全部,加一個只掛 LINE 工具的 stateless MCP server。 |
| `LineMcpOptions.ChannelName` · `LineMcpClients` | 工具與待發服務用哪個 channel 的用戶端;channel 不存在為 `line.not_configured`。 |
| `IMcpServerBuilder.WithLineTools()` | 把八組工具加進宿主既有的 MCP server。 |
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
| `MapLineWebhook(pattern, handler, options)` | 整批內容的多載,可帶 `LineWebhookEndpointOptions`;其中只有 `ChannelName` 與 `ChannelRouteParameter` 生效。 |
| `LineWebhookItems.AutoReplyOutcome` | 自動回覆結果在 `HttpContext.Items` 裡的鍵名。 |
| `LineWebhookItems.ChannelName` | 驗簽通過的請求所屬的 channel(預設為空字串);驗簽通過之後才放值。 |
| `LineWebhookItems.MaintenanceSkipped` | 本次請求有事件被維護模式擋下時為 `true`。 |
| `LineWebhookEvent.Joined` / `Left` / `Beacon` / `Unsend` / `Link` / `Membership` | 成員加入與離開、beacon、收回訊息、帳號連結與會員事件的強型別內容(字串由 `LineWebhookBeaconTypes`、`LineWebhookLinkResults`、`LineWebhookMembershipTypes` 命名)。 |
| `LineWebhookMessage.Emojis` / `Mention` / `ImageSet` / `QuotedMessageId` / `MarkAsReadToken` / `StickerResourceType` / `Keywords` | 強型別的訊息欄位;沒建模的東西仍在 `Raw`。 |

### 錯誤代碼

`line.not_configured` · `line.validation.too_many_recipients` · `line.validation.too_many_messages` · `line.validation.invalid_json` · `line.validation.too_many_quick_reply_items` · `line.validation.invalid_template` · `line.validation.invalid_imagemap` · `line.validation.invalid_loading_seconds` · `line.validation.invalid_page_size` · `line.validation.too_many_audience_members` · `line.validation.missing_template_variables` · `line.validation.invalid_auto_reply_rule` · `line.validation.invalid_message` · `line.validation.invalid_action` · `line.validation.invalid_webhook_endpoint` · `line.validation.invalid_liff_app` · `line.validation.invalid_date_range` · `line.validation.invalid_aggregation_unit` · `line.validation.invalid_rich_menu_batch` · `line.validation.no_recipients` · `line.validation.invalid_flex` · `line.mcp.outbox_not_found` · `line.mcp.outbox_invalid_status` · `line.mcp.image_fetch_failed` · `line.api.error` · `line.api.invalid_response` · `line.id_token.invalid_format` · `line.id_token.unsupported_algorithm` · `line.id_token.invalid_signature` · `line.id_token.invalid_issuer` · `line.id_token.invalid_audience` · `line.id_token.expired` · `line.id_token.nonce_mismatch` · `line.webhook.invalid_signature` · `line.webhook.invalid_payload` · `line.webhook.no_reply_token` · `line.webhook.reply_token_used` · `line.webhook.not_postback` · `line.postback.expired` · `line.validation.postback_data_too_long`

這些字串是公開契約,發佈後不再更動。

---

## 相容性

- **目標框架:** net10.0。
- **相依:** `Ozakboy.Core.Abstractions`、`Ozakboy.Http` 與 `Microsoft.Extensions.*`。`Ozakboy.Line.AspNetCore` 另外參照共用框架 `Microsoft.AspNetCore.App`。核心與整合兩個套件的遞移相依裡沒有任何第三方套件,也沒有任何 JWT 函式庫。
- **MCP 套件的相依例外:** `Ozakboy.Line.Mcp` 相依 `ModelContextProtocol.AspNetCore` —— MCP 的官方 C# SDK,由 modelcontextprotocol 組織與 Microsoft 共同維護。這是相依政策的**唯一明列例外**,理由是 MCP 是一份有版本的線路協定,自己實作等於自己維護一份會隨協定改版而落後的實作,而落後的症狀是「連接器連得上但工具清單是空的」。不想帶這條相依的話,不裝這個套件即可,核心與整合套件完全不受影響。
- **測試:** 全程離線。測試一次都不連 LINE,憑證一律是佔位符。

## 授權

MIT,見 [LICENSE](LICENSE)。
