# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[Unreleased]: https://github.com/ozakboy/Ozakboy.Line/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/ozakboy/Ozakboy.Line/releases/tag/v0.1.0
