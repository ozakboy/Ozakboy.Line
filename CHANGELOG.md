# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.3.0] - 2026-10-27

The rest of the Messaging API's account, statistics and rich menu surface, a LIFF server client, typed webhook events,
oversized sends split into batches, a local check for the caps LINE enforces on messages and actions, and seven new MCP
tools. No existing error code, category or method signature changed.

Messaging API 剩下的帳號設定、統計與圖文選單端點、LIFF server 用戶端、強型別的 webhook 事件、超量發送自動分批、
LINE 對訊息與動作的上限在本地檢查,以及七個新的 MCP 工具。既有的錯誤代碼、分類與方法簽章都沒有變。

### Added

- **`Ozakboy.Line` — webhook endpoint, account linking and message content.** `GetWebhookEndpointAsync`,
  `SetWebhookEndpointAsync` and `TestWebhookEndpointAsync` read, change and test the webhook address; the address must
  be https and at most 500 characters, checked locally. A test whose far end answers non-2xx is still a successful
  call — read `LineWebhookTestResult.Success`. `IssueLinkTokenAsync` issues the link token for account linking,
  `GetMessageContentPreviewAsync` downloads the preview of an image or video, and
  `GetMessageContentTranscodingStatusAsync` reports whether a video is ready; a status string it does not know fails
  as `line.api.invalid_response`.
  **`Ozakboy.Line` — webhook 位址、帳號連結與訊息內容。** `GetWebhookEndpointAsync`、`SetWebhookEndpointAsync` 與
  `TestWebhookEndpointAsync` 讀取、修改與測試 webhook 位址;位址必須是 https 且不超過 500 字元,本地檢查。
  測試時對方伺服器沒回 2xx,呼叫本身仍算成功 —— 要看 `LineWebhookTestResult.Success`。
  `IssueLinkTokenAsync` 核發帳號連結用的 link token,`GetMessageContentPreviewAsync` 下載圖片或影片的縮圖,
  `GetMessageContentTranscodingStatusAsync` 回報影片轉檔是否完成;不認得的狀態字串以
  `line.api.invalid_response` 失敗。

- **`Ozakboy.Line` — LIFF.** `ILineLiffClient` (`AddAsync`, `UpdateAsync`, `GetAllAsync`, `DeleteAsync`) manages the
  LIFF apps of a LINE Login channel, registered with either `AddLineLiff` overload and configured through
  `LineLiffOptions`. It runs on its own named `HttpClient` (`LineHttpClientNames.Liff`) with the **Login channel's**
  token, not the Messaging API's. `UpdateAsync` sends only the fields that are set and never the `liffId`;
  `GetAllAsync` turns a 404 into a successful empty list. Blank credentials fail as `line.not_configured` without
  sending anything, and a malformed app as `line.validation.invalid_liff_app`.
  **`Ozakboy.Line` — LIFF。** `ILineLiffClient`(`AddAsync`、`UpdateAsync`、`GetAllAsync`、`DeleteAsync`)管理
  一個 LINE Login 頻道下的 LIFF 應用程式,以 `AddLineLiff` 的兩個多載註冊、`LineLiffOptions` 設定。它走自己的
  具名 `HttpClient`(`LineHttpClientNames.Liff`),用的是 **Login 頻道**的權杖,不是 Messaging API 的。
  `UpdateAsync` 只送有值的欄位、絕不送 `liffId`;`GetAllAsync` 把 404 當成成功的空清單。憑證空白以
  `line.not_configured` 失敗且不送出,內容不合規格是 `line.validation.invalid_liff_app`。

- **`Ozakboy.Line` — statistics and aggregation units.** `GetMessageDeliveryCountAsync(type, date)` reads one
  channel's delivered count for one day (LINE counts days in JST; anything but `ready` leaves `Success` null),
  `GetMessageEventInsightAsync(requestId)` the engagement of one send, and `GetMessageEventInsightByUnitAsync` that
  of a unit over a range of at most 30 days (`from` not after `to`, else `line.validation.invalid_date_range`).
  `LinePushOptions.CustomAggregationUnits` and the narrowcast options tag a send with a unit — at most one per
  request, at most 30 letters, digits or underscores, checked locally as `line.validation.invalid_aggregation_unit`;
  `GetAggregationUnitUsageAsync` and `GetAggregationUnitNamesAsync` report the units in use.
  **`Ozakboy.Line` — 統計與統計單位。** `GetMessageDeliveryCountAsync(type, date)` 讀某管道某一天的送達數
  (LINE 以 JST 計日;狀態不是 `ready` 時 `Success` 為 null),`GetMessageEventInsightAsync(requestId)` 讀單次發送
  的互動統計,`GetMessageEventInsightByUnitAsync` 讀某個統計單位在最多 30 天區間內的統計(`from` 不得晚於
  `to`,否則 `line.validation.invalid_date_range`)。`LinePushOptions.CustomAggregationUnits` 與分眾推播選項
  替一次發送標上統計單位 —— 每次請求最多 1 個、最多 30 個英數字元或底線,本地檢查,違反為
  `line.validation.invalid_aggregation_unit`;`GetAggregationUnitUsageAsync` 與
  `GetAggregationUnitNamesAsync` 回報用了哪些單位。

- **`Ozakboy.Line` — rich menu batch operations.** `ReplaceOrUnlinkRichMenusInBatchAsync` takes 1 to 1000
  `LineRichMenuBatchOperation`s (`Link`, `Unlink`, `UnlinkAll`) and returns the request id from `X-Line-Request-Id`;
  `ValidateRichMenuBatchAsync` checks them without running, and `GetRichMenuBatchProgressAsync` reads the phase. An
  empty or oversized list fails locally as `line.validation.invalid_rich_menu_batch`.
  **`Ozakboy.Line` — 圖文選單批次作業。** `ReplaceOrUnlinkRichMenusInBatchAsync` 收 1 到 1000 個
  `LineRichMenuBatchOperation`(`Link`、`Unlink`、`UnlinkAll`),回 `X-Line-Request-Id` 標頭裡的請求識別碼;
  `ValidateRichMenuBatchAsync` 只驗不執行,`GetRichMenuBatchProgressAsync` 讀階段。空清單或超量在本地以
  `line.validation.invalid_rich_menu_batch` 失敗。

- **`Ozakboy.Line` — bulk send.** `PushManyAsync` and `MulticastManyAsync` split an oversized send into batches of
  5 messages and 500 recipients and send them **in order**, stopping at the first failure; the error keeps that
  batch's code and category and adds `sentBatches` and `failedBatchIndex` (`LineBulkSendErrorDataKeys`). The whole
  input is validated before the first batch goes out. With a `RetryKey`, each batch gets its own key from
  `LineBulkSendRetryKey.Derive`, so retrying the whole call cannot duplicate a batch that already went out; without
  one nothing is retried. Zero messages fail as `too_many_messages` and zero recipients as `line.validation.no_recipients`.
  There is deliberately no `ReplyManyAsync`: a reply token is single-use and expires.
  **`Ozakboy.Line` — 超量發送。** `PushManyAsync` 與 `MulticastManyAsync` 把超量的發送切成每批 5 則訊息、
  500 位收件人,**依序**送出,第一批失敗即停;錯誤保留該批的代碼與分類,並附上 `sentBatches` 與
  `failedBatchIndex`(`LineBulkSendErrorDataKeys`)。整個輸入先驗證,通過才送第一批。給了 `RetryKey` 時,
  每一批由 `LineBulkSendRetryKey.Derive` 推導出自己的鍵,整個呼叫重試時不會重複已經送出的批次;沒給就不重試。
  零則訊息是 `too_many_messages`,零位收件人是 `line.validation.no_recipients`。刻意沒有 `ReplyManyAsync`:
  回覆權杖只能用一次而且會過期。

- **`Ozakboy.Line` — typed webhook fields.** `LineWebhookEvent` gained `Joined`, `Left`, `Beacon`, `Unsend`,
  `Link` and `Membership`; `LineWebhookMessage` gained `Emojis`, `Mention`, `ImageSet`, `QuotedMessageId`,
  `MarkAsReadToken`, `StickerResourceType` and `Keywords`; and `LineWebhookBeaconTypes`, `LineWebhookLinkResults`,
  `LineWebhookMembershipTypes` and `LineWebhookEventTypes.Membership` name the wire strings.
  **`Ozakboy.Line` — webhook 強型別欄位。** `LineWebhookEvent` 多了 `Joined`、`Left`、`Beacon`、`Unsend`、`Link`
  與 `Membership`;`LineWebhookMessage` 多了 `Emojis`、`Mention`、`ImageSet`、`QuotedMessageId`、
  `MarkAsReadToken`、`StickerResourceType` 與 `Keywords`;`LineWebhookBeaconTypes`、`LineWebhookLinkResults`、
  `LineWebhookMembershipTypes` 與 `LineWebhookEventTypes.Membership` 給這些字串取了名字。

- **`Ozakboy.Line` — emoji and textV2.** `TextMessage.Emojis` (`LineTextEmoji`) places LINE emoji by index, and the
  new `TextV2Message` writes `{key}` placeholders resolved by `LineTextSubstitution.Mention`, `MentionAll` and
  `Emoji`, so positions no longer shift when a surrogate pair precedes them. Every `{key}` must exist in the
  substitution and every key must be referenced at least once.
  **`Ozakboy.Line` — emoji 與 textV2。** `TextMessage.Emojis`(`LineTextEmoji`)以索引放 LINE emoji;新的
  `TextV2Message` 寫 `{key}` 佔位符,由 `LineTextSubstitution.Mention`、`MentionAll` 與 `Emoji` 解析,位置不會
  因為前面有代理對而偏掉。每個 `{key}` 必須在 substitution 裡,每個鍵至少要被引用一次。

- **`Ozakboy.Line` — local validation.** Text and textV2 at most 5000 characters, alt text 400, location title and
  address 100, media URLs https and at most 2000, action labels 20 (imagemap 50, image carousel 12), postback data
  300, a uri-scheme allow-list, `#RRGGBB` colours. A violation fails as `line.validation.invalid_message` or
  `line.validation.invalid_action` and sends nothing; `RawMessage` and `RawAction` are not checked. Every rich menu
  area's action is now checked too, in `CreateRichMenuAsync`, `ValidateRichMenuAsync` and `ReplaceRichMenuAsync`,
  with the area named in the message (`richMenu.areas[2]`); `LineRichMenu.Validate()` is public.
  **`Ozakboy.Line` — 本地驗證。** 文字與 textV2 至多 5000 字元、altText 400、位置的標題與地址 100、媒體網址
  須 https 且至多 2000、動作標籤 20(圖片地圖 50、圖片輪播 12)、postback data 300、uri 協定白名單、
  `#RRGGBB` 色碼。違反時以 `line.validation.invalid_message` 或 `line.validation.invalid_action` 失敗且不送出;
  `RawMessage` 與 `RawAction` 不驗。圖文選單每個區塊的動作現在也會檢查
  (`CreateRichMenuAsync`、`ValidateRichMenuAsync`、`ReplaceRichMenuAsync`),訊息裡指出是哪個區塊
  (`richMenu.areas[2]`);`LineRichMenu.Validate()` 是公開的。

- **`Ozakboy.Line.Mcp` — seven tools.** `line_get_webhook_info`, `line_test_webhook`, `line_get_delivery_stats`
  (all four channels for one `yyyy-MM-dd` date; an `unready` channel is reported as it is) and
  `line_get_message_event_insight` are read-only and do not count against the daily cap. `line_list_liff_apps`,
  `line_create_liff_app` and `line_delete_liff_app` manage LIFF apps; when the host has not called `AddLineLiff` they
  answer `line.not_configured`. Creating and deleting change the account's configuration rather than send a message,
  so they skip the review queue and write a log line. `line_send_push`, `line_send_multicast` and
  `line_send_broadcast` take an optional `customAggregationUnit`, checked when proposed and stored with the outbox
  item; items queued by 0.2.0 approve as before.
  **`Ozakboy.Line.Mcp` — 七個工具。** `line_get_webhook_info`、`line_test_webhook`、`line_get_delivery_stats`
  (某個 `yyyy-MM-dd` 日期四種管道一次回,`unready` 的管道如實回報)與 `line_get_message_event_insight`
  都是唯讀,不計每日額度。`line_list_liff_apps`、`line_create_liff_app` 與 `line_delete_liff_app` 管理 LIFF
  應用程式;宿主沒呼叫 `AddLineLiff` 時回 `line.not_configured`。建立與刪除改的是帳號設定而不是送出訊息,
  所以不進待審佇列,改寫一行日誌。`line_send_push`、`line_send_multicast` 與 `line_send_broadcast` 多了
  選填的 `customAggregationUnit`,提出時檢查、隨待發項目一起存;0.2.0 排進佇列的項目照常核准。

- **New error codes.** `line.validation.invalid_message`, `line.validation.invalid_action`,
  `line.validation.invalid_webhook_endpoint`, `line.validation.invalid_liff_app`,
  `line.validation.invalid_date_range`, `line.validation.invalid_aggregation_unit`,
  `line.validation.invalid_rich_menu_batch` and `line.validation.no_recipients`; `LineMessagingLimits` gained the
  matching constants. Existing codes are untouched.
  **新錯誤代碼。** `line.validation.invalid_message`、`line.validation.invalid_action`、
  `line.validation.invalid_webhook_endpoint`、`line.validation.invalid_liff_app`、
  `line.validation.invalid_date_range`、`line.validation.invalid_aggregation_unit`、
  `line.validation.invalid_rich_menu_batch` 與 `line.validation.no_recipients`;`LineMessagingLimits` 加上對應
  的常數。既有代碼不動。

### Fixed

- **Serialising a concrete message or action type directly.** `JsonSerializer.Serialize(new TextMessage("x"))`
  infers `TextMessage` as the type argument, and the `[JsonConverter]` on `LineMessage` is not inherited, so it fell
  back to reflection and wrote PascalCase JSON with no error. Every concrete `LineMessage` and `LineAction` now
  carries a converter factory, and the output matches serialising it as the base type.
  **直接以具體型別序列化訊息或動作。** `JsonSerializer.Serialize(new TextMessage("x"))` 推導出的型別參數是
  `TextMessage`,而 `LineMessage` 上的 `[JsonConverter]` 不會被繼承,所以它退回反射、寫出 PascalCase 的 JSON
  而且沒有任何錯誤。現在每個具體的 `LineMessage` 與 `LineAction` 都掛了轉換器工廠,輸出與以基底型別序列化一致。

### Notes

- **Contract behaviour added in this release.** `ILineLiffClient.GetAllAsync` treats 404 as a successful empty
  list (the third 404-as-none, after the two rich menu id lookups); every other status still fails with its
  category kept. `TestWebhookEndpointAsync` succeeds even when the far end does not answer 2xx. A bulk send sends
  batches in order, stops at the first failure, and derives each batch's retry key as the first 16 bytes of
  SHA-256 over the key's 16 bytes and the 4-byte big-endian batch index, with the UUID version 5 and RFC 4122
  variant bits set — a single batch derives one too, and no key means no retry. The `line_*` names of the seven new
  MCP tools are a contract like the rest.
  **本版新增的契約行為。** `ILineLiffClient.GetAllAsync` 把 404 當成成功的空清單(繼兩個圖文選單識別碼
  查詢之後的第三個「404 當成沒有」);其他狀態碼仍是失敗,分類保留。`TestWebhookEndpointAsync` 在對方伺服器
  沒回 2xx 時呼叫本身仍成功。分批送出依序送、第一批失敗即停,每批的重試鍵是「鍵的 16 位元組加批次索引 4 位元組
  (big-endian)」做 SHA-256 取前 16 位元組,再設上 UUID v5 與 RFC 4122 的位元 —— 單批也推導,沒給鍵就不重試。
  七個新 MCP 工具的 `line_*` 名稱與其他工具一樣是契約。

## [0.2.0] - 2026-10-27

Two more message types, the rest of the Messaging API's chat, audience and insight surface, and a local check for
every new cap LINE enforces. No existing error code, category or method signature changed.

再加兩種訊息型別、Messaging API 剩下的聊天、受眾與洞察端點,以及每一條 LINE 新上限的本地檢查。
既有的錯誤代碼、分類與方法簽章都沒有變。

### Added

- **`Ozakboy.Line` — template and imagemap messages.** `TemplateMessage` wraps one of `ButtonsTemplate`,
  `ConfirmTemplate`, `CarouselTemplate` (with `CarouselColumn`) or `ImageCarouselTemplate` (with
  `ImageCarouselColumn`); `ImagemapMessage` carries `ImagemapUriAction` and `ImagemapMessageAction` areas and an
  optional `LineImagemapVideo`, with the base width fixed at 1040 so the constructor takes the height alone. Their
  caps — 4 buttons, exactly 2 confirm actions, 10 columns of at most 3 actions that must match across columns,
  50 imagemap areas — are checked on the caller's machine and a call that breaks one sends nothing. Every
  `LineMessage` now has a public `Validate()`, which the client runs per message before every send, and a
  `Sender` (`LineMessageSender`: `name`, `iconUrl`) written only when set; `RawMessage` ignores both, as it does
  the quick reply. `VideoMessage` gained `TrackingId`, and `LineWebhookEvent.VideoPlayComplete.TrackingId` reads
  it back from a `videoPlayComplete` event.
  **`Ozakboy.Line` — 範本與圖片地圖訊息。** `TemplateMessage` 包 `ButtonsTemplate`、`ConfirmTemplate`、
  `CarouselTemplate`(配 `CarouselColumn`)或 `ImageCarouselTemplate`(配 `ImageCarouselColumn`)其中之一;
  `ImagemapMessage` 帶 `ImagemapUriAction` 與 `ImagemapMessageAction` 兩種區域與可選的 `LineImagemapVideo`,
  底圖寬度固定 1040,建構子只收高度。上限 —— 4 個按鈕、確認範本恰好 2 個動作、10 欄且每欄最多 3 個動作
  並各欄一致、50 塊圖片地圖區域 —— 在呼叫端的機器上檢查,違反的呼叫一個請求都不送。每個 `LineMessage`
  多了公開的 `Validate()`(用戶端每次送出前逐則呼叫)與有值才輸出的 `Sender`(`LineMessageSender`:
  `name`、`iconUrl`);`RawMessage` 與快速回覆同理,兩者都忽略。`VideoMessage` 多了 `TrackingId`,
  `LineWebhookEvent.VideoPlayComplete.TrackingId` 從 `videoPlayComplete` 事件把它讀回來。

- **`Ozakboy.Line` — followers, groups and rooms.** `GetFollowerIdsAsync` pages the follower list with `start`
  and `limit`; groups get `GetGroupSummaryAsync`, `GetGroupMemberCountAsync`, `GetGroupMemberIdsAsync`,
  `LeaveGroupAsync` and `GetGroupMemberProfileAsync`, rooms the same minus the summary. Both member lists and
  the follower list come back as `LineUserIdsPage`, although LINE names the field `memberIds` in one and
  `userIds` in the other. `StartLoadingAnimationAsync` shows the typing animation for 5 to 60 seconds in steps of
  5, checked locally; `MarkAsReadAsync` writes the nested `chat.userId` LINE asks for.
  **`Ozakboy.Line` — 好友、群組與聊天室。** `GetFollowerIdsAsync` 以 `start` 與 `limit` 分頁列好友;群組有
  `GetGroupSummaryAsync`、`GetGroupMemberCountAsync`、`GetGroupMemberIdsAsync`、`LeaveGroupAsync` 與
  `GetGroupMemberProfileAsync`,聊天室除了摘要以外相同。兩種成員清單與好友清單都以 `LineUserIdsPage` 回來,
  雖然 LINE 一邊叫 `memberIds`、一邊叫 `userIds`。`StartLoadingAnimationAsync` 顯示 5 到 60 秒、5 的倍數的
  輸入中動畫(本地檢查);`MarkAsReadAsync` 寫出 LINE 要的巢狀 `chat.userId`。

- **`Ozakboy.Line` — message validation.** `ValidateMessagesAsync(LineMessageValidationTarget, messages)` and
  its raw-JSON twin post to LINE's `validate/{push|multicast|broadcast|reply|narrowcast}` endpoints, which check
  the message objects without sending anything or using quota. Local caps are checked first, and LINE's 400 comes
  back through `LineApiErrorMapper` with the offending field in `lineDetails`.
  **`Ozakboy.Line` — 訊息驗證。** `ValidateMessagesAsync(LineMessageValidationTarget, messages)` 與它的
  原始 JSON 版本打 LINE 的 `validate/{push|multicast|broadcast|reply|narrowcast}` 端點,只驗訊息物件、
  不送出、不計額度。本地上限先擋,LINE 的 400 經 `LineApiErrorMapper` 回來,出錯的欄位在 `lineDetails`。

- **`Ozakboy.Line` — narrowcast and audiences.** `NarrowcastAsync` sends to a subset of friends and returns the
  request id from the `X-Line-Request-Id` header; `LineNarrowcastOptions` takes the recipient and demographic
  trees as `JsonElement`, with `LineNarrowcastRecipient.Audience` / `Redelivery` / `And` / `Or` / `Not` building
  the common recipient shapes, plus the limit, the notification switch and a retry key with the same meaning as a
  push's. `GetNarrowcastProgressAsync` reads the phase and counts. Upload audiences get
  `CreateUploadAudienceGroupAsync`, `AddAudienceGroupMembersAsync` (a PUT, up to 10 000 users per call, checked
  locally), `GetAudienceGroupAsync`, `GetAudienceGroupListAsync` (page from 1, size 1 to 40) and
  `DeleteAudienceGroupAsync`.
  **`Ozakboy.Line` — 分眾推播與受眾。** `NarrowcastAsync` 送給一部分好友,回 `X-Line-Request-Id` 標頭裡的
  請求識別碼;`LineNarrowcastOptions` 以 `JsonElement` 收收件對象與屬性篩選的運算樹,
  `LineNarrowcastRecipient.Audience` / `Redelivery` / `And` / `Or` / `Not` 組常用的收件對象,另有人數上限、
  通知開關與語意同推播的重試鍵。`GetNarrowcastProgressAsync` 讀階段與人數。上傳型受眾有
  `CreateUploadAudienceGroupAsync`、`AddAudienceGroupMembersAsync`(PUT,單次最多 10,000 人,本地檢查)、
  `GetAudienceGroupAsync`、`GetAudienceGroupListAsync`(頁碼從 1 起、每頁 1 到 40)與
  `DeleteAudienceGroupAsync`。

- **`Ozakboy.Line` — insights.** `GetMessageDeliveryInsightAsync(date)`, `GetFollowersInsightAsync(date)` and
  `GetDemographicInsightAsync()` read the message delivery counts, the follower figures and the friend
  demographics. Dates are written as `yyyyMMdd` in the invariant culture, and a status other than `ready` leaves
  every number `null` rather than zero.
  **`Ozakboy.Line` — 成效洞察。** `GetMessageDeliveryInsightAsync(date)`、`GetFollowersInsightAsync(date)` 與
  `GetDemographicInsightAsync()` 讀訊息傳送數、好友數與好友屬性分布。日期以不變文化寫成 `yyyyMMdd`,
  狀態不是 `ready` 時所有數字是 `null` 而不是 0。

- **New error codes and caps.** `line.validation.invalid_template`, `line.validation.invalid_imagemap`,
  `line.validation.invalid_loading_seconds`, `line.validation.invalid_page_size` and
  `line.validation.too_many_audience_members`; `LineMessagingLimits` gained the matching constants. Existing codes
  are untouched.
  **新錯誤代碼與上限。** `line.validation.invalid_template`、`line.validation.invalid_imagemap`、
  `line.validation.invalid_loading_seconds`、`line.validation.invalid_page_size` 與
  `line.validation.too_many_audience_members`;`LineMessagingLimits` 加上對應的常數。既有代碼不動。

- **`Ozakboy.Line.Mcp`.** `line_send_push`, `line_send_multicast` and `line_send_broadcast` were confirmed to
  accept template and imagemap JSON in `messagesJson` verbatim, with tests; no tool was added or renamed.
  **`Ozakboy.Line.Mcp`。** 確認 `line_send_push`、`line_send_multicast` 與 `line_send_broadcast` 的
  `messagesJson` 對範本與圖片地圖 JSON 原樣收下,並補上測試;沒有新增或改名任何工具。

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

### Notes

- **The new caps are contracts, like the old ones.** They are checked before sending and a call that breaks one
  sends nothing: LINE rejects the whole message and its 400 names no field, so a local failure that says how many
  there were is the better one to get.
  **新上限與舊上限一樣是契約。** 送出前檢查,違反的呼叫一個請求都不送:LINE 是整則退回而且 400 不指名欄位,
  本地說得出實際數量的失敗才是比較好的那一個。

- **A narrowcast is asynchronous.** A 2xx means LINE accepted it; `GetNarrowcastProgressAsync` says whether it
  went out and to how many people.
  **分眾推播是非同步的。** 2xx 只代表 LINE 收下了,有沒有送出、送給幾個人要看 `GetNarrowcastProgressAsync`。

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

[Unreleased]: https://github.com/ozakboy/Ozakboy.Line/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/ozakboy/Ozakboy.Line/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/ozakboy/Ozakboy.Line/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/ozakboy/Ozakboy.Line/releases/tag/v0.1.0
