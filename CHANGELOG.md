# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- **Documentation only; no code or behaviour change.** `CLAUDE.md`'s directory map now matches the tree: the
  `Actions/` folder holds nine concrete actions plus `RawAction` (it said four), and `LineQuickReply`,
  `LineRichMenuAlias`, `LineRichMenuReplaceOptions`, `LineWebhookContentProvider` and `ILineMcpOutboxService` are
  listed where they live. The `Ozakboy.Line.Mcp` csproj comment on CA1848 and CA2007 was rewritten to say what is
  actually true: neither rule is in `NoWarn` — the package has no logging at all, so CA1848 never fires, and
  every `await` carries `.ConfigureAwait(false)`, so CA2007 is satisfied rather than suppressed. No rule was
  relaxed.
  **只改文件,程式碼與行為不變。** `CLAUDE.md` 的目錄結構改成與實際檔案一致:`Actions/` 是九種具體動作加
  `RawAction`(原本寫四種),並補列 `LineQuickReply`、`LineRichMenuAlias`、`LineRichMenuReplaceOptions`、
  `LineWebhookContentProvider` 與 `ILineMcpOutboxService`。`Ozakboy.Line.Mcp` csproj 裡關於 CA1848 與
  CA2007 的註解改寫成實況:兩條都不在 `NoWarn` —— 本套件完全沒有記錄,CA1848 根本不會觸發;
  每個 `await` 都接了 `.ConfigureAwait(false)`,CA2007 是被滿足而不是被壓下來。沒有放寬任何規則。

## [0.1.0] - 2026-09-15

First release. LINE Login, the Messaging API, and webhooks for .NET, with an ASP.NET Core authentication scheme
and webhook endpoint on top.

第一版。.NET 的 LINE Login、Messaging API 與 Webhook,上面再疊一套 ASP.NET Core 的認證方案與 webhook 端點。

### Added

- **`Ozakboy.Line` — LINE Login.** `ILineLoginClient` covers the whole flow: an authorization URL carrying PKCE,
  a nonce, `bot_prompt`, `ui_locales`, forced consent and the auto-login switches; the code exchange, refresh and
  revoke; access token and id_token verification; the profile, userinfo and friendship endpoints; and
  `CompleteLoginAsync`, which runs the exchange, the profile lookup, the friendship lookup and id_token
  validation as one call. `LinePkce` produces RFC 7636 verifiers and S256 challenges.
  `LineIdTokenValidator` verifies an id_token locally against the channel secret — signature, issuer, audience,
  expiry and nonce, each with its own error code — using `HMACSHA256` and `CryptographicOperations.FixedTimeEquals`
  from the BCL, with no JWT library.
  **`Ozakboy.Line` — LINE Login。** `ILineLoginClient` 涵蓋整個流程:帶 PKCE、nonce、`bot_prompt`、
  `ui_locales`、強制同意與自動登入開關的授權網址;換權杖、續期與撤銷;存取權杖與 id_token 的驗證;
  個人檔案、userinfo 與好友狀態端點;以及把換權杖、查檔案、查好友、驗 id_token 併成一次呼叫的
  `CompleteLoginAsync`。`LinePkce` 產生 RFC 7636 的 verifier 與 S256 challenge。
  `LineIdTokenValidator` 以 channel secret 在本地驗 id_token —— 簽章、發行者、對象、到期與 nonce,
  各有各的錯誤代碼 —— 用的是 BCL 的 `HMACSHA256` 與 `CryptographicOperations.FixedTimeEquals`,
  不引入任何 JWT 函式庫。

- **`Ozakboy.Line` — the Messaging API.** `ILineMessagingClient` covers push, multicast, broadcast and reply
  (each also in a text-only and a raw-JSON form), the message quota and its consumption, bot info, a friend's
  profile, message content downloads, and the full rich menu surface: create, upload image, list, read, delete,
  set and clear the default, and link, unlink and read a user's menu. Messages are modelled as `TextMessage`,
  `ImageMessage`, `VideoMessage`, `AudioMessage`, `LocationMessage`, `StickerMessage` and `FlexMessage`, with
  `RawMessage` as the escape hatch for anything LINE adds later; actions as `UriAction`, `MessageAction`,
  `PostbackAction` and `RawAction`. Every outgoing body is written field by field rather than reflected from
  property names.
  **`Ozakboy.Line` — Messaging API。** `ILineMessagingClient` 涵蓋推播、群發、廣播與回覆(各自另有純文字與
  原始 JSON 兩種形式)、推播額度與已用量、機器人資訊、好友的個人檔案、訊息內容下載,以及整組圖文選單:
  建立、上傳圖片、列表、查詢、刪除、設定與清除預設,以及連結、解除連結與查詢單一使用者的選單。
  訊息建模為 `TextMessage`、`ImageMessage`、`VideoMessage`、`AudioMessage`、`LocationMessage`、
  `StickerMessage` 與 `FlexMessage`,並以 `RawMessage` 作為 LINE 日後新增項目的逃生口;
  動作為 `UriAction`、`MessageAction`、`PostbackAction` 與 `RawAction`。送出的內容一律逐欄位寫出,
  不靠反射轉屬性名。

- **`Ozakboy.Line` — webhooks.** `LineWebhookSignature` computes and verifies `X-Line-Signature` over the raw
  request bytes in fixed time, answering `false` — never an exception — for a missing header, a blank secret or a
  signature that is not base64. `LineWebhookParser` turns a body into events, refusing nothing: an unfamiliar
  event type or an unmodelled field still arrives, with its original JSON in `LineWebhookEvent.Raw`.
  **`Ozakboy.Line` — Webhook。** `LineWebhookSignature` 對原始請求位元組計算並以定時比較驗證
  `X-Line-Signature`,標頭缺漏、密鑰空白或簽章不是合法 base64 時一律回 `false` 而非擲出例外。
  `LineWebhookParser` 把內容解析成事件,不擋任何東西:沒見過的事件型別或沒建模的欄位照樣送達,
  原始 JSON 留在 `LineWebhookEvent.Raw`。

- **`Ozakboy.Line.AspNetCore` — the authentication scheme.** `AddLineLogin()` on `AuthenticationBuilder`
  registers a LINE Login scheme built on the first-party `OAuthHandler`: PKCE on by default, a per-sign-in nonce
  carried through the OAuth state and checked against the id_token, `bot_prompt` for the add-friend flow, profile
  claims, an optional friendship claim, and local id_token validation that fails the whole sign-in when it does
  not verify. `MapLineWebhook()` maps an endpoint that verifies before parsing — 401 on a bad signature, 400 on
  an unparsable body — and isolates each event's handler so one failure does not make LINE redeliver the batch.
  **`Ozakboy.Line.AspNetCore` — 認證方案。** `AuthenticationBuilder` 上的 `AddLineLogin()` 註冊一個建立在
  官方 `OAuthHandler` 之上的 LINE Login 方案:PKCE 預設開啟、每次登入的 nonce 隨 OAuth state 帶出去再與
  id_token 比對、`bot_prompt` 加好友引導、個人檔案宣告、可選的好友狀態宣告,以及驗不過就讓整個登入失敗的
  本地 id_token 驗證。`MapLineWebhook()` 掛上一個先驗簽再解析的端點 —— 簽章不對回 401、內容讀不懂回 400
  —— 並隔離每個事件的處理常式,一個失敗不會讓 LINE 重送整批。

- **`Ozakboy.Line` — quick replies and the full action surface.** `LineQuickReply` and `LineQuickReplyItem`
  replace the raw `JsonElement` that `LineMessage.QuickReply` used to be, with the thirteen-button cap enforced
  before anything is sent — LINE rejects the whole message rather than showing the first thirteen. Actions now
  cover LINE's entire set: `DatetimePickerAction`, `CameraAction`, `CameraRollAction`, `LocationAction`,
  `ClipboardAction` and `RichMenuSwitchAction` join the existing four, `UriAction` gained `AltUriDesktop` for
  addresses only a phone understands, and `PostbackAction` gained `InputOption` and `FillInText`.
  **`Ozakboy.Line` — 快速回覆與完整的動作型別。** `LineQuickReply` 與 `LineQuickReplyItem` 取代了
  `LineMessage.QuickReply` 原本的 `JsonElement`,13 顆按鈕的上限在送出前就檢查 ——
  超量時 LINE 是整則訊息退回,不是只顯示前 13 個。動作補齊 LINE 全部型別:
  `DatetimePickerAction`、`CameraAction`、`CameraRollAction`、`LocationAction`、`ClipboardAction`、
  `RichMenuSwitchAction` 加入既有的四種,`UriAction` 多了給桌機用的 `AltUriDesktop`,
  `PostbackAction` 多了 `InputOption` 與 `FillInText`。

- **`Ozakboy.Line` — message templates.** `LineMessageTemplate` stores the JSON of a message array rather than a
  piece of plain text, so images, Flex layouts and quick replies all fit into a template.
  `LineTemplateVariables.Extract` finds its `{{variable}}` placeholders and `LineTemplateRenderer` substitutes
  them, escaping every value for JSON first — without which one quotation mark in a display name breaks the whole
  document. `ILineTemplateStore` ships with in-memory and JSON-file implementations, the latter writing
  atomically through a temporary file and serialising reads and writes behind one lock.
  **`Ozakboy.Line` — 訊息範本。** `LineMessageTemplate` 存的是訊息陣列的 JSON 而不是一段純文字,
  圖片、Flex 版面與快速回覆因此都能存進範本。`LineTemplateVariables.Extract` 找出 `{{變數}}` 佔位符,
  `LineTemplateRenderer` 代入它們,而且值一律先做 JSON 跳脫 ——
  少了這一步,一個暱稱裡的引號就足以把整份 JSON 弄壞。`ILineTemplateStore` 附記憶體與 JSON 檔兩種實作,
  後者寫入原子化(先寫暫存檔再換檔),讀寫共用一把鎖。

- **`Ozakboy.Line` — keyword auto reply.** A rule is data rather than code: `LineAutoReplyRule` goes into a store
  and can be edited from an admin page. Six match modes — `Exact`, `StartsWith`, `Contains`, `Regex`, plus
  `Follow` for the welcome message and `Fallback` for anything no rule matched — are ordered by priority and then
  by how specific the match is, so one loose `Contains` cannot quietly swallow a pile of precise `Exact` rules.
  Regular expressions run under a 100-millisecond cap and a timeout counts as no match rather than throwing.
  `MapLineWebhook` gained an overload taking `LineWebhookEndpointOptions`, whose `AutoReply` runs the rules
  before the handler and leaves the outcome in `HttpContext.Items`.
  **`Ozakboy.Line` — 關鍵字自動回覆。** 規則是資料而不是程式碼:`LineAutoReplyRule` 存進 store,
  由後台編輯。六種比對方式 —— `Exact`、`StartsWith`、`Contains`、`Regex`,加上歡迎訊息用的 `Follow`
  與沒有規則命中時用的 `Fallback` —— 先依優先序、同分再依明確程度排序,
  一條寬鬆的 `Contains` 因此吃不掉一堆精準的 `Exact`。正規表示式的比對有 100 毫秒上限,
  逾時視為不符而不是擲出例外。`MapLineWebhook` 多了一個收 `LineWebhookEndpointOptions` 的多載,
  它的 `AutoReply` 會在處理常式之前跑規則,結果放進 `HttpContext.Items`。

- **`Ozakboy.Line` — rich menu replacement, aliases and bulk links.** `ReplaceRichMenuAsync` runs create, upload,
  set-as-default, repoint-the-alias and delete-the-old-menu as one call, with deliberately asymmetric failure
  handling: a failed image upload deletes the menu just created, while a failed deletion of the old menu is only
  logged. Aliases get create, update, delete, read and list; `LinkRichMenuToUsersAsync` and
  `UnlinkRichMenuFromUsersAsync` cover the bulk endpoints up to 500 users, and `ValidateRichMenuAsync` checks a
  definition without creating anything.
  **`Ozakboy.Line` — 圖文選單以新換舊、別名與批次連結。** `ReplaceRichMenuAsync` 把建立、上傳圖片、
  設預設、指向別名、刪舊選單串成一次呼叫,失敗處理刻意不對稱:圖片上傳失敗會刪掉剛建的新選單,
  刪舊選單失敗則只記錄。別名有建立、更新、刪除、查詢與列表;`LinkRichMenuToUsersAsync` 與
  `UnlinkRichMenuFromUsersAsync` 對應批次端點(單次 500 人),`ValidateRichMenuAsync` 驗定義而不建立。

- **`Ozakboy.Line.AspNetCore` — `PublicOrigin`.** Naming the site's public address makes `redirect_uri` come from
  it rather than from the incoming request, at both the authorisation step and the token exchange. Behind a
  reverse proxy that does not set `X-Forwarded-Proto`, the derived address reads `http://` while the LINE
  Developers console has `https://` registered, and LINE — which compares verbatim — answers a 400 that says
  nothing about what differs.
  **`Ozakboy.Line.AspNetCore` — `PublicOrigin`。** 明確指定站台的對外網址之後,
  授權與換權杖兩個階段的 `redirect_uri` 都以它組出來,不再從當前請求推導。
  反向代理沒設 `X-Forwarded-Proto` 時,推導出來的是 `http://` 而後台登記的是 `https://`,
  而 LINE 是逐字比對的,回的 400 一個字都不會說是哪裡不一樣。

- **`Ozakboy.Line.Mcp` — a new package.** MCP tools over the above, so an outside AI can read the account's
  state, draft broadcasts, edit rules and templates, and replace rich menus. The rule is that the AI cannot send:
  in the default Review mode every sending tool writes to an outbox as `PendingReview`, the approve-and-send API
  belongs to the host and is never a tool, and a daily cap limits how much can be queued.
  `UseLineMcpKeyGate()` guards the whole subtree with a key carried in the path, compared in fixed time and
  answering a clean JSON 404 on any failure — 401 or 403 would confirm the endpoint exists.
  `WithLineTools()` adds the same tools to an MCP server a host already has.
  **`Ozakboy.Line.Mcp` — 新套件。** 把上述功能包成 MCP 工具,讓外部 AI 查詢帳號狀態、擬廣播、
  改規則與範本、替換圖文選單。鐵則是 AI 不得直接發送:預設的待審模式下,
  所有送出類工具只寫進待發佇列(`PendingReview`),核准並送出的 API 屬於宿主、永遠不是工具,
  另有每日建立上限。`UseLineMcpKeyGate()` 以路徑上的金鑰罩住整個子樹,定時比較,
  失敗一律回乾淨的 JSON 404 —— 回 401 或 403 等於確認這個端點存在。
  `WithLineTools()` 可把同一組工具加進宿主既有的 MCP server。

### Notes

- **Expected failures are `Result<T>`, not exceptions.** Every `line.*` error code listed in the README is a
  public contract and will not change once published; the category assigned by `Ozakboy.Http` is preserved, so a
  404 stays `NotFound` and a 429 stays `RateLimited`. LINE's own `message` and `details` are folded into the
  error data as `lineMessage` and `lineDetails`.
  **預期之內的失敗是 `Result<T>` 而不是例外。** README 列出的每一個 `line.*` 錯誤代碼都是公開契約,
  發佈後不再更動;`Ozakboy.Http` 判定的分類原封保留,404 仍是 `NotFound`、429 仍是 `RateLimited`。
  LINE 自己的 `message` 與 `details` 折進錯誤資料的 `lineMessage` 與 `lineDetails`。

- **A push is retried only with a retry key.** Without `LinePushOptions.RetryKey` a push goes out exactly once,
  because resending a POST sends a second message. With one, the request carries `X-Line-Retry-Key` — which LINE
  deduplicates on — and the pipeline may retry it.
  **推播只有帶了重試鍵才重試。** 沒有 `LinePushOptions.RetryKey` 的推播只送一次,因為重送 POST 就是多發
  一則訊息。帶了鍵之後請求會附上 `X-Line-Retry-Key`,LINE 以它去重,管線這時才會重試。

- **A Login channel and a Messaging channel are different channels**, with separate credentials and separate
  named HTTP clients. `AddLineLogin` and `AddLineMessaging` register one each.
  **Login channel 與 Messaging channel 是兩個不同的頻道**,憑證分開,具名 HTTP 用戶端也分開;
  `AddLineLogin` 與 `AddLineMessaging` 各註冊一個。

- **An outside AI proposes; a person approves.** `Ozakboy.Line.Mcp` defaults to Review mode, where nothing leaves
  the building until the host calls `ILineMcpOutboxService.ApproveAndSendAsync` — an API that is deliberately not
  exposed as an MCP tool, because a review queue is worth something only while whoever proposes cannot approve.
  Direct mode sends straight away and still writes an outbox record, since without one there is nowhere
  afterwards to find out who sent a message, when, or why.
  **外部 AI 提案,人核准。** `Ozakboy.Line.Mcp` 預設為待審模式,在宿主呼叫
  `ILineMcpOutboxService.ApproveAndSendAsync` 之前什麼都不會送出 —— 那個 API 刻意不做成 MCP 工具,
  因為待審制度的價值只在「提案的一方沒有核准權」時才成立。直接模式會立刻送出,但仍然寫一筆待發紀錄,
  否則事後沒有任何地方查得出訊息是誰、什麼時候、為什麼發的。

- **The MCP package is the one listed exception to the dependency policy.** It takes
  `ModelContextProtocol.AspNetCore`, the official C# SDK maintained by the modelcontextprotocol organisation
  together with Microsoft. MCP is a versioned wire protocol, and a hand-rolled implementation would fall behind
  every revision — visible as a connector that attaches and then lists no tools. The core and integration
  packages are unaffected.
  **MCP 套件是相依政策的唯一明列例外。** 它相依 `ModelContextProtocol.AspNetCore`,
  由 modelcontextprotocol 組織與 Microsoft 共同維護的官方 C# SDK。MCP 是一份有版本的線路協定,
  自己實作等於維護一份會隨協定改版而落後的實作 —— 落後的症狀是連接器連得上但工具清單是空的。
  核心與整合兩個套件不受影響。

[Unreleased]: https://github.com/ozakboy/Ozakboy.Line/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/ozakboy/Ozakboy.Line/releases/tag/v0.1.0
