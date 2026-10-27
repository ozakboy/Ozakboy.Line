# Ozakboy.Line

> 建立日期:2026-09-15(claude-harness 標準架構,起手式比照 `Ozakboy.Http`)。全域規範(語言、C# 慣例、機密、禁令)見 `~/.claude/CLAUDE.md`;**發佈流程(`uplog`)一律走 `nuget-release` skill**,此檔只放本專案特有內容。
>
> NuGet 套件 ID:**`Ozakboy.Line`**(核心)、**`Ozakboy.Line.AspNetCore`**(整合)與 **`Ozakboy.Line.Mcp`**(MCP 工具組)。定位:LINE Login + Messaging API + Webhook 的 .NET 用戶端,附 ASP.NET Core 認證方案與 webhook 端點。程式碼抽取自幼明燈(`D:\程式專案\個人專案\YouMingDeng`)的實戰經驗,但兩邊不共用程式碼。

## ⛔ 鐵則 — 不開分支,一律在 `main` 上工作

1. 禁止建立任何新分支(feature / fix / topic)、禁止開 PR
2. 若 session 啟動於 Claude Code worktree branch(`claude/xxx-yyy`)→ 第一件事切回主 repo 的 `main` 工作,完成後告知使用者可清理 worktree
3. 所有變更直接 commit 到 `main`;唯一例外:使用者明確指示「開分支 / 開 PR」
4. `git push` 須使用者明確授權;**唯一常駐授權場景:執行 `uplog` 時的 push**
5. commit 訊息走 Conventional Commits(全域規範),描述用中文說明「為什麼」

## 技術棧與相依政策

- C# 函式庫,TFM:**`net10.0` 單目標**。不做 netstandard 多目標(這個套件的消費端是 ASP.NET Core 應用,沒有舊框架需求),因此 `record` / `init` / `required` / file-scoped namespace / collection expressions 全部可用。
- **相依政策:只允許 .NET BCL、Microsoft 官方套件與 `Ozakboy.*` 自研套件,判定看「遞移相依」而非套件名稱。**
  - HTTP 一律走 `Ozakboy.Http`(0.3.3),不自己組 `HttpClient` 管線;失敗表達走 `Ozakboy.Core.Abstractions`(0.3.0)的 `Result` / `Result<T>`。
  - **不准加 JWT 函式庫。** id_token 是 HS256、金鑰就是 channel secret,BCL 的 `HMACSHA256` + `Base64Url` 已足夠(見 `LineIdTokenValidator`)。
  - **不准加 Newtonsoft.Json。** JSON 一律 `System.Text.Json`,共用設定在 `LineJson.Options`。
  - 測試專案走 MSTest 4.x + Microsoft.Testing.Platform,**不走 VSTest**(那條路徑會遞移帶進 Newtonsoft.Json);repo 根目錄的 `global.json` 的 `test.runner` 是關鍵。
  - 刻意不引用 `Microsoft.SourceLink.GitHub`(SDK 內建,外掛套件會帶進有安全通報的相依)。
  - **相依政策的唯一明列例外:`Ozakboy.Line.Mcp` 可以相依 `ModelContextProtocol.AspNetCore`(2.2.0)。**
    它是 MCP 的官方 C# SDK,由 modelcontextprotocol 組織與 Microsoft 共同維護。破例的理由是 MCP 是一份
    **有版本的線路協定**(JSON-RPC over Streamable HTTP),自己實作等於自己維護一份會隨協定改版而落後的實作,
    而落後的症狀是「連接器連得上但工具清單是空的」。這個例外**只適用於 Mcp 專案**,
    核心與 AspNetCore 兩個套件的相依政策不變(改動時請確認沒有把這條相依滲進去)。
- 建置品質:`TreatWarningsAsErrors` + `AnalysisLevel=latest-all` + `EnforceCodeStyleInBuild` + `Features=strict`(在 `Directory.Build.props`)。**主函式庫零警告**;測試專案保留分析器但不因警告中斷。
- XML doc:所有 public / internal 成員**中英雙語(先中後英)**;程式碼註解繁體中文(台灣用語),識別字英文。

## 目錄結構

```
Ozakboy.Line/
  Directory.Build.props  global.json  Ozakboy.Line.sln  logo.png  LICENSE
  README.md  README_zh-TW.md  CHANGELOG.md  CLAUDE.md
  .github/workflows/build.yml
  src/Ozakboy.Line/                     核心(PackageId=Ozakboy.Line)
    Line{Endpoints,ErrorCodes,ErrorDataKeys,MessagingLimits,Json,Http,ApiErrorMapper}.cs
                  (Endpoints / ErrorCodes / MessagingLimits 另拆 partial:.Channel / .Liff / .Stats / .RichMenuBatch /
                  .Batch / .Messages / .Postback / .Webhook,依功能分檔)
    LineUserProfile.cs  LineHttpClientNames.cs  LineServiceCollectionExtensions.cs
    LineChannel{Names,Registrations}.cs(多 channel:具名登記與「同名取代」的共用工具,0.4.0)
    Login/        LineLoginOptions / LineLoginClient / LineIdTokenValidator / LinePkce / 回應模型
    Messaging/    LineMessagingOptions / LineMessagingClient(+ .Chat / .Narrowcast / .Insight / .Channel / .Stats /
                  .RichMenuBatch / .Bulk 七個 partial,ILineMessagingClient 同樣分檔,另有 .Identity 放 ChannelName)
                  ILineMessagingClientFactory / LineMessagingClientFactory(多 channel 的工廠)
                  回應模型 / LinePushOptions / LineQuickReply(+Item)/ LineMessageSender / LineUserIdsPage
                  LineGroupSummary / LineMessageValidationTarget / LineWebhookEndpointInfo / LineWebhookTestResult
                  LineBulkSend{Result,RetryKey,ErrorDataKeys}
      Messages/   LineMessage 與十種具體訊息(含 TemplateMessage、ImagemapMessage、TextV2Message)+ RawMessage
                  + 序列化器與轉換器(LineMessageJsonConverter + 具體型別用的 Factory)+ LineMessageValidation
                  + LineTextEmoji / LineTextSubstitution
        Template/ LineTemplate / Buttons / Confirm / Carousel(+Column)/ ImageCarousel(+Column)
        Imagemap/ LineImagemapAction / ImagemapUri / ImagemapMessage / LineImagemapArea / LineImagemapVideo
      Actions/    LineAction 與九種具體動作(Uri / Message / Postback / DatetimePicker / Camera / CameraRoll /
                Location / Clipboard / RichMenuSwitch)+ RawAction + 轉換器
      RichMenu/   LineRichMenu / Size / Bounds / Area / Info / Alias / ReplaceOptions / BatchOperation / BatchProgress
      Narrowcast/ LineNarrowcastOptions / LineNarrowcastRecipient(Audience / Redelivery / And / Or / Not)/ Progress
      Audience/   LineAudienceGroup / Created / Detail / Job / Page
      Insight/    LineMessageDeliveryInsight / LineFollowersInsight / LineDemographicInsight(+ 五種百分比項目)
                  / LineMessageDeliveryCount / LineMessageEventInsight(+ Overview / Message / Click)/ 統計單位回應
    Liff/         ILineLiffClient / LineLiffClient / LineLiffOptions / LineLiffApp(+ View / ViewType / Features)
                  / LineLiffServiceCollectionExtensions(AddLineLiff 具名與未具名多載)
                  / ILineLiffClientFactory / LineLiffClientFactory
    Webhook/      LineWebhookSignature / LineWebhookParser / LineWebhookContentProvider / LineWebhookVideoPlayComplete
                  事件模型 / 型別常數 / Beacon / Link / Membership / Unsend / Mention / Emoji / ImageSet 等強型別
      Handling/   LineWebhookHandler(抽象基底,21 個 On*Async)/ LineWebhookContext(單一事件的脈絡、一次性回覆、postback 還原)
    Postback/     ILinePostbackDataStore / InMemory / JsonFile / LinePostbackData{Entry,Options,Token,Resolver,StoreSupport}
                  / LinePostbackDataServiceCollectionExtensions(AddLinePostbackData)
    Templates/    LineMessageTemplate / LineTemplateVariables / LineTemplateRenderer / store 與 DI
    AutoReply/    LineAutoReplyRule(含 ChannelName)/ Matcher(含 ForChannel)/ Service / Outcome / store 與 DI
    Storage/      JsonFileStore(internal,原子寫入 + 讀寫鎖)/ LineStoreIds
  src/Ozakboy.Line.AspNetCore/          整合(PackageId=Ozakboy.Line.AspNetCore)
    LineLoginAuthentication{Defaults,Options,Handler,Extensions}.cs  LineClaimTypes.cs
    Webhook/      LineWebhookHttpRequestExtensions / LineWebhookEndpointRouteBuilderExtensions(+ .Handling:
                  泛型 THandler 多載、維護模式、錯誤通知)/ LineWebhookChannelSelection(internal,channel 挑選方式快照)
                  LineWebhookEndpointOptions / LineWebhookItems / LineWebhookMaintenanceOptions
                  / LineWebhookErrorNotificationOptions / LineWebhookServiceCollectionExtensions(AddLineWebhookHandler)
  src/Ozakboy.Line.Mcp/                 MCP 工具組(PackageId=Ozakboy.Line.Mcp)
    LineMcp{Options,SendMode,Json,Messages,RichMenu,DailyLimit,StoreOptions,HttpClientNames}.cs
    LineMcp{ServiceCollection,ServerBuilder,ApplicationBuilder,EndpointRouteBuilder}Extensions.cs
    LineMcp{KeyGate,WellKnownNotFound}Middleware.cs
    Outbox/       LineMcpOutbox{Item,Kind,Status} / ILineMcpOutboxStore / InMemory / JsonFile / ILineMcpOutboxService / Service
    LineMcpAggregationUnit.cs(送出工具的統計單位檢查與選項轉換)
    LineMcpClients.cs(依 LineMcpOptions.ChannelName 取 Messaging / LIFF 用戶端)
    LineMcpUnavailableMessagingClient.cs(channel 不存在時的 DispatchProxy 替身,每個方法回 not_configured)
    Tools/        Line{Info,Send,Outbox,RichMenu,Liff,AutoReply,Template}Tools
  tests/Ozakboy.Line.Tests/             核心測試(離線;Samples/*.json 是依官方文件整理的回應樣本,source 欄位註明端點;
                                        TestSupport/RecordingLineMessagingClient.cs 是 DispatchProxy 的記錄用假用戶端)
  tests/Ozakboy.Line.AspNetCore.Tests/  整合測試(TestHost,離線)
  tests/Ozakboy.Line.Mcp.Tests/         MCP 測試(工具直接 new + TestHost 驗金鑰閘,離線;FakeLineLiffClient 在 TestSupport/)
```

## 驗證指令(改完必跑)

- build:`dotnet build Ozakboy.Line.sln -c Release` — **主函式庫零警告零錯誤**
- test:`dotnet test --solution Ozakboy.Line.sln -c Release` — **全綠**
  - ⚠️ .NET 10 的 `dotnet test` 不吃 `-p`/專案位置參數,單跑一個專案要用 `--project <csproj>`,跑整個方案要用 `--solution <sln>`。
- CI:`.github/workflows/build.yml`(ubuntu-latest)push main / PR 自動跑;MTP 不認得 VSTest 的 `--logger`,產 trx 要用 `--report-trx`。
- **測試一律離線,絕不連真實 LINE**,憑證一律佔位符(`test-channel-id` / `test-channel-secret` / `test-channel-access-token`)。

## 發佈(`uplog`)— 走 nuget-release skill,本專案參數

- sln:`Ozakboy.Line.sln`
- csproj(三個都要打包):`src/Ozakboy.Line/Ozakboy.Line.csproj`、`src/Ozakboy.Line.AspNetCore/Ozakboy.Line.AspNetCore.csproj`、`src/Ozakboy.Line.Mcp/Ozakboy.Line.Mcp.csproj`
- **版號只改一處**:`Directory.Build.props` 的 `<VersionPrefix>`(三個套件同版號);`Ozakboy.Line.AspNetCore` 與 `Ozakboy.Line.Mcp` 對核心都是 `ProjectReference`,打包時會自動轉成同版號的 `PackageReference`
- 產物:`artifacts/`(或各專案 `bin/Release/`)的 `.nupkg` 與 `.snupkg`
- GitHub Release repo:`ozakboy/Ozakboy.Line`
- secrets:**共用** `D:\程式專案\Ozakboy_GitHub\.claude\secrets.local.json`,由 `../publish-package.ps1` 讀取(**勿讀出內容**)
- 每次發版必同步:三個 csproj 的 `PackageReleaseNotes`(英文)、`CHANGELOG.md`(中英雙語)、README 有提到版號之處
- 發新套件或加主要功能後,同步 harness `dotnet-packages` skill 的對照表(nuget-release 第 12 步)

## 公開 API 契約(動到就是 Major / Minor,不可順手改)

- **`LineErrorCodes` 的字串值是契約。** 呼叫端靠它分支與統計,發佈後不改字面值也不改語意;要表達新的失敗情況就加新代碼。
- **錯誤分類是契約。** `LineApiErrorMapper` 加工錯誤時**保留** `Ozakboy.Http` 判定的 `ErrorCategory` 與它放進去的資料鍵(`statusCode` / `body` / `retryAfterSeconds`),只補上 `lineMessage` / `lineDetails` 並改寫 `Message`。404 必須維持 `NotFound`、429 必須維持 `RateLimited` —— 下游用分類決定「要不要重試」,改掉會靜默打壞重試行為。
- **`RetryKey` 的語意是契約。** 有值 → 加 `X-Line-Retry-Key` 標頭**且**標 `AsIdempotent()`;沒有 → 兩者都不做,因此絕不重試。只做其中一件的結果分別是「重試但重複發送」與「帶了鍵卻永遠不重試」。
- **multicast 上限 500、單次訊息 1~5 則**,在本地檢查、失敗時**不送出任何請求**(`LineMessagingLimits`)。
- **`not_configured` 行為是契約。** 憑證不齊時所有方法直接回 `line.not_configured` 失敗且**一個請求都不送**;唯一例外是 `BuildAuthorizationUrl`(不回 `Result`,改擲 `InvalidOperationException`)。
- **404 當成「沒有」的兩個方法**:`GetDefaultRichMenuIdAsync` 與 `GetRichMenuIdOfUserAsync` 在 404 時回**成功且值為 null**;其他狀態碼仍是失敗。(0.3.0 的 `ILineLiffClient.GetAllAsync` 是第三個,見下方 LIFF 條目。)
- **`LineClaimTypes` 的宣告型別字串是契約**;`IsFriend` 在查不到時**不發這個宣告**(不是發 `"false"`)。
- **id_token 驗不過 = 整個登入失敗**,核心的 `CompleteLoginAsync` 與整合的 `CreateTicketAsync` 兩邊都是。
- **webhook 端點的狀態碼是契約**:驗簽失敗 401、解析失敗 400、其餘一律 200(含處理常式擲例外與零事件的驗證請求)。
- `LineMessage` / `LineAction` 只允許本組件內繼承(改寫輸出的成員是 internal);對外的擴充點是 `RawMessage` / `RawAction` 與 `*RawJsonAsync`。
- **快速回覆上限 13 顆按鈕**(`LineMessagingLimits.MaxQuickReplyItems`),`0` 或 `>13` 一律在送出前失敗;**超量時 LINE 是整則訊息退回**,不是只顯示前 13 個。零顆也算失敗,不當成「沒設定」。
- **範本與圖片地圖的本地上限是契約**(0.2.0):buttons 動作 1~4、confirm **恰好** 2、carousel 與 image_carousel 欄數 1~10、carousel 每欄動作 1~3 **且各欄一致**、imagemap 區域 1~50、`baseSize.width` 固定 1040(建構子只收高度)。全部走 `LineMessage.Validate()`,用戶端每次送出前逐則呼叫,違反時**一個請求都不送**;錯誤代碼 `line.validation.invalid_template` / `line.validation.invalid_imagemap`。
- **`LineMessage.Sender` 有值才輸出、兩欄皆空不輸出;`RawMessage` 忽略 `Sender` 與 `QuickReply`**(呼叫端的物件可能已經有那兩個欄位,再寫一次是重複欄位)。
- **端點參數的本地上限是契約**(0.2.0):載入動畫秒數 5~60 且為 5 的倍數(`line.validation.invalid_loading_seconds`)、好友清單 `limit` 1~1000 與受眾清單 `page ≥ 1` / `size` 1~40(`line.validation.invalid_page_size`)、受眾成員單次 ≤ 10,000(建立可為 0、加成員至少 1;`line.validation.too_many_audience_members`)。違反時不送出。
- **`NarrowcastAsync` 回的是 `X-Line-Request-Id` 標頭的值**,沒有那個標頭是 `line.api.invalid_response` 失敗;`RetryKey` 語意與推播完全相同。**`ValidateMessagesAsync` 先跑本地檢查再打 LINE**,本地失敗時不送。
- **`LineMessageValidationTarget` 只決定路徑最後一段**(`push` / `multicast` / `broadcast` / `reply` / `narrowcast`),不改請求內容。
- **LIFF 是第三個「404 當成沒有」,而且走獨立的權杖與 HttpClient**(0.3.0):`ILineLiffClient.GetAllAsync` 404 → **成功的空清單**,其他狀態碼仍是失敗且分類保留;LIFF 走獨立具名 HttpClient(`LineHttpClientNames.Liff`)與 **Login 頻道**的權杖(不是 Messaging 的);憑證空白 → `line.not_configured` 且一個請求都不送。
- **Webhook 位址與 LIFF 更新的行為是契約**(0.3.0):`SetWebhookEndpointAsync` / `TestWebhookEndpointAsync` 的 endpoint 本地檢查(https、≤ 500,`line.validation.invalid_webhook_endpoint`);`TestWebhookEndpointAsync` 對方伺服器沒回 2xx 時**呼叫本身仍成功**,要看 `Success` 欄位;`ILineLiffClient.UpdateAsync` **只送有值的欄位且絕不送 `liffId`**;`GetMessageContentTranscodingStatusAsync` 遇到未知狀態字串 → `line.api.invalid_response`(不猜)。
- **分批送出(`PushManyAsync` / `MulticastManyAsync`)的契約**(0.3.0):每批 5 則 / 500 人、**依序**送、**第一批失敗即停**,錯誤**保留該批的代碼與分類**並附 `sentBatches` 與 `failedBatchIndex`(0 起算;multicast 的索引 = recipientBatch × messageBatchCount + messageBatch);**整個輸入先驗證再送第一批**;零則訊息 → `too_many_messages`、零收件人 → `line.validation.no_recipients`;每批 RetryKey = `SHA256(key 的 16 bytes + 批次索引 4 bytes big-endian)` 的前 16 bytes 並設 UUID v5 / RFC 4122 位元(`LineBulkSendRetryKey.Derive`),**單批也推導**,沒給 key 就不帶、不重試;**刻意沒有 `ReplyManyAsync`**(回覆權杖單次有效且很快過期,不能分散到多個請求)。
- **統計單位與批次上限是契約**(0.3.0):`CustomAggregationUnits` 每次請求最多 1 個、≤ 30 字英數底線,本地擋(`line.validation.invalid_aggregation_unit`;push / multicast / broadcast / narrowcast 皆然);`GetMessageEventInsightByUnitAsync` 區間 `from ≤ to` 且 ≤ 30 天(`line.validation.invalid_date_range`);圖文選單批次作業 1–1000(`line.validation.invalid_rich_menu_batch`),request id 來自 `X-Line-Request-Id`。
- **訊息與動作的本地驗證是契約**(0.3.0):text / textV2 ≤ 5000、altText ≤ 400、location title / address ≤ 100、媒體 URL https 且 ≤ 2000、動作 label ≤ 20(imagemap 50、image carousel 12)、postback data ≤ 300、uri scheme 白名單、色碼 `#RRGGBB`、textV2 的 `{key}` 必須在 substitution 裡且每個 key 至少被引用一次;違反 → `line.validation.invalid_message` / `line.validation.invalid_action`,**一個請求都不送**;**圖文選單每個區塊的動作**也在 `CreateRichMenuAsync` / `ValidateRichMenuAsync` / `ReplaceRichMenuAsync`(建立之前)檢查,訊息指出 `richMenu.areas[i]`;**`RawMessage` / `RawAction` 不驗**(逃生口,因此 MCP 讀進來的選單——動作一律是 `RawAction`——不經這道本地檢查,最後防線是 LINE 的 validate 端點)。
- **`ReplaceRichMenuAsync` 的失敗處理是契約,而且刻意不對稱**:圖片上傳失敗會**刪掉剛建的新選單**(沒有圖片的選單比沒有選單更糟,而且佔額度);刪舊選單失敗**只記錄、整體仍回成功**(新選單已上線,回失敗只會讓呼叫端重做而多出一個選單)。設預設失敗則回失敗但**不**刪新選單。
- **自動回覆的挑選順序是契約**:先 `Priority` 升冪,同分再依明確程度 `Exact` > `StartsWith` > `Contains` > `Regex`。`Follow` 與 `Fallback` **不參與一般比對**,由 `FindFirst` 另外取;`Fallback` 只在其他規則都沒命中時才用,不參與優先序比較。
- **正規表示式比對逾時(100 ms)或表示式無效一律視為「不符」**,不擲出例外 —— 一條寫壞的規則不得讓 webhook 回非 2xx,那會讓 LINE 重送整批。
- **範本渲染的值一律先做 JSON 字串跳脫再替換**;缺變數是失敗(`line.validation.missing_template_variables`)而**不是**留著 `{{name}}` 送出去。
- **`LineWebhookItems.AutoReplyOutcome` 的鍵名字串是契約**;自動回覆失敗時**不放值**(免得宿主把失敗誤讀成「已經回過了」)。
- **`LineLoginAuthenticationOptions.PublicOrigin` 必須同時影響授權與換權杖兩個階段。** OAuth 要求兩次的 `redirect_uri` 逐字相同,只改一邊的結果是「授權成功但換權杖失敗」。
- **MCP 的待審制度是契約,不可放寬**:預設 `SendMode = Review`;`ILineMcpOutboxService` 的核准 / 退回 / 取消**永遠不得暴露成 MCP 工具**;所有送出類工具兩種模式都要寫一筆待發紀錄(直接模式**先送再記**,免得留下「已送出」的假紀錄);每日建立上限以 **CreatedAt** 計數而非送出數。
- **MCP 待發項目狀態只往前走**:`PendingReview` → `Approved` / `Sent` / `Failed` / `Rejected` / `Canceled`,已送出的不得回到待審(能回到待審 = 同一則訊息可能被核准兩次)。
- **MCP 金鑰閘驗證失敗一律回 404**,不得改成 401/403(那等於確認端點存在);未設 `ApiKey` = 功能關閉、整個子樹 404;比對必須用 `CryptographicOperations.FixedTimeEquals`。
- **MCP 工具名稱(`line_*`)是契約**:外部 AI 的提示詞與流程綁在上面,發佈後不改名。要表達新能力請加新工具。0.3.0 新增的七個——`line_get_webhook_info`、`line_test_webhook`、`line_get_delivery_stats`、`line_get_message_event_insight`、`line_list_liff_apps`、`line_create_liff_app`、`line_delete_liff_app`——同樣適用。
- **MCP 的 LIFF 工具與統計單位**(0.3.0):LIFF 工具在 `ILineLiffClient` 未註冊(宿主沒呼叫 `AddLineLiff`)時**照樣掛上並回 `line.not_configured`**,不是消失;建立 / 刪除 LIFF 是帳號設定變更,與 `line_replace_rich_menu` 一樣**走待審佇列**(`LineMcpOutboxKind.LiffCreate = 4` / `LiffDelete = 5`,接在既有數值之後以相容 0.2.0 的 JSON 檔;payload 存 viewType / url / description / botPrompt 或 liffId)——Review 模式只排佇列、核准時由 `LineMcpOutboxService` 依 kind 呼叫 `ILineLiffClient`,Direct 模式**先執行再記錄**,每日建立上限照常以 CreatedAt 計數,執行時寫一行 `ILogger`;核准時 `ILineLiffClient` 未註冊 → 項目標 `Failed`(`line.not_configured`);`line_list_liff_apps` 是唯讀直接查;四個查詢類新工具唯讀、不計每日額度,`line_get_delivery_stats` 四種管道都要回、`unready` 如實回報(`success` 為 null 不是 0);三個送出工具的 `customAggregationUnit` 在**提出時**就檢查,並存在待發項目的 `PayloadJson`(欄位 `customAggregationUnit`,舊項目沒有此欄位照常核准)、核准時才帶進 `LinePushOptions`。
- **多 channel 是契約**(0.4.0):空字串是預設 channel;**具名**註冊只註冊 keyed 服務,**未具名** = keyed("") + unkeyed(同一個實例);同名後註冊**整個取代**(設定委派、keyed 服務、登記項目都先移除再加,不疊加);具名 channel 的 `line.not_configured` 同樣成立(憑證不齊就一個請求都不送);`ILineMessagingClient.ChannelName` 是**預設介面成員**(舊實作不必改);webhook 的未知 channel **一律 401,且在驗簽前不觸碰任何狀態**(不讀 body、不取 `IOptionsMonitor`,絕不拿空密鑰驗簽);`LineWebhookItems.ChannelName` 的鍵名字串是契約,只在驗簽通過後才放值。`ChannelName` 與 `ChannelRouteParameter` 互斥(掛端點當下擲 `InvalidOperationException`);路由參數缺漏(選擇性參數沒給)= 空字串 = 預設 channel;整批內容的多載接 `LineWebhookEndpointOptions` 的形式刻意排在處理常式**之後**,排前面會與逐事件多載的 `Action<LineWebhookEndpointOptions>?` 在 `null` / 未標型別 lambda 下歧義。
- **事件分派與回覆權杖是契約**(0.4.0):message 事件**先** `OnMessageAsync` 再細分(`OnTextMessageAsync` …),未知型別走 `OnUnknownEventAsync`;基底類別**不吞例外**,隔離是端點的責任(單一事件擲例外只記錄,狀態碼契約不變)。回覆權杖一次性:任何**通過本地檢查而送出**的回覆都算用掉(即使 LINE 拒絕 —— 權杖是否被消耗無從得知),第二次在本地回 `line.webhook.reply_token_used` 且不送;事件沒有權杖回 `line.webhook.no_reply_token`;兩者都**不擲例外**。
- **維護模式與錯誤通知是契約**(0.4.0):維護模式看 `Source.UserId`(群組事件也一樣,沒有 `UserId` 視為非管理員);被擋的事件不進處理常式、不跑自動回覆;無回覆權杖或 `mode=standby` 的被擋事件**不回**。錯誤通知用 **push**(不是 reply),管理員並行、各自失敗,整體 10 秒逾時,整則 ≤ 5000 字(不切斷代理對),**絕不改變 200**。選項在**掛端點當下快照**(之後改物件不生效);`Enabled` 但白名單空 / `ReplyText` 空白 / handler 或用戶端未註冊 → 掛端點當下擲例外。批次內每個事件跑自動回覆**之前**先清掉上一個事件的 `Items[AutoReplyOutcome]`(自動回覆失敗時契約是「不放值」)。
- **postback 暫存是契約**(0.4.0):代碼前綴 `ozl:`、全長 36 字(32 個小寫 hex)**不可改**(已發出去的按鈕靠它辨識);`PostbackAction.CreateStoredAsync` **短資料也照存**(呼叫端不必依長度分支);store 一律註冊為 **singleton**,JSON 版 `IDisposable`;**找不到與過期是同一個失敗**(`line.postback.expired`,免得外人能分辨「猜中過」與「沒有」);資料超過 store 上限為 `line.validation.postback_data_too_long`。暫存資料與一般 postback `Data` 一樣**沒有簽章**,不能單靠它做權限判斷,必須拿 `Source.UserId` 回頭查自己的資料。`LineWebhookContext.ResolvePostbackDataAsync`:非 postback → `line.webhook.not_postback`;`Data` 不是代碼 → 原樣成功;是代碼但沒有 store → `line.not_configured`。
- **`ImagemapMessage.ForSingleImage` 是實驗性**(0.4.0):位址尾端補 `?`(已含 query 補 `&`,尾端已是 `?` / `&` 不補)、含 `#` 拒絕;動作範圍換成整張圖(複製一份,不改傳入物件)。它依賴 **LINE 會在底圖位址後接 `/{width}`、且伺服器忽略多出的 query**,官方沒有保證 —— 不要把它寫成穩定契約的賣點。
- **自動回覆與 MCP 的 channel 是契約**(0.4.0):`LineAutoReplyRule.ChannelName` 為 null 或空 = **所有 channel**(含舊資料缺省);比對前只留下 `ChannelName` 空或等於目前 channel 的規則(序數比較),挑選順序契約不變;`HandleAsync(evt)` = 預設 channel,`HandleAsync(evt, channelName)` 未註冊的名稱回 `line.not_configured`(預設介面實作只認預設 channel,不把別的 channel 當預設處理);服務的 `{{displayName}}` 查詢走同一個 channel 的用戶端。MCP 的 `LineMcpOptions.ChannelName`(預設空字串)決定所有工具與 `LineMcpOutboxService` 用哪個 channel;**預設 channel 優先用直接註冊的 `ILineMessagingClient`**(沒有工廠 / 替換了用戶端的舊宿主照常可用),**具名 channel 一律走工廠,沒有工廠或名稱未註冊就是 `line.not_configured` —— 絕不退回別的帳號**(送出類工具拿錯帳號比送不出去嚴重得多);送出類工具與 `line_replace_rich_menu` 在**提出時**就擋,不排進佇列。`line_get_bot_info` 輸出含 `channelName`。

## 本專案特有規則(踩坑紀錄)

- **`[JsonConverter]` 掛在基底類別不會被衍生型別繼承**:`JsonSerializer.Serialize(new TextMessage("x"))` 的型別參數推導成 `TextMessage`,沒有轉換器就退回反射,寫出 PascalCase 的錯誤 JSON 而且沒有任何錯誤(只有以 `LineMessage` / `LineAction` 為型別參數才對)。0.3.0 起**每個具體的 `LineMessage` / `LineAction` 子類都掛 `[JsonConverter(typeof(LineMessageJsonConverterFactory))]` / `LineActionJsonConverterFactory`**;**新增子類時必須一併掛上**,`LineConcreteTypeSerializationTests` 會逐型別比對與基底型別序列化的輸出。
- **MCP 測試專案以萬用字元 Link 核心測試的 `FakeLineMessagingClient*.cs`**(csproj 的 `<Compile Include="…\FakeLineMessagingClient*.cs">`)。`ILineMessagingClient` 與假用戶端都拆成 partial 檔,逐檔 Link 會在新增 partial 時漏掉一個,症狀是 MCP 測試專案編譯不過(假用戶端少實作介面成員)。新增 partial 檔名要維持 `FakeLineMessagingClient.<Name>.cs`。
- **`.claude/worktrees/` 已 gitignore**:Claude Code 代理的暫時 worktree 是工具產物,絕不進版控(不要 `git add -f`)。
- **`HttpPipelineClient.SendAsync` 不會因為非 2xx 而失敗**,`SendForStringAsync` 才會(它產生 `http.status.NNN` 錯誤並帶上 `body` / `retryAfterSeconds`)。所以 JSON 端點一律走 `LineHttp.SendForJsonAsync`;只有二進位的 `GetMessageContentAsync` 走 `SendAsync`,並自己呼叫 `HttpErrorMapper.FromResponseAsync` 補上同一套錯誤加工。
- **請求的擁有權**:`LineHttp.*` 會 `using` 掉傳進去的 `HttpRequestMessage`,呼叫端不要再包一層 `using`;直接用 `_http.SendAsync` 的路徑才由呼叫端自己釋放。
- **`Uri.ToString()` 會把 `%20` 還原成空白**,驗證編碼的測試要比對 `AbsoluteUri`。
- **`JsonElement` 的生命週期綁在 `JsonDocument` 上**,任何要留著的元素(`FlexMessage.Contents`、`RawMessage.Contents`、`LineWebhookEvent.Raw`)一律 `Clone()` 後保存,否則是一碰就擲例外的空殼。
- **LINE 的 `richmenus` 欄位是全小寫**(不是 `richMenus`),靠 camelCase 命名原則會得到永遠空的清單而且沒有錯誤 —— 所有模型都標了明確的 `[JsonPropertyName]`,不靠命名原則。
- **webhook 的 `timestamp` 是 Unix 毫秒**,當成秒解會落在 1970 年附近。
- **成員清單的欄位名不一致**:好友清單回 `userIds`,群組 / 聊天室成員清單回 `memberIds`;共用一個反序列化模型會有一邊永遠是空的而且沒有錯誤。對外統一成 `LineUserIdsPage`,內部是兩個 response DTO。
- **narrowcast 的請求識別碼在回應標頭 `X-Line-Request-Id`,不在內容裡**;`LineHttp.SendForStringAsync` 只給內容,所以 `NarrowcastAsync` 與 `GetMessageContentAsync` 一樣直接走 `_http.SendAsync`,自己釋放請求、自己呼叫 `HttpErrorMapper.FromResponseAsync`。
- **受眾加成員是 PUT 到同一個 `/audienceGroup/upload` 端點**,不是 POST 到 `/{id}/members`;成員是 `{"id": …}` 物件陣列而不是裸字串(與 multicast 的 `to` 不同);`audienceGroupId` 是**數字**。`markAsRead` 的內容是巢狀的 `chat.userId`,不是平的 `userId`。
- **imagemap 動作的位址欄位是 `linkUri`**,不是快速回覆動作的 `uri`;範本外層 `type` 固定 `template`,真正的型別在 `template.type`。這兩組動作型別因此各自建模(`LineImagemapAction` 不是 `LineAction`),不重用。
- **洞察端點的日期一律以 `CultureInfo.InvariantCulture` 寫成 `yyyyMMdd`**,預設文化在某些機器會寫成民國年或加分隔符號。
- **`AddOzakboyHttpPipeline` 的設定委派在註冊當下就執行完畢**,拿不到 `IOptions`。要把 channel secret 登記進遮罩器,只能在 `AddLineLogin` / `AddLineMessaging` 裡先自己跑一次 `configure` 取值(見 `LineServiceCollectionExtensions` 的 `probe`)。祕密長度不足 `SecretMasker.MinimumKnownSecretLength`(8)時不登記,否則 `HttpPipelineOptions.Validate()` 會讓註冊直接擲例外。
- **認證處理器的 nonce 必須在呼叫 `base.BuildChallengeUrl` 之前放進 `properties.Items`**,因為基底類別在那個方法裡就把 properties 序列化成 state 了;之後再放的東西不會出門。
- **遠端認證失敗的預設行為是把例外往外丟**,測試(與正式站)要設 `Events.OnRemoteFailure` 才看得到狀態碼。
- **`LineRichMenuArea.Action` 是 `required`**:不給預設動作,因為「忘了設定的區塊安靜地做某件事」比編譯錯誤難查得多。
- **`LineRichMenu.Areas` 是沒有 setter 的集合屬性**,`JsonSerializer` 預設不會去填它:反序列化出來的選單「名稱與尺寸都對、但一個區塊都沒有」,而 LINE 照收不誤,使用者看到的是一張按不動的圖。MCP 套件因此逐欄位手動讀(`LineMcpRichMenu.Parse`),不走 `JsonSerializer`。
- **MCP 的兩道閘必須在 `UseRouting` 之前。** 路由一旦比對完成,中介層再改寫路徑也沒有用 —— 端點早就選好了。放錯位置的症狀是「金鑰對不對都一樣,一律 404」,而那看起來像是金鑰設錯。
- **`UseLineMcpWellKnownNotFound()` 攔的是整個 `/.well-known` 子樹。** 日後要放 `security.txt`、`assetlinks.json` 這類資源,得在這道閘**之前**處理掉,否則會被無聲吃掉 —— 檔案明明在磁碟上、網址卻回 404,是個很難聯想到中介層的症狀。
- **`ModelContextProtocol` 的 `IMcpServerBuilder` 在 `Microsoft.Extensions.DependencyInjection` 命名空間**,不在 `ModelContextProtocol.Server`。
- **平面檔 store(`JsonFileStore<T>`)持有 `SemaphoreSlim`**,所以它與三個檔案型 store 都實作 `IDisposable`,由容器釋放;它們一律註冊為 **singleton** —— 鎖是實例層級的,每次請求各拿一個等於沒有鎖。
- **自動回覆的 `{{displayName}}` 只在規則真的用到時才查個人檔案**,查不到以空字串代入而不讓整個回覆失敗:為了一個名字讓歡迎訊息整個發不出去,是把小事變成大事。
- **測試專案要零警告,而且不准用 `NoWarn` / `#pragma`**:CA1861 —— 常數陣列當引數(連單元素的 `new[] { "x" }` 也算)一律抽成 `static readonly` 欄位,或改成 `string.Join` 比對;MSTEST0032 —— 常數對常數的斷言(`Assert.AreEqual("line.x", LineErrorCodes.X)`)改成斷言執行期取得的值(`result.Error.Code`);CS8602 —— `Result.Error` 是可為 null 的,先 `Assert.IsNotNull(result.Error)` 再取 `.Code`。
- **`RecordingLineMessagingClient`(`tests/Ozakboy.Line.Tests/TestSupport/`)是 `DispatchProxy` 實作的 `ILineMessagingClient`**:只認得 `ReplyAsync` / `ReplyTextAsync` / `PushAsync` / `PushTextAsync` 並記錄,其他任何成員(**包括 `ChannelName`**)擲 `NotSupportedException`,所以處理常式測試別讀 `context.Messaging.ChannelName`。它不隨介面長成員而需維護;AspNetCore 測試專案以**單檔** `<Compile Include=… Link=…>` 連結(只有這一個檔,不像 `FakeLineMessagingClient*.cs` 要萬用字元)。多 channel 測試要換掉某個 channel 的用戶端時,在 `AddLineMessaging(name, …)` 之後 `AddKeyedSingleton<ILineMessagingClient>(name, fake)`(最後註冊的贏,工廠走 keyed 解析)。MCP 的 `LineMcpUnavailableMessagingClient` 同樣是 `DispatchProxy`,所以不能 `sealed`(CA1852 用逐點 `#pragma` 處理,理由寫在旁邊)。
- 分析器 `NoWarn`(在核心 csproj,每條都附了中文理由):`CA1054` / `CA1055` / `CA1056`(URI 維持 string,避免 `System.Uri` 正規化改掉 LINE 給的位址,尤其 redirect_uri 必須逐字相同)、`CA1716`(`to` 是 LINE 自己的欄位名)、`CA1819`(`LineContent.Bytes`)、`CA2000`(請求的擁有權轉移給 `LineHttp`)。`CA2227` 與 `CA1031` 用逐點 `#pragma` 處理,不全域關。
- MCP 專案的 `NoWarn`:`CA1054` / `CA1055` / `CA1056`(理由同核心 —— 位址一律維持 string,MCP 工具的參數更是如此,那些值由外部 AI 提供,原樣傳遞才對得起 LINE 的逐字比對)、`CA1812`(兩個中介層由 `UseMiddleware<T>` 以反射具現化,分析器看不出來)。**`CA1716` 不在名單上**:MCP 專案裡的 `to` 是自己取的參數名,直接改名(`CountCreatedBetweenAsync` 的 `until`)而不是關規則。
