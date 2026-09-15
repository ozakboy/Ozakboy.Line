# Ozakboy.Line

> 建立日期:2026-09-15(claude-harness 標準架構,起手式比照 `Ozakboy.Http`)。全域規範(語言、C# 慣例、機密、禁令)見 `~/.claude/CLAUDE.md`;**發佈流程(`uplog`)一律走 `nuget-release` skill**,此檔只放本專案特有內容。
>
> NuGet 套件 ID:**`Ozakboy.Line`**(核心)與 **`Ozakboy.Line.AspNetCore`**(整合)。定位:LINE Login + Messaging API + Webhook 的 .NET 用戶端,附 ASP.NET Core 認證方案與 webhook 端點。程式碼抽取自幼明燈(`D:\程式專案\個人專案\YouMingDeng`)的實戰經驗,但兩邊不共用程式碼。

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
    LineUserProfile.cs  LineHttpClientNames.cs  LineServiceCollectionExtensions.cs
    Login/        LineLoginOptions / LineLoginClient / LineIdTokenValidator / LinePkce / 回應模型
    Messaging/    LineMessagingOptions / LineMessagingClient / 回應模型 / LinePushOptions
      Messages/   LineMessage 與七種具體訊息 + RawMessage + 序列化器與轉換器
      Actions/    LineAction 與四種具體動作 + 轉換器
      RichMenu/   LineRichMenu / Size / Bounds / Area / Info
    Webhook/      LineWebhookSignature / LineWebhookParser / 事件模型 / 型別常數
  src/Ozakboy.Line.AspNetCore/          整合(PackageId=Ozakboy.Line.AspNetCore)
    LineLoginAuthentication{Defaults,Options,Handler,Extensions}.cs  LineClaimTypes.cs
    Webhook/      LineWebhookHttpRequestExtensions / LineWebhookEndpointRouteBuilderExtensions
  tests/Ozakboy.Line.Tests/             核心測試(離線)
  tests/Ozakboy.Line.AspNetCore.Tests/  整合測試(TestHost,離線)
```

## 驗證指令(改完必跑)

- build:`dotnet build Ozakboy.Line.sln -c Release` — **主函式庫零警告零錯誤**
- test:`dotnet test --solution Ozakboy.Line.sln -c Release` — **全綠**
  - ⚠️ .NET 10 的 `dotnet test` 不吃 `-p`/專案位置參數,單跑一個專案要用 `--project <csproj>`,跑整個方案要用 `--solution <sln>`。
- CI:`.github/workflows/build.yml`(ubuntu-latest)push main / PR 自動跑;MTP 不認得 VSTest 的 `--logger`,產 trx 要用 `--report-trx`。
- **測試一律離線,絕不連真實 LINE**,憑證一律佔位符(`test-channel-id` / `test-channel-secret` / `test-channel-access-token`)。

## 發佈(`uplog`)— 走 nuget-release skill,本專案參數

- sln:`Ozakboy.Line.sln`
- csproj(兩個都要打包):`src/Ozakboy.Line/Ozakboy.Line.csproj`、`src/Ozakboy.Line.AspNetCore/Ozakboy.Line.AspNetCore.csproj`
- **版號只改一處**:`Directory.Build.props` 的 `<VersionPrefix>`(兩個套件同版號);`Ozakboy.Line.AspNetCore` 對核心是 `ProjectReference`,打包時會自動轉成同版號的 `PackageReference`
- 產物:`artifacts/`(或各專案 `bin/Release/`)的 `.nupkg` 與 `.snupkg`
- GitHub Release repo:`ozakboy/Ozakboy.Line`
- secrets:**共用** `D:\程式專案\Ozakboy_GitHub\.claude\secrets.local.json`,由 `../publish-package.ps1` 讀取(**勿讀出內容**)
- 每次發版必同步:兩個 csproj 的 `PackageReleaseNotes`(英文)、`CHANGELOG.md`(中英雙語)、README 有提到版號之處
- 發新套件或加主要功能後,同步 harness `dotnet-packages` skill 的對照表(nuget-release 第 12 步)

## 公開 API 契約(動到就是 Major / Minor,不可順手改)

- **`LineErrorCodes` 的字串值是契約。** 呼叫端靠它分支與統計,發佈後不改字面值也不改語意;要表達新的失敗情況就加新代碼。
- **錯誤分類是契約。** `LineApiErrorMapper` 加工錯誤時**保留** `Ozakboy.Http` 判定的 `ErrorCategory` 與它放進去的資料鍵(`statusCode` / `body` / `retryAfterSeconds`),只補上 `lineMessage` / `lineDetails` 並改寫 `Message`。404 必須維持 `NotFound`、429 必須維持 `RateLimited` —— 下游用分類決定「要不要重試」,改掉會靜默打壞重試行為。
- **`RetryKey` 的語意是契約。** 有值 → 加 `X-Line-Retry-Key` 標頭**且**標 `AsIdempotent()`;沒有 → 兩者都不做,因此絕不重試。只做其中一件的結果分別是「重試但重複發送」與「帶了鍵卻永遠不重試」。
- **multicast 上限 500、單次訊息 1~5 則**,在本地檢查、失敗時**不送出任何請求**(`LineMessagingLimits`)。
- **`not_configured` 行為是契約。** 憑證不齊時所有方法直接回 `line.not_configured` 失敗且**一個請求都不送**;唯一例外是 `BuildAuthorizationUrl`(不回 `Result`,改擲 `InvalidOperationException`)。
- **404 當成「沒有」的兩個方法**:`GetDefaultRichMenuIdAsync` 與 `GetRichMenuIdOfUserAsync` 在 404 時回**成功且值為 null**;其他狀態碼仍是失敗。
- **`LineClaimTypes` 的宣告型別字串是契約**;`IsFriend` 在查不到時**不發這個宣告**(不是發 `"false"`)。
- **id_token 驗不過 = 整個登入失敗**,核心的 `CompleteLoginAsync` 與整合的 `CreateTicketAsync` 兩邊都是。
- **webhook 端點的狀態碼是契約**:驗簽失敗 401、解析失敗 400、其餘一律 200(含處理常式擲例外與零事件的驗證請求)。
- `LineMessage` / `LineAction` 只允許本組件內繼承(改寫輸出的成員是 internal);對外的擴充點是 `RawMessage` / `RawAction` 與 `*RawJsonAsync`。

## 本專案特有規則(踩坑紀錄)

- **`HttpPipelineClient.SendAsync` 不會因為非 2xx 而失敗**,`SendForStringAsync` 才會(它產生 `http.status.NNN` 錯誤並帶上 `body` / `retryAfterSeconds`)。所以 JSON 端點一律走 `LineHttp.SendForJsonAsync`;只有二進位的 `GetMessageContentAsync` 走 `SendAsync`,並自己呼叫 `HttpErrorMapper.FromResponseAsync` 補上同一套錯誤加工。
- **請求的擁有權**:`LineHttp.*` 會 `using` 掉傳進去的 `HttpRequestMessage`,呼叫端不要再包一層 `using`;直接用 `_http.SendAsync` 的路徑才由呼叫端自己釋放。
- **`Uri.ToString()` 會把 `%20` 還原成空白**,驗證編碼的測試要比對 `AbsoluteUri`。
- **`JsonElement` 的生命週期綁在 `JsonDocument` 上**,任何要留著的元素(`FlexMessage.Contents`、`RawMessage.Contents`、`LineWebhookEvent.Raw`)一律 `Clone()` 後保存,否則是一碰就擲例外的空殼。
- **LINE 的 `richmenus` 欄位是全小寫**(不是 `richMenus`),靠 camelCase 命名原則會得到永遠空的清單而且沒有錯誤 —— 所有模型都標了明確的 `[JsonPropertyName]`,不靠命名原則。
- **webhook 的 `timestamp` 是 Unix 毫秒**,當成秒解會落在 1970 年附近。
- **`AddOzakboyHttpPipeline` 的設定委派在註冊當下就執行完畢**,拿不到 `IOptions`。要把 channel secret 登記進遮罩器,只能在 `AddLineLogin` / `AddLineMessaging` 裡先自己跑一次 `configure` 取值(見 `LineServiceCollectionExtensions` 的 `probe`)。祕密長度不足 `SecretMasker.MinimumKnownSecretLength`(8)時不登記,否則 `HttpPipelineOptions.Validate()` 會讓註冊直接擲例外。
- **認證處理器的 nonce 必須在呼叫 `base.BuildChallengeUrl` 之前放進 `properties.Items`**,因為基底類別在那個方法裡就把 properties 序列化成 state 了;之後再放的東西不會出門。
- **遠端認證失敗的預設行為是把例外往外丟**,測試(與正式站)要設 `Events.OnRemoteFailure` 才看得到狀態碼。
- **`LineRichMenuArea.Action` 是 `required`**:不給預設動作,因為「忘了設定的區塊安靜地做某件事」比編譯錯誤難查得多。
- 分析器 `NoWarn`(在核心 csproj,每條都附了中文理由):`CA1054` / `CA1055` / `CA1056`(URI 維持 string,避免 `System.Uri` 正規化改掉 LINE 給的位址,尤其 redirect_uri 必須逐字相同)、`CA1716`(`to` 是 LINE 自己的欄位名)、`CA1819`(`LineContent.Bytes`)、`CA2000`(請求的擁有權轉移給 `LineHttp`)。`CA2227` 與 `CA1031` 用逐點 `#pragma` 處理,不全域關。
