# LineBotSDK 2.18.45 元件盤點報告（反編譯分析）

- 作者：David Tung / isRock info LTD.（授權 MIT）
- 分析來源：ILSpy 反編譯的 netstandard2.0 組件（`LineBotSDK.dll`，108 個 .cs、約 4,400 行）；輔助資料為 `lib/net45/LineBot.XML`（作者註解）、`Readme.txt`、`LineBotSDK.nuspec`。
- 方法：全部 .cs 逐檔讀完；另以 `strings` 掃描 net45 的 `LineBot.dll` 以辨識兩個目標框架的差異（未執行任何反編譯出來的程式碼）。
- 「初判」標記：凡是依程式碼推論、未實測 LINE 伺服器行為的結論，會明確標註「推論」或「待驗證」。

---

## 1. 套件概觀

### 1.1 TFM 與相依

| 項目 | 內容 |
|---|---|
| 套件內 TFM | `lib/net45/LineBot.dll`（附 `LineBot.XML`）與 `lib/netstandard2.0/LineBotSDK.dll`（無 XML） |
| nuspec 相依（僅 netstandard2.0 群組） | `Microsoft.AspNetCore.Mvc.Core` 2.2.5、`Microsoft.CSharp` 4.7.0、`Newtonsoft.Json` 13.0.1 |
| `Microsoft.CSharp` 的用途 | 程式碼中用了 `dynamic`（`Utility.SetWebhookEndpointURL` 回傳 `dynamic`；`GetMembersInGroupCount` / `GetMembersInRoomCount` 內部以 `dynamic val.count` 取值） |
| `Microsoft.AspNetCore.Mvc.Core` 的用途 | 只為了兩件事：`LineWebHookControllerBase : ControllerBase`、`Utility.SignatureValidation(ControllerBase, secret)` |
| 反編譯專案 csproj | 是 ILSpy 產出的 `Microsoft.NET.Sdk.Web` + ASP.NET 8 HintPath，那是反編譯環境的產物，不代表套件實際相依 |
| 版本字串 | AssemblyVersion 2.18.40.0 / InformationalVersion 2.18.45；nuspec 的 releaseNotes 只寫「新增 PushBroadcast 和 PushMulticast」 |
| 自稱功能 | AssemblyDescription／nuspec 宣稱「Flex Message supported」，但程式碼中沒有任何 Flex 型別（見 §2.2、§4） |

**兩個 TFM 內容不同（重要）**：XML 文件與 net45 DLL 內含 `isRock.LineBot.Conversation.*`（`InformationCollector<T>`、`ConversationState<T>`、`ProcessResult<T>` 等對話狀態機）、`isRock.MsQnAMaker.Client`（Microsoft QnA Maker 呼叫）、`LineBot.LineBotHelper`、`Bot()` 無參數建構式（從 appSettings 讀 token）、`SignatureValidation(System.Web.Http.ApiController)`。這些在 netstandard2.0 組件中**完全不存在**（原始碼 108 檔中無對應型別，netstandard DLL 字串掃描也搜不到）。換言之，XML 描述的是舊版 .NET Framework 版，不能拿來當 netstandard 版的功能清單。以下「元件清單」以 netstandard2.0 反編譯結果為準，net45 專屬功能只在 §2.12 列為附註（只有 XML／字串層級證據，未反編譯）。

### 1.2 命名空間一覽

| 命名空間 | 型別數（頂層） | 內容 |
|---|---|---|
| `isRock.LineBot` | 74（另有 9 個巢狀型別在 `Utility` 內） | 核心：靜態 `Utility`（1,338 行）、物件 `Bot`（589 行）、`LineWebHookControllerBase`（265 行）、訊息／動作／QuickReply／事件／結果模型 |
| `isRock.LineBot.RichMenu` | 7 | `RichMenuItem`、`Area`、`Bounds`、`Size`、`GetRichMenuListResponse`、`richMenuIdResponse`、`ActionConverter` |
| `isRock.LineBot.Demographic` | 6 | `GetFriendDemographicsResult`、`Gender`、`Age`、`Area`、`AppType`、`SubscriptionPeriod` |
| `isRock.LineBot.Extensions` | 1 | `JsonExtensions.ToJson(this object)` |
| `isRock.LINE` | 1 | `internal class Utility`：內部 HTTP 薄封裝（`LineHttpPost/Put/Delete/Get/GetBytes`） |
| `isRock.LineLoginV1` | 3 | 全部 `[Obsolete]` |
| `isRock.LineLoginV21` | 4 | `Utility`、`GetTokenFromCodeResult`、`Profile`、`GetFriendshipStatusResult` |
| `isRock.LineNotify` | 5 | 全部 `[Obsolete]` |
| `isRock.LIFF` | 6 | `Utility`、`App`、`View`、`ViewType`、`GetAllLiffAppsResult`、`internal AddLiffAppResult` |
| （全域） | 0 | `Properties/AssemblyInfo.cs` 非型別 |

合計 107 個頂層型別 + 9 個巢狀型別。`isRock.LineBot` 內 74 型的功能分布：訊息 9、Imagemap 附屬 7、Template 7、動作 10、QuickReply 8、其他送出用 3、接收事件模型 15、結果／資訊模型 12、門面類別 3。

### 1.3 架構風格

- **靜態 Utility 類為主，Bot 類為薄包裝**：`isRock.LineBot.Utility` 是 god class（64 個 public static 方法，**絕大多數最後一個參數是 `ChannelAccessToken`**）。`Bot` 保存 token 後多數方法轉呼叫 `Utility`，另外直接實作 quota／insight／validate／broadcast／multicast。`LineWebHookControllerBase` 是第三層薄包裝（內部 `new Bot(token)`）。沒有介面、沒有 DI 整合、沒有 `IHttpClientFactory`、沒有 Options。
- **全同步**：整個程式碼庫沒有 `async`／`Task`（grep 無結果）。HTTP 一律 `System.Net.WebClient`（共 39 處 `new WebClient`）。
- **JSON 序列化混合兩套**：
  - 新路徑：物件模型 → `MessageBase.ToJSON()` → `JsonConvert.SerializeObject((object)this)`（Newtonsoft.Json）。
  - 舊路徑：用 C# 字串樣板 + `string.Format` + `.Replace("'", "\"")` 手工拼 JSON（Text、Image、Sticker、Imagemap、4 種 Template 的 Push／Reply 全是這種）。
  - 反序列化一律 `JsonConvert.DeserializeObject<T>`；結果型別是無驗證的 POCO（小寫屬性名直接對應 LINE JSON）。
- **錯誤處理**：全部轉成 `System.Exception`（訊息內含 HTTP 回應本文、Endpoint、請求 JSON），沒有自訂例外型別、沒有狀態碼屬性（只能從 `InnerException as WebException` 取）。部分方法吞掉例外回傳 `false`（`Validate*`、`DisplayLoadingAnimation`）或 `null`（Rich Menu 的 404）。
- **前置驗證**：大量在客戶端以 `throw new Exception(...)` 檢查（訊息數 1–5、multicast 人數、actions 數量、https 開頭、QuickReply ≤ 13、DisplayLoadingAnimation 秒數等）。
- **命名**：混合 PascalCase 方法、小寫 JSON 屬性；有拼字錯誤殘留（`ReceievedMessage`、`GetToeknFromCode`）並以 `[Obsolete]` 墊片保留。

---

## 2. 元件清單（依功能分類）

> 約定：`Utility.X` = `isRock.LineBot.Utility`（靜態，最後參數都是 `token`，下表省略）；`Bot.X` = 實例方法（建構式 `Bot(string ChannelAccessToken)`）；`Ctrl.X` = `LineWebHookControllerBase`。主機：`api.line.me`（簡寫 A）、`api-data.line.me`（簡寫 D）、`notify-api.line.me`（N）、`notify-bot.line.me`（NB）。**程式碼中沒有任何 `access.line.me`**（LINE Login 授權網址不是由 SDK 產生）。

### 2.1 Messaging API 送訊息（push／reply／multicast／broadcast／validate）

> 可看到 narrowcast 與 audience 完全沒有（grep 無任何 `narrowcast`、`audience`、`aggregation` 字串）。

**Push（單一對象）**——全部 `POST https://api.line.me/v2/bot/message/push`：

| 方法 | 說明 |
|---|---|
| `Utility.PushMessage(to, text)` | 手工拼 JSON；會把訊息內的 `"` 換成 `'` |
| `Utility.PushImageMessage(to, originalUrl, previewUrl)` | 檢查 https |
| `Utility.PushStickerMessage(to, packageId, stickerId)` | |
| `Utility.PushImageMapMessage(to, ImagemapMessage)` | actions ≤ 50 檢查 |
| `Utility.PushTemplateMessage(to, ButtonsTemplate / ConfirmTemplate / CarouselTemplate / ImageCarouselTemplate)` | 4 個多載，各自檢查 actions 數量 |
| `Utility.PushMessagesWithJSON(to, jsonArray)` | 傳入 JSON 陣列字串（Flex 唯一途徑） |
| `Bot.PushMessage(to, string / Uri / Uri,Uri / int,int / ImagemapMessage / 4 Templates / TextMessage / MessageBase / List<MessageBase>)` | 約 13 個多載；字串版限 1800 字；`List` 版限 1–5 則 |
| `Bot.PushMessageWithJSON(to, json)` | 轉 `Utility.PushMessagesWithJSON` |
| `Ctrl.PushMessage(...)`、`Ctrl.PushMessagesWithJSON(...)` | 同上，用控制器的 `ChannelAccessToken` |

**Reply**——全部 `POST https://api.line.me/v2/bot/message/reply`：

| 方法 | 說明 |
|---|---|
| `Utility.ReplyMessage(replyToken, text)` | |
| `Utility.ReplyStickerMessage` / `ReplyImageMessage` / `ReplyImageMapMessage` | |
| `Utility.ReplyTemplateMessage(...)` ×4 | Buttons／Confirm／Carousel／ImageCarousel |
| `Utility.ReplyMessageWithJSON(replyToken, jsonArray)` | |
| `Bot.ReplyMessage(replyToken, string / int,int / Uri / Uri,Uri / TextMessage / MessageBase / List<MessageBase>)` | `List` 版限 1–5 則 |
| `Bot.ReplyMessageWithJSON(ToUserID, json)` | 參數名誤寫成 ToUserID，實為 replyToken |
| `Ctrl.ReplyMessage(...)` | 全套多載 |

**Multicast／Broadcast**：

| 方法 | HTTP |
|---|---|
| `Bot.PushBroadcast(List<MessageBase>)`（1–5 則） | `POST https://api.line.me/v2/bot/message/broadcast` |
| `Bot.PushMulticast(List<string> to, List<MessageBase>)`（人數 1–500、訊息 1–5） | `POST https://api.line.me/v2/bot/message/multicast` |
| `Bot.PushMulticast(List<string> to, List<string> texts)`（人數 **< 150**、訊息 1–5；僅純文字） | `POST https://api.line.me/v2/bot/message/multicast` |
| `Ctrl.PushMulticast(List<string>, List<string>)` | 同上（與 Bot 版整段複製貼上） |

**Validate**：

| 方法 | HTTP |
|---|---|
| `Bot.ValidatePushMessage(TextMessage / MessageBase / List<MessageBase>)`、`ValidatePushMessageWithJSON(json)` | `POST https://api.line.me/v2/bot/message/validate/push` |
| `Bot.ValidateReplyMessage(TextMessage / MessageBase / List<MessageBase>)`、`ValidateReplyMessageWithJSON(json)` | `POST https://api.line.me/v2/bot/message/validate/reply` |

回傳 `bool`，任何例外一律吞成 `false`（拿不到驗證失敗的原因）。

**其他送訊息相關**：
- `Bot.DisplayLoadingAnimation(chatId, loadingSeconds = 20)` → `POST https://api.line.me/v2/bot/chat/loading/start`（客戶端檢查 5–60 且為 5 的倍數；例外吞成 `false`）。

### 2.2 訊息型別模型

**基底**：`MessageBase`（abstract）——屬性 `type`、`quoteToken`（送出時可引用）、`quickReply`、`sender`（getter 惰性建立空 `Sender`）；方法 `ToJSON()`。

**具體訊息類別**（皆繼承 `MessageBase`，建構式設好 `type` 並放入空 `QuickReply`）：

| 型別 | 屬性／建構式 |
|---|---|
| `TextMessage(string text)` | 只有 `text`，**沒有 `emojis`、沒有 mention、沒有 `textV2`** |
| `ImageMessage(Uri original, Uri preview)` | |
| `VideoMessage(Uri original, Uri preview)` | |
| `AudioMessage(Uri original, int duration)` | |
| `LocationMessage(title, address, lat, lng)` | |
| `StickerMessage(int packageId, int stickerId)` | 兩個 id 以字串儲存 |
| `ImagemapMessage` | `baseUrl`(setter 檢查 https)、`altText`、`baseSize`(`System.Drawing.Size` + `SizeJsonConverter`)、`actions`(`List<ImagemapActionBase>`)、`video`(`ImagemapVideo`) |
| `TemplateMessage(TemplateMessageBase)` | 包裝 Template 以進入 `MessageBase` 管線，預設 altText 為「請在手機上觀看此訊息…」 |

**Imagemap 附屬**：`ImagemapArea(x,y,w,h)`、`ImagemapActionBase`、`ImagemapUriAction`(`linkUri`, `area`)、`ImagemapMessageAction`(`text`, `area`)、`ImagemapVideo`(`originalContentUrl`, `previewImageUrl`, `area`, `externalLink`)、`ImagemapExternalLink`(`linkUri`, `label`)、`SizeJsonConverter`。

**Template（樣板）**：`TemplateMessageBase`(abstract) → `ButtonsTemplate`(`thumbnailImageUrl`, `imageAspectRatio`, `imageSize`, `imageBackgroundColor`, `title`, `text`, `actions`；三個屬性 setter 帶驗證)、`ConfirmTemplate`(`text`, `actions`)、`CarouselTemplate`(`columns`, `imageAspectRatio`, `imageSize`)、`ImageCarouselTemplate`(`columns`)；欄位類別 `Column`(`thumbnailImageUrl`, `title`, `text`, `actions`)、`ImageCarouselColumn`(`imageUrl`, `action`)。**沒有 `defaultAction`**。

**Flex Message：完全沒有模型**。唯一支援方式是自己準備 JSON 陣列字串，走 `PushMessagesWithJSON`／`ReplyMessageWithJSON`／`Bot.PushMessageWithJSON`／`Bot.ReplyMessageWithJSON`（以及 Validate*WithJSON）。沒有 Flex 容器／元件／Bubble 型別。

**附屬物**：

| 型別 | 說明 |
|---|---|
| `Sender`(`name`, `iconUrl`) | 掛在 `MessageBase.sender`，可改訊息顯示名稱與頭像 |
| `QuickReply`(`items`: `List<QuickReplyItemBase>`) | ≤ 13 於 `ToJSON()` 檢查；空清單序列化前置為 null |
| `QuickReplyItemBase`(abstract: `type`, `imageUrl`(https 檢查), `action`) | |
| `QuickReplyMessageAction(label,text[,imageUrl])`、`QuickReplyPostbackAction(label,data,displayText,text[,imageUrl])`、`QuickReplyDatetimePickerAction(label,data,mode[,imageUrl])`、`QuickReplyCameraAction`、`QuickReplyCamerarollAction`、`QuickReplyLocationAction` | 6 個工廠式子類別（**但 `type` 欄位多半設錯，見 §4**） |
| `Emoji`(`index`,`length`,`productId`,`emojiId`)、`Mention`/`Mentionee`(`index`,`length`,`userId`,`type`,`isSelf`) | **只出現在接收端 `Message` 模型**；送出端沒有對應欄位 |
| `MulticastMessage`(`type`,`text`) | 舊版純文字 multicast 用的簡易 DTO |
| `ConstData.NotSupportMessage` | 預設 altText：「請在手機上觀看此訊息。Please check this message on your phone.」 |

### 2.3 動作型別

全部繼承 `TemplateActionBase`（有 `[JsonProperty] protected string type`），子類別建構式寫入 `type`：

| 型別 | `type` 值 | 屬性 |
|---|---|---|
| `UriAction` | `uri` | `label`, `uri`（無 `altUri`） |
| `MessageAction` | `message` | `label`, `text` |
| `PostbackAction` | `postback` | `label`, `data`, `displayText`, `text`, `inputOption`, `fillInText`（後兩者對應 2022-05 rich menu 開關鍵盤功能） |
| `DateTimePickerAction` | `datetimepicker` | `label`, `data`, `mode`(setter 檢查 date/time/datetime), `initial`, `max`, `min`；`enum DatetimePickerModes` |
| `CameraAction` | `camera` | `label` |
| `CamerarollAction` | `cameraRoll` | `label` |
| `LocationAction` | `location` | `label` |
| `ClipboardAction` | `clipboard` | `label`, `clipboardText` |
| **無 `richmenuswitch`** | — | 整個程式碼沒有 `RichMenuSwitchAction`，也沒有 `richMenuAliasId` 欄位 |

Imagemap 另有獨立的 `ImagemapUriAction`、`ImagemapMessageAction`（不是繼承 `TemplateActionBase`）。

### 2.4 Webhook 接收與解析

**解析**：
- `Utility.Parsing(string RawData)` → `ReceivedMessage`（含 `destination` 與 `List<Event> events`）。
- `ReceievedMessage`（拼字錯誤版）：`[Obsolete]`，結構相同。

**事件模型**（全部是單一扁平 POCO，沒有繼承、沒有多型）：

| 型別 | 欄位 |
|---|---|
| `Event` | `type`, `replyToken`, `source`, `timestamp`(long), `message`, `postback`, `beacon`, `members`(SourceUser[]), `left`, `joined`, `link` |
| `Source` | `type`, `userId`, `groupId`, `roomId` |
| `SourceUser` | `type`, `userId` |
| `Members` | `members`(List<SourceUser>)，給 `left`/`joined` 用 |
| `Message`（接收端，聯集型） | `type`, `id`, `quoteToken`, `fileName`, `fileSize`(string), `text`, `emojis`, `mention`, `title`, `address`, `latitude`, `longitude`, `packageId`, `stickerId`, `stickerResourceType`, `duration`, `contentProvider` |
| `ContentProvider` | `type`, `originalContentUrl`, `previewImageUrl` |
| `Postback` + `Params` | `data`；`Params`: `datetime`, `date`, `time` |
| `Beacon` | `hwid`, `dm`, `type` |
| `Link` | `result`, `nonce`（account link 事件） |

**缺漏**（對照現行 Webhook 事件）：`Event` 沒有 `webhookEventId`、`deliveryContext`、`mode`、`unsend`（含 `messageId`）、`videoPlayComplete`、`membership`、`things`；`Message` 沒有 `quotedMessageId`、`markAsReadToken`、`keywords`、`imageSet`。事件種類沒有列舉，呼叫端自己用字串比對 `Event.type`。

**簽章驗證**：`Utility.SignatureValidation(ControllerBase ApiController, string LineChannelSecret)` → `bool`。讀 `X-Line-Signature` 標頭，把整個 `Request.Body` 讀乾後計算 HMAC-SHA256 比對 Base64（實作細節與風險見 §3-#9、§4）。

**`LineWebHookControllerBase : ControllerBase`**（`[ApiController]`／`[Route]` 要由子類別自己加）：

| 成員 | 行為 |
|---|---|
| `string ChannelAccessToken { get; set; }` | 子類別在動作方法中自行賦值，沒有建構式／DI |
| `ReceivedMessage ReceivedMessage { get; }` | 第一次存取時 **同步** 讀 `Request.Body` → 存進 `PostData` → `Utility.Parsing` → 快取於私有欄位 |
| `string PostData { get; set; }` | 原始請求本文 |
| `PushMessage(...)` ×9 | 字串、Uri、Uri+Uri、貼圖、`MessageBase`、`List<MessageBase>`、Imagemap、Buttons／Confirm／Carousel（**漏掉 ImageCarouselTemplate**） |
| `PushMessagesWithJSON`、`PushMulticast(List<string>, List<string>)` | |
| `ReplyMessage(...)` ×11 | 含 Imagemap、4 種 Template、字串、貼圖、Uri 等；`ReplyMessageWithJSON` |
| `GetUserUploadedContent(contentId)`、`GetUserInfo(uid)`、`GetGroupSummary(gid)`、`GetMembersInGroupCount(gid)`、`GetMembersInRoomCount(rid)` | 皆 `new Bot(token)` 後轉呼叫；token 為空會丟 `Exception("ChannelAccessToken cannot be empty.")` |

生命週期：ASP.NET Core 每個請求建立一個控制器實例 → 子類別動作方法設定 `ChannelAccessToken` → 存取 `ReceivedMessage`（觸發讀 body + 解析）→ 迴圈處理 `events` → 呼叫 `ReplyMessage`／`PushMessage` → 子類別自行 `return Ok()`。基底類別**不驗簽、不回 200、不做事件分派、不管理 replyToken**。

### 2.5 Rich Menu

**模型**（`isRock.LineBot.RichMenu`）：`RichMenuItem`(`richMenuId`, `size`, `selected`, `name`, `chatBarText`, `areas`；預設 2500×1686)、`Area`(`bounds`, `action`[`ActionConverter`])、`Bounds`(x,y,width,height)、`Size`(width,height)、`GetRichMenuListResponse`(`richmenus`)、`richMenuIdResponse`(`richMenuId`)。

**API**（均為 `Utility` 靜態方法，token 最後參數）：

| 方法 | HTTP |
|---|---|
| `CreateRichMenu(RichMenuItem)` | `POST https://api.line.me/v2/bot/richmenu`（檢查 size 必為 2500×1686 或 2500×843） |
| `CreateRichMenu(RichMenuItem, byte[] png)` | 上一個 + `UploadRichMenuImage` |
| `CreateRichMenu(RichMenuItem, Uri pngUrl)` | 先 `new WebClient().DownloadData(url)` 再上傳 |
| `UploadRichMenuImage(richMenuId, byte[])` | `POST https://api-data.line.me/v2/bot/richmenu/{id}/content`（`Content-Type: image/png` 寫死） |
| `DownloadRichMenuImage(richMenuId)` | `GET https://api-data.line.me/v2/bot/richmenu/{id}/content` |
| `GetRichMenuList()` | `GET https://api.line.me/v2/bot/richmenu/list`（404 回 `null`） |
| `GetRichMenu(id)` | `GET https://api.line.me/v2/bot/richmenu/{id}`（404 回 `null`） |
| `DeleteRichMenu(id)` | `DELETE https://api.line.me/v2/bot/richmenu/{id}` |
| `SetDefaultRichMenu(id)` | `POST https://api.line.me/v2/bot/user/all/richmenu/{id}` |
| `GetDefaultRichMenuID()` | `GET https://api.line.me/v2/bot/user/all/richmenu`（404 回 `null`） |
| `CancelDefaultRichMenu()` | `DELETE https://api.line.me/v2/bot/user/all/richmenu` |
| `LinkRichMenuToUser(richMenuId, userId)` | `POST https://api.line.me/v2/bot/user/{uid}/richmenu/{rid}` |
| `GetRichMenuIdOfUser(userId)` | `GET https://api.line.me/v2/bot/user/{uid}/richmenu`（404 回 `null`） |
| `UnlinkRichMenuFromUser(userId)` | `DELETE https://api.line.me/v2/bot/user/{uid}/richmenu`（404 靜默吞掉） |
| `LinkRichMenuToUsers(richMenuId, IEnumerable<string> userIds)` | `POST https://api.line.me/v2/bot/richmenu/bulk/link` |
| `UnlinkRichMenuFromUsers(userIds)` | `POST https://api.line.me/v2/bot/richmenu/bulk/unlink` |
| `ReplaceOrUnlinkRichMenusInBatch(RichMenuBatchRequest)` | `POST https://api.line.me/v2/bot/richmenu/batch`（客戶端先逐一驗 operation） |
| `ValidateRichMenuBatchRequest(request)` | `POST https://api.line.me/v2/bot/richmenu/validate/batch` |
| `GetRichMenuBatchProgress(requestId)` | `GET https://api.line.me/v2/bot/richmenu/progress/batch?requestId=...` |
| `CreateRichMenuAlias(aliasId, richMenuId)` | `POST https://api.line.me/v2/bot/richmenu/alias` |
| `UpdateRichMenuAlias(aliasId, newRichMenuId)` | `POST https://api.line.me/v2/bot/richmenu/alias/{aliasId}` |
| `GetRichMenuAlias(aliasId)` | `GET https://api.line.me/v2/bot/richmenu/alias/{aliasId}` |
| `GetRichMenuAliasList()` | `GET https://api.line.me/v2/bot/richmenu/alias/list` |
| `DeleteRichMenuAlias(aliasId)` | `DELETE https://api.line.me/v2/bot/richmenu/alias/{aliasId}` |

巢狀型別（`Utility` 內）：`BulkLinkRequest`、`BulkUnlinkRequest`、`RichMenuBatchRequest`(`resumeRequestKey`, `operations`)、`RichMenuBatchOperation`(`type`, `fromRichMenuId`, `toRichMenuId`, `richMenuId`)、`RichMenuBatchProgressResponse`(`phase`, `succeededCount`, `failedCount`, `acceptedTime`, `startedTime`, `completedTime`)、`CreateRichMenuAliasRequest`、`UpdateRichMenuAliasRequest`、`RichMenuAlias`、`RichMenuAliasListResponse`。

注意：`RichMenuItem` 沒有任何 alias 欄位，也沒有 `richmenuswitch` 動作可用，所以 alias API 只能配合手寫 JSON 動作使用。

### 2.6 使用者／群組／聊天室／好友資料

| 方法 | HTTP |
|---|---|
| `Utility.GetUserInfo(uid)` / `Bot.GetUserInfo` / `Ctrl.GetUserInfo` → `LineUserInfo`(displayName, userId, pictureUrl, statusMessage) | `GET https://api.line.me/v2/bot/profile/{uid}`（`Utility` 內用 WebClient 直接寫，沒用 `LineHttpGet`） |
| `Utility.GetGroupMemberProfile(groupId, userId)` | `GET https://api.line.me/v2/bot/group/{gid}/member/{uid}` |
| `Utility.GetRoomMemberProfile(roomId, userId)` | `GET https://api.line.me/v2/bot/room/{rid}/member/{uid}` |
| `Utility.GetGroupMemberUserIDs(groupId)` → `MemberUserIDsResult`(memberIds, next) | `GET https://api.line.me/v2/bot/group/{gid}/members/ids`（**不能傳 `start`，無法翻頁**） |
| `Utility.GetRoomMemberUserIDs(roomId)` | `GET https://api.line.me/v2/bot/room/{rid}/members/ids`（同上） |
| `Utility.GetGroupSummary(groupId)` / `Bot` / `Ctrl` → `GroupSummary`(groupId, groupName, pictureUrl) | `GET https://api.line.me/v2/bot/group/{gid}/summary` |
| `Utility.GetMembersInGroupCount(groupId)` / `Bot` / `Ctrl` → `int` | `GET https://api.line.me/v2/bot/group/{gid}/members/count` |
| `Utility.GetMembersInRoomCount(roomId)` / `Bot` / `Ctrl` → `int` | `GET https://api.line.me/v2/bot/room/{rid}/members/count` |
| `Utility.LeaveGroup(groupId)` | `POST https://api.line.me/v2/bot/group/{gid}/leave` |
| `Utility.LeaveRoom(roomId)` | `POST https://api.line.me/v2/bot/room/{rid}/leave` |
| `Utility.Leave(groupId)` / `Bot.Leave`（`[Obsolete]`） | 轉呼叫 `LeaveGroup` |
| `Utility.GetUserUploadedContent(contentId)` / `Bot` / `Ctrl` → `byte[]` | `GET https://api-data.line.me/v2/bot/message/{id}/content`（同步下載整個 byte[]；沒有 preview、沒有 transcoding 查詢） |
| `Utility.GetBotInfo()` → `BotInfo`(userId, basicId, displayName, pictureUrl, chatMode, markAsReadMode) | `GET https://api.line.me/v2/bot/info` |

**沒有**：取得追蹤者 userId 清單、mark as read、`/v2/bot/user/{id}/linkToken` 實用版（見 §2.12）。

### 2.7 配額、洞察（Insight）、Demographic

全部是 `Bot` 實例方法。凡帶 `UTCDate` 的方法，內部 `UTCDate.AddHours(9).ToString("yyyyMMdd")`（LINE 以 JST 計日）。

| 方法 → 回傳型別 | HTTP |
|---|---|
| `GetTheTargetLimitForAdditionalMessages()` → `GetTheTargetLimitForAdditionalMessagesResult`(`type`, `value`) | `GET https://api.line.me/v2/bot/message/quota` |
| `GetNumberOfMessagesSentThisMonth()` → `...Result`(`totalUsage` 型別為 string) | `GET https://api.line.me/v2/bot/message/quota/consumption` |
| `GetNumberOfSentReplyMessages(date)` → `GetDeliveryResult`(status, success) | `GET https://api.line.me/v2/bot/message/delivery/reply?date=yyyyMMdd` |
| `GetNumberOfSentPushMessages(date)` | `GET https://api.line.me/v2/bot/message/delivery/push?date=` |
| `GetNumberOfSentMulticastMessages(date)` | `GET https://api.line.me/v2/bot/message/delivery/multicast?date=` |
| `GetNumberOfSentBroadcastMessages(date)` | `GET https://api.line.me/v2/bot/message/delivery/broadcast?date=` |
| `GetNumberOfMessageDeliveries(date)` → `GetNumberOfMessageDeliveriesResult`(broadcast, targeting, autoResponse, welcomeResponse, chat, apiBroadcast, apiPush, apiMulticast, apiReply) | `GET https://api.line.me/v2/bot/insight/message/delivery?date=` |
| `GetNumberOfFollowers(date)` → `GetNumberOfFollowersResult`(status, followers, targetedReaches, blocks) | `GET https://api.line.me/v2/bot/insight/followers?date=` |
| `GetFriendDemographics()` → `GetFriendDemographicsResult`(available, genders, ages, areas, appTypes, subscriptionPeriods) | `GET https://api.line.me/v2/bot/insight/demographic` |

Demographic 的 6 個型別（`Gender`, `Age`, `Area`, `AppType`, `SubscriptionPeriod`, `GetFriendDemographicsResult`）都是 `{ 名稱字串; double percentage }` 的單純 DTO。**沒有**：`/v2/bot/insight/message/event`（單則訊息互動）、statistics per unit、`/v2/bot/message/aggregation`、`/v2/bot/message/quota` 的 narrowcast 進度查詢。

### 2.8 LINE Login

**V1（`isRock.LineLoginV1`，整個命名空間 `[Obsolete("Line已改版，請用v2.1版本")]`）**：

| 方法 | HTTP |
|---|---|
| `Utility.GetToeknFromCode(code, ClientId, ClientSecret, redirect_uri)` → `GetTokenFromCodeResult`(mid, access_token, token_type, expires_in, refresh_token, scope) | `POST https://api.line.me/v1/oauth/accessToken`（form） |
| `Utility.GetUserProfile(access_token)` → `Profile`(displayName, mid, pictureUrl, statusMessage) | `GET https://api.line.me/v1/profile` |

**V2.1（`isRock.LineLoginV21`）**——只有三個方法：

| 方法 | HTTP |
|---|---|
| `Utility.GetTokenFromCode(code, ClientId, ClientSecret, redirect_uri)` → `GetTokenFromCodeResult`(id_token, access_token, token_type, expires_in[decimal], refresh_token, scope) | `POST https://api.line.me/oauth2/v2.1/token`（`application/x-www-form-urlencoded`，`grant_type=authorization_code`） |
| `Utility.GetUserProfile(access_token)` → `Profile`(displayName, userId, pictureUrl, statusMessage) | `GET https://api.line.me/v2/profile` |
| `Utility.GetFriendshipStatus(access_token)` → `bool`（`friendFlag`） | `GET https://api.line.me/friendship/v1/status` |

**缺漏**：授權網址組裝（無 `access.line.me`）、state／nonce、PKCE（`code_verifier` 參數）、refresh token、revoke、verify access token（`/oauth2/v2.1/verify`）、id_token 驗證（`/oauth2/v2.1/verify` 或本地 JWT）、userinfo（OpenID）。

### 2.9 LINE Notify

整個 `isRock.LineNotify.Utility` 類別與 4 個結果／模型型別皆 `[Obsolete("此類別已過時")]`；`API_Rate_Limit` 為無人使用的空殼模型。

| 方法 | HTTP |
|---|---|
| `Utility.GetTokenFromCode(code, ClientId, ClientSecret, redirect_uri)` → `GetTokenFromCodeResult`(status, message, access_token)；`GetToeknFromCode` 為拼字錯誤墊片 | `POST https://notify-bot.line.me/oauth/token`（form） |
| `Utility.SendNotify(NotifyToken, message)` → `SendNotifyResult`(status, message) | `POST https://notify-api.line.me/api/notify`（form：message） |
| `Utility.SendNotify(NotifyToken, message, imageThumbnail, imageFullsize, stickerPackageId, stickerId, notificationDisabled)` | `POST https://notify-api.line.me/api/notify`（form） |
| `Utility.GetStatus(access_token)` → `GetStatusResult`(status, message, targetType, target) | `GET https://notify-api.line.me/api/status` |
| `Bot.SendNotify(...)` ×2（`virtual`） | 轉呼叫上述；`Bot` 本身並未標 `[Obsolete]` |

### 2.10 LIFF

`isRock.LIFF.Utility`（皆走 `api.line.me/liff/v1/apps`）：

| 方法 | HTTP |
|---|---|
| `static string AddLiffApp(token, Uri LiffAppUrl, ViewType viewType)` → 回傳 `line://app/{liffId}` | `POST https://api.line.me/liff/v1/apps`（body：`{"view":{"type":..,"url":..}}`，字串 Replace 組出） |
| `static GetAllLiffAppsResult GetAllLiffApps(token)` | `GET https://api.line.me/liff/v1/apps` |
| `static void DeleteLiffApp(token, liffId)` | `DELETE https://api.line.me/liff/v1/apps/{liffId}` |
| `string UpdateLiffApp(...)`（**非 static**） | `throw new NotImplementedException()` |

模型：`App`(liffId, view)、`View`(type, url)、`ViewType`(compact/tall/full)、`GetAllLiffAppsResult`(apps)。`App`／`View` 缺少 `description`、`features`、`permanentLinkPattern`、`scope`、`botPrompt` 等現行欄位。

### 2.11 其他（Webhook endpoint 設定、短期 token、JSON 擴充）

| 方法 | HTTP |
|---|---|
| `Utility.GetWebhookEndpointInformation()` → `WebhookEndpointInfo`(endpoint, active[string]) | `GET https://api.line.me/v2/bot/channel/webhook/endpoint` |
| `Utility.SetWebhookEndpointURL(Uri)` → `dynamic` | `PUT https://api.line.me/v2/bot/channel/webhook/endpoint`（字串拼 `{"endpoint":"..."}`） |
| `Utility.TestWebhookEndpoint()` → `TestWebhookEndpointResult`(success, timestamp, statusCode, reason, detail) | `POST https://api.line.me/v2/bot/channel/webhook/test`（body 空字串） |
| `Utility.TestWebhookEndpoint(Uri)` | `POST https://api.line.me/v2/bot/channel/webhook/test`（`{"endpoint":"..."}`） |
| `Utility.IssueChannelAccessToken(client_id, client_secret)` → `ShortLivedToken`(access_token, expires_in, token_type) | `POST https://api.line.me/v2/oauth/accessToken`（form，`grant_type=client_credentials`，字串串接） |
| `Utility.RevokeChannelAccessToken(access_token)` | `throw new NotSupportedException("Not Support Yet.")` |
| `Utility.IssueLinkToken(userId)`（`[Obsolete("not implement yet")]`，回傳 void，丟掉 linkToken） | `POST https://api.line.me/v2/bot/user/{uid}/linkToken` |
| `JsonExtensions.ToJson(this object)`（`isRock.LineBot.Extensions`） | 本地：`JsonConvert.SerializeObject`，null 回 `"null"`，例外回 `"Exception"` |
| `isRock.LINE.Utility`（internal） | `LineHttpPost(string json | byte[] png)`、`LineHttpPost<T>`、`LineHttpPut`、`LineHttpDelete`、`LineHttpGet`、`LineHttpGet<T>`、`LineHttpGetBytes`、`GetWebException` |

### 2.12 net45 專屬（附註；僅 XML／字串證據）

`isRock.LineBot.Conversation.*`（`InformationCollector<T>(userId)`、`ConversationState<T>`、`ConversationEntity`、`Question/Order/ButtonsTemplateQuestion` Attribute、`ProcessResultStatus`{Processed, Done, Pass, Exception, Break, InputDataFitError}、`LoadConversationStatus/SaveConversationStatus/Process(Event, bool)`）——以屬性宣告問題、逐題收集使用者輸入的對話狀態機；`isRock.MsQnAMaker.Client`（beta 與 GA 兩種建構式，`GetResponse(query)`）；`LineBotHelper`。net45 DLL 另有 `https://trialbot-api.line.me/`（古早 Trial Bot 端點）字串。這些未出現在 netstandard2.0 版本。

---

## 3. 值得注意的實作細節與技巧

### 3-1. Imagemap 圖片：不上傳，用 `?` 技巧騙過「多尺寸圖」規則
**`isRock.LineBot/ImagemapMessage.cs`**。LINE 的 imagemap 會把 `{baseUrl}/1040` 等寬度字尾附加到 baseUrl 上抓不同尺寸的圖。SDK 完全不處理圖片上傳或縮圖，單圖建構式改成在網址尾端補一個 `?`，讓附加的 `/1040` 變成 query 的一部分，任何寬度都取到同一張圖：
```csharp
public ImagemapMessage(Uri Image, Uri Action) {
    ...
    baseSize = new Size(1040, 1040);
    baseUrl  = new Uri(Image.AbsoluteUri + "?");
    actions.Add(new ImagemapUriAction { area = new ImagemapArea(0,0,1040,1040), linkUri = Action });
}
```
（此為依程式碼對 LINE 規則的推論，未實測。）

### 3-2. 訊息 JSON 多型：靠「子類別建構式寫 discriminator + 序列化執行期型別」，不用 JsonConverter
**`MessageBase.cs`、`TemplateActionBase.cs`、各 `*Action.cs`**。`type` 是 `protected` 屬性但掛 `[JsonProperty]` 讓 Newtonsoft 仍序列化；序列化時強制轉 `object` 以走執行期型別：
```csharp
public abstract class TemplateActionBase { [JsonProperty] protected string type { get; set; } }
public UriAction() { base.type = "uri"; }
// MessageBase.ToJSON():
return JsonConvert.SerializeObject((object)this);
```
只做序列化方向；反序列化多型僅 Rich Menu 的 `ActionConverter`（見 3-12）。

### 3-3. `MessageBase.ToJSON()` 順手做 QuickReply 規則檢查
**`MessageBase.cs`**。空清單轉 null（避免送出空 `quickReply`）、超過 13 個丟例外；但只有走 `ToJSON()` 的路徑才檢查，舊式 Template 手工拼 JSON 路徑不會：
```csharp
if (quickReply?.items != null && quickReply.items.Count() <= 0) quickReply = null;
if (quickReply?.items != null && quickReply.items.Count() > 13)
    throw new Exception("Quick reply button objects. Max: 13 objects");
return JsonConvert.SerializeObject((object)this);
```

### 3-4. 惰性預設物件（sender／quickReply）
**`MessageBase.cs`、`TextMessage.cs`**。`sender` 的 getter 在 null 時自動建立空 `Sender`；每個訊息建構式都 `base.quickReply = new QuickReply()`。好處是使用者可直接 `msg.quickReply.items.Add(...)`、`msg.sender.name = "x"` 而不用 new；代價是序列化結果永遠帶 `"sender":{"name":null,"iconUrl":null}`（沒設 `NullValueHandling.Ignore`，靠 LINE 容忍 null 欄位——待驗證）。

### 3-5. HttpClient／WebClient 包裝：internal `LineHttp*` 五個方法 + 泛型版
**`isRock.LINE/Utility.cs`**。沒有 HttpClient，每次呼叫 `new WebClient()`，設兩個標頭；POST 有 `string json` 與 `byte[] png` 兩個多載（後者 `Content-Type: image/png`）；DELETE 因 `WebClient` 沒有專用方法，用 `UploadValues(endpoint, "DELETE", new NameValueCollection())` 湊出來；泛型版只是 `JsonConvert.DeserializeObject<T>(LineHttpGet(...))`：
```csharp
webClient.Headers.Add("Content-Type", "application/json");
webClient.Headers.Add("Authorization", "Bearer " + ChannelAccessToken);
byte[] r = webClient.UploadData(Endpoint, Encoding.UTF8.GetBytes(JSON));
...
webClient.UploadValues(Endpoint, "DELETE", new NameValueCollection());
```

### 3-6. 錯誤處理：把 LINE 回應本文塞進例外訊息
**`isRock.LINE/Utility.cs`**。`GetWebException` 讀取 `WebException.Response` 的本文（讀不到回空字串），新例外訊息含 HTTP 錯誤、LINE 回應 JSON、Endpoint、請求 JSON，並保留 `innerException`。診斷資訊很足（token 不會被寫進訊息），但有請求內容外洩到日誌的風險：
```csharp
throw new Exception($"\n LineHttpPost Exception:{ex.Message} \nResponse:{webException} \nEndpoint:{Endpoint} \nJSON:{JSON}", ex);
```
`Utility` 內的舊方法（`PushMessage`、`GetUserInfo`、`LeaveGroup` 等）則自己內嵌 `ex.Response.GetResponseStream()` 而沒有 null 判斷，連線失敗時 `Response` 為 null 會丟 `NullReferenceException` 蓋掉原例外。

### 3-7. 404 轉 null 的 Rich Menu 讀取慣例
**`isRock.LineBot/Utility.cs`**（`GetRichMenuList`、`GetRichMenu`、`GetDefaultRichMenuID`、`GetRichMenuIdOfUser`、`UnlinkRichMenuFromUser`）。透過 `InnerException` 取 `HttpWebResponse.StatusCode`，404 回 null 或靜默返回，其他重新擲回：
```csharp
catch (Exception ex) {
    if (ex != null && (WebException)ex.InnerException != null) {
        var r = (HttpWebResponse)((WebException)ex.InnerException).Response;
        if (r != null && r.StatusCode == HttpStatusCode.NotFound) return null;
    }
    throw ex;   // 重擲會重設堆疊
}
```
同一段樣板複製貼上 5 次；`(WebException)ex.InnerException` 是硬轉型，若 inner 是 `JsonException` 會丟 `InvalidCastException`。

### 3-8. 日期參數統一轉 JST
**`Bot.cs`**（8 個 quota／delivery／insight 方法）。方法簽名叫 `UTCDate`，內部加 9 小時再格式化，貼合 LINE 文件「日期為 UTC+9」的規則：
```csharp
UTCDate = UTCDate.AddHours(9.0);
string text = UTCDate.ToString("yyyyMMdd");
... "https://api.line.me/v2/bot/message/delivery/reply?date=" + text
```
（呼叫端若傳入 Local 或 Unspecified kind 的時間會偏移；沒有 `ToUniversalTime()`。）

### 3-9. 簽章驗證：直接讀 `ControllerBase.Request`
**`Utility.cs`（`SignatureValidation`）**。把整個 body 讀成字串後以 channel secret 為 key 算 HMAC-SHA256、Base64 後用 `==` 比對：
```csharp
using (var sr = new StreamReader(ApiController.Request.Body)) { s = sr.ReadToEnd(); }
var key = Encoding.UTF8.GetBytes(LineChannelSecret);
if (Convert.ToBase64String(new HMACSHA256(key).ComputeHash(Encoding.UTF8.GetBytes(s))) == text) return true;
```
特點是一行呼叫就能驗；缺點見 §4（吃掉 body、非固定時間比較、`HMACSHA256` 未釋放、同步 `ReadToEnd` 在 Kestrel 預設禁止同步 IO 時會丟例外，除非 `AllowSynchronousIO`）。

### 3-10. Controller 基底類的設計：惰性解析屬性 + 每次 `new Bot`
**`LineWebHookControllerBase.cs`**。`ReceivedMessage` getter 首次存取才讀 body；`PostData` 順手保留原始本文；其餘方法統一走私有 `GetBotInstance()`（每次 `new Bot(token)`，輕量所以無所謂）：
```csharp
using (var sr = new StreamReader(base.Request.Body, Encoding.UTF8))
    text = sr.ReadToEndAsync().Result;      // 同步阻塞
PostData = text;
receivedMessage = Utility.Parsing(text);
```
`StreamReader` 的 `using` 會連同關閉 `Request.Body`，因此若同一請求先呼叫 `SignatureValidation` 再存取 `ReceivedMessage`（或反過來），第二次讀取會失敗或讀到空（依程式碼推論；除非自行 `EnableBuffering` 且不被 `Dispose` 影響，實務上很難成功）。

### 3-11. Webhook 解析的 `params` 補丁
**`Utility.cs`（`Parsing`）**。先對原始字串做一個奇怪的 Replace 再反序列化，目的是把 `params`（postback 的 datetimepicker 結果）改成 `Params`（C# 屬性名首字母大寫）：
```csharp
if (RawData.Contains("','params':{'datetime':'"))
    RawData = RawData.Replace("','params':{'datetime':'", "','Params':{'datetime':'");
return JsonConvert.DeserializeObject<ReceivedMessage>(RawData);
```
比對字串含**單引號**，真正的 LINE webhook 是雙引號 JSON，所以幾乎不會命中；而 Newtonsoft 預設屬性比對本來就不分大小寫，這段實際上是無效程式碼（推論）。`Postback.Params` 靠大小寫不敏感比對即可成功。

### 3-12. Rich Menu 動作的「嘗試轉型」反序列化器（有 bug）
**`isRock.LineBot.RichMenu/ActionConverter.cs`**。`CanConvert` 永遠回 `true`，`ReadJson` 依序對同一個 `JObject` 呼叫 `ToObject<MessageAction>()`、`ToObject<UriAction>()`…，以「非 null 就採用」為判斷：
```csharp
if (val.ToObject<MessageAction>() != null) return val.ToObject<MessageAction>();
if (val.ToObject<UriAction>() != null)     return val.ToObject<UriAction>();
...
```
`JToken.ToObject<T>()` 對物件幾乎不會回 null，所以永遠第一個命中 `MessageAction`：從 `GetRichMenu`／`GetRichMenuList` 讀回的每個 area action 都會被轉成 `MessageAction`（`uri`、`data` 等欄位遺失；protected `type` 會被 JSON 改寫為真正字串所以仍看得出類型）。這是依程式碼的推論，未實測。

### 3-13. 客戶端前置驗證 + 屬性 setter 驗證
**`ButtonsTemplate.cs`、`CarouselTemplate.cs`、`DateTimePickerAction.cs`、`QuickReplyItemBase.cs`、`ImagemapMessage.cs`、`Utility.cs`**。常見手法是在 setter 丟例外，讓錯誤在「賦值當下」而不是「HTTP 回 400」才暴露：
```csharp
set { if (!string.IsNullOrEmpty(value) && !Regex.IsMatch(value, "^#[0-9A-Fa-f]{6}$"))
        throw new Exception("imageBackgroundColor must be a valid RGB color value (e.g., #FFFFFF)");
      _imageBackgroundColor = value; }
```
同類型：`imageAspectRatio`（rectangle/square）、`imageSize`（cover/contain）、`mode`（date/time/datetime）、`imageUrl` 必須 https、`baseUrl` 必須 https。

### 3-14. Rich Menu 批次請求的預先驗證
**`Utility.cs`（`ReplaceOrUnlinkRichMenusInBatch`）**。對 `operations` 逐筆以 `switch` 檢查必填欄位，避免送出必定失敗的請求：
```csharp
switch (operation.type.Trim().ToLowerInvariant()) {
  case "link":   if (string.IsNullOrEmpty(op.fromRichMenuId) || string.IsNullOrEmpty(op.toRichMenuId)) throw ...; break;
  case "unlink": if (string.IsNullOrEmpty(op.richMenuId)) throw ...; break;
  case "unlinkall": break;
  default: throw new Exception("operation.type must be one of: link, unlink, unlinkAll.");
}
```
（驗證時不分大小寫，但送出時是原字串，若使用者傳 `"UNLINK"` 通過本地驗證卻會被 LINE 拒絕。）

### 3-15. 用「假值字串」再用 Replace 刪掉選填欄位
**`Utility.cs`（`PushTemplateMessage(ButtonsTemplate)`、`ReplyTemplateMessage(ButtonsTemplate)`）**。因為用 `string.Format` 拼 JSON，選填欄位只能事後刪除：先塞入假 URI，格式化完再把那段文字用字串取代拿掉：
```csharp
if (TemplateMessage.thumbnailImageUrl == null)
    TemplateMessage.thumbnailImageUrl = new Uri("dmy://dummy");
...
text2 = text2.Replace("\"thumbnailImageUrl\": \"dmy://dummy/\",", "");
text2 = text2.Replace("\"title\": \"\",", "");
text2 = text2.Replace("\"text\": \"\",", "");
```
同時也意味著 `ButtonsTemplate` 的 `imageAspectRatio`／`imageSize`／`imageBackgroundColor` 在這條舊路徑中**根本沒被送出**（樣板字串只有 `thumbnailImageUrl`、`title`、`text`、`actions`）；只有改用 `new TemplateMessage(buttons)` 走 `MessageBase` 管線才會序列化完整屬性。

### 3-16. 兩條並行的送訊息路徑（舊字串樣板 vs 新物件管線）
**`Utility.cs` vs `Bot.cs`**。Readme 歷史顯示 2018-10（v0.8.4）才加入「Template 進 MessageBase」。於是同一件事有兩種實作：`Utility.PushTemplateMessage(...)`（拼字串）與 `Bot.PushMessage(to, new TemplateMessage(tpl))`（物件管線）。對 `Bot` 呼叫舊的 `PushMessage(to, ButtonsTemplate)` 重載仍轉到前者。

### 3-17. 參數驗證式的 API 包裝（loading animation）
**`Bot.cs`**。LINE 規定秒數 5–60 且為 5 的倍數，SDK 在送出前檢查並區分 `ArgumentException`；但 HTTP 失敗一律吞掉回 `false`：
```csharp
if (loadingSeconds < 5 || loadingSeconds > 60) throw new ArgumentException("loadingSeconds must be between 5-60.");
if (loadingSeconds % 5 != 0) throw new ArgumentException("loadingSeconds must be divisible by 5.");
try { LineHttpPost(...); } catch (Exception) { return false; }
```

### 3-18. 拼字錯誤的相容墊片
**`ReceievedMessage.cs`、`LineNotify/Utility.cs`、`LineLoginV1`**。保留錯字型別／方法並標 `[Obsolete("拼字錯誤，請改為ReceivedMessage")]`，新舊並存以免破壞 API；但 `LineLoginV21.GetTokenFromCode` 的例外訊息仍寫 `"GetToeknFromCode: "`。

### 3-19. 尺寸型別：System.Drawing.Size 與自訂轉換器
**`ImagemapMessage.cs`、`SizeJsonConverter.cs`**。`baseSize` 用 BCL 的 `System.Drawing.Size`（序列化預設會變成 `{"IsEmpty":false,"Width":..,"Height":..}`），所以另寫轉換器轉成 `{"width":..,"height":..}`；Rich Menu 的 `size` 則是 SDK 自定義的 `Size` DTO，同名不同型，容易混淆：
```csharp
[JsonConverter(typeof(SizeJsonConverter))] public Size baseSize { get; set; }
...
obj.Add("width", size.Width); obj.Add("height", size.Height);
```

### 3-20. 對 reply token 一次性規則：SDK 沒有任何處理
全碼庫沒有追蹤、去重、過期判斷。唯一的緩解是提供 `ReplyMessage(replyToken, List<MessageBase>)`（1–5 則）讓呼叫端把多則訊息塞進同一次 reply；已用過的 token 會得到 LINE 回應的 400，經 `LineHttpPost` 包成例外。

---

## 4. 設計缺陷／反模式／過時之處

| # | 類別 | 內容與證據 | 影響 |
|---|---|---|---|
| 1 | **字串拼 JSON（嚴重）** | 舊路徑 `Utility.PushMessage/ReplyMessage/PushImageMessage/PushStickerMessage/PushImageMapMessage/PushTemplateMessage/ReplyTemplateMessage…` 全用 `string.Format(@"{ 'to':'{0}', ... }")` + `Replace("'", "\"")`。使用者文字、altText、title、URL、userId 都未跳脫。 | 文字含 `\`、制表字元等會產生壞 JSON；`PushMessage(to, text)` 為了避開引號問題，把訊息中的 `"` **改成 `'`**（`Message.Replace("\"", "'")`），悄悄竄改使用者內容；`Message.Length < 0` 永遠為 false |
| 2 | **單引號被改成雙引號，毀損 JSON（嚴重）** | `Bot.PushMessage(string, MessageBase)`、`PushMessage(string, List<MessageBase>)`、`ReplyMessage(string, List<MessageBase>)`、`PushBroadcast`、`PushMulticast(..., List<MessageBase>)`、`ValidatePushMessage(MessageBase)` 都是 `$"{{ 'to':'{to}', 'messages':[ {Message.ToJSON()} ] }}".Replace("'", "\"")`——**插值後才 Replace**，所以序列化好的 JSON 內的每個 `'`（例如 `"It's"`）都被換成 `"`。 | 文字訊息含撇號（英文縮寫、中文語境的 `'`）時送出無效 JSON，LINE 回 400。同一類別中 `ReplyMessage(string, MessageBase)` 與 `ValidateReplyMessage(MessageBase)` 是「先 Replace 後 Format」，反而安全——一致性差，容易被不同路徑踩雷 |
| 3 | **`PushMessagesWithJSON` 會改壞使用者 JSON** | `string text2 = JSONMessages; text2 = text2.Replace("'", "\"");`（`ReplyMessageWithJSON` 沒有這行）。又 `ValidatePushMessageWithJSON` 內 `JSONMessages.Replace("'", "\"");` 結果沒指派，是無效語句 | Flex JSON 的文字含 `'` 時 Push 會壞、Reply 不會 |
| 4 | **同步阻塞、無 async** | 全部 API 同步；`LineWebHookControllerBase.ReceivedMessage` 內 `ReadToEndAsync().Result`；`SignatureValidation` 同步 `ReadToEnd()` | ASP.NET Core 預設 `AllowSynchronousIO=false`，`ReadToEnd()` 會丟 `InvalidOperationException`；高併發佔用執行緒 |
| 5 | **WebClient（已過時）每次 new、無 HttpClientFactory** | `new WebClient()` 39 處；.NET 6+ 標記 `SYSLIB0014`；沒有 timeout、cancellation、重試、`X-Line-Retry-Key`、proxy 設定、`IHttpClientFactory` | 難以測試、難以設定逾時／重試；每次呼叫配置新物件 |
| 6 | **TLS 全域副作用** | `LineNotify/Utility.cs` 3 處 `ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;` | 呼叫一次 Notify，整個行程（.NET Framework）的預設 TLS 版本被鎖成 1.2（只影響 `isRock.LineNotify`，且該服務已結束） |
| 7 | **例外吞掉／失真** | `Bot.Validate*`、`DisplayLoadingAnimation` 任何例外一律 `return false`；`GetRichMenu*` 404 以外 `throw ex`（重設堆疊）；多處 `ex.Response.GetResponseStream()` 無 null 檢查；`(WebException)ex.InnerException` 硬轉型 | 拿不到 LINE 驗證錯誤原因；連線失敗被 `NullReferenceException`／`InvalidCastException` 蓋掉；通用 `System.Exception` 無法分辨狀態碼 |
| 8 | **ActionConverter 永遠回 `MessageAction`** | 見 §3-12 | 讀回的 Rich Menu 動作失真，不能安全做「讀 → 改 → 寫」 |
| 9 | **QuickReply 的 `type` 值設錯（高機率會被 LINE 拒絕）** | `QuickReplyCameraAction`、`QuickReplyCamerarollAction`、`QuickReplyLocationAction` 的 ctor 都寫 `base.type = "location"`；`QuickReplyDatetimePickerAction` 寫 `"datetimepicker"`。LINE 文件規定 quick reply item 的 `type` 固定為 `"action"`，只有 `QuickReplyMessageAction`／`QuickReplyPostbackAction` 是對的。 | 其餘四種實際上多半送不出去（依文件推論，未實測；也可能因 LINE 容錯而被忽略 `type`——待驗證）。也是典型複製貼上錯誤 |
| 10 | **Flex 宣稱支援卻無模型** | nuspec／AssemblyDescription 寫「Flex Message supported」，實際只有 `*WithJSON` 吃字串 | 與宣傳不符；且受缺陷 #3 影響 |
| 11 | **功能缺漏／落後** | 無 narrowcast、audience、aggregation、coupon、mark as read、取得追蹤者 ID、richmenuswitch 動作、`textV2`／Emoji／Mention 送出模型、Flex 模型、videoPlayComplete／unsend／membership 等事件、`webhookEventId`／`mode`／`deliveryContext`、群組成員 ID 翻頁（`start`）、LINE Login 授權網址／PKCE／refresh／revoke／verify／userinfo、LIFF update | 只覆蓋 Messaging API 的約 2019–2022 年水準，外加少量近年的 rich menu batch／alias |
| 12 | **LINE Notify 已終止，卻仍掛在 `Bot.SendNotify`** | `isRock.LineNotify.*` 全標 `[Obsolete]`，但 `Bot.SendNotify`（virtual）仍轉呼叫、本身未標過時；端點 `notify-api.line.me`、`notify-bot.line.me`。LINE Notify 已於 2025-03-31 結束服務（依我所知，建議主代理再核對官方公告）。 | 呼叫必然失敗；死碼 |
| 13 | **LINE Notify 長訊息切割漏尾巴** | `Split(str, chunkSize)` = `Enumerable.Range(0, str.Length / chunkSize).Select(i => str.Substring(i*chunkSize, chunkSize))`，整除後的餘數段被丟掉；切割後只回傳最後一次結果，且圖片／貼圖只在 ≤ 980 字的路徑附帶 | 超長訊息尾端遺失 |
| 14 | **LINE Login V1 過時** | `isRock.LineLoginV1` 打 `api.line.me/v1/oauth/accessToken`、`/v1/profile`，已全標 `[Obsolete]` | 死碼 |
| 15 | **LINE Login V2.1 不安全且不完整** | 沒有 state／nonce／PKCE，沒有 id_token 驗證；`client_secret` 以參數傳遞；缺 refresh／revoke／verify | 無法完成安全的 OIDC 流程（需呼叫端自己補） |
| 16 | **SignatureValidation 設計問題** | 吃掉並關閉 `Request.Body`；`==` 比對非固定時間；`new HMACSHA256(...)` 未 `Dispose`；`value.ToArray()[0]`；被限定為 `ControllerBase`（Minimal API／Middleware 無法用） | 與 `ReceivedMessage` 互相衝突；時序攻擊風險低但屬壞習慣 |
| 17 | **程式碼重複／god class** | `PushMulticast(List<string>,List<string>)` 在 `Bot` 與 `LineWebHookControllerBase` 各複製一份；5 段 404→null 樣板；Reply／Push 兩組 Template 實作幾乎逐字相同；`Utility.cs` 1,338 行；`Bot.PushMulticast` 兩個多載的人數上限不一致（< 150 vs ≤ 500）；Bot／Controller 字串限制 1800 字（LINE 實際為 5000） | 維護困難、行為不一致 |
| 18 | **會修改呼叫端的輸入集合** | `PushMulticast(List<string> ToUsers, ...)` 就地 `ToUsers[i] = ToUsers[i].Trim()`；文字版就地改寫 `TextMessages[i]`（含 `\n` → `\\n`、`"` → `'`） | 副作用難察覺 |
| 19 | **介面／可測試性差** | 沒有 `IBot` 介面、靜態方法每次帶 token、`Bot` 僅有部分方法為 `virtual`、無 DI、無 Options；token 靠子類別手動設定 | 難以在 ASP.NET Core DI 中整合與單元測試 |
| 20 | **模型不完整／鬆散型別** | `Event`／`Message` 是欄位聯集式扁平類別；`fileSize`、`totalUsage`、`active` 等數值／布林以 string 宣告；`SetWebhookEndpointURL` 回 `dynamic`；`TemplateMessageBase` 有 `altText`、`type` 屬性會被一併序列化到 `template` 內（多餘欄位）；`TemplateMessage` 以 `new string type => "template"` 隱藏基底成員 | 型別安全差；序列化行為依賴 Newtonsoft 對隱藏成員的處理（推論） |
| 21 | **未實作的空殼** | `Utility.RevokeChannelAccessToken` 丟 `NotSupportedException`；`LIFF.Utility.UpdateLiffApp` 丟 `NotImplementedException`（且非 static）；`Utility.IssueLinkToken` 標記「not implement yet」、回傳 void 丟掉 linkToken；`API_Rate_Limit` 無人使用 | 公開 API 表面積大於實際功能 |
| 22 | **Rich Menu 限制過嚴且圖片格式寫死** | `CreateRichMenu` 只允許 2500×1686／2500×843；`UploadRichMenuImage` 的 `Content-Type` 固定 `image/png`（JPEG 會被標錯） | 擋掉 LINE 也接受的其他尺寸（800×540、1200×810 等）與 JPEG（依 LINE 文件，待驗證） |
| 23 | **Readme 過期** | 版本歷史停在 2022-05（2.5.33）；Readme 仍指向 Linebot-Demo 等舊範例 | 文件與實際版本（2.18.x）脫節 |
| 24 | **net45 / netstandard 功能分歧** | Conversation、MsQnAMaker、`Bot()` 無參建構式只存在 net45 | 同一個 NuGet 套件跨框架 API 不一致 |

---

## 5. LINE 端點總表（程式碼字串常值去重）

| # | URL（含 HTTP 動詞） | 用途 | 所在型別.方法 |
|---|---|---|---|
| 1 | `POST https://api.line.me/v2/bot/message/push` | 單一對象推播 | `Utility.Push*`、`Bot.PushMessage*`、`Utility.PushMessagesWithJSON` |
| 2 | `POST https://api.line.me/v2/bot/message/reply` | 回覆 | `Utility.Reply*`、`Bot.ReplyMessage*` |
| 3 | `POST https://api.line.me/v2/bot/message/broadcast` | 廣播 | `Bot.PushBroadcast` |
| 4 | `POST https://api.line.me/v2/bot/message/multicast` | 多人推播 | `Bot.PushMulticast`（2 個多載）、`LineWebHookControllerBase.PushMulticast` |
| 5 | `POST https://api.line.me/v2/bot/message/validate/push` | 驗證 push 訊息 | `Bot.ValidatePushMessage*` |
| 6 | `POST https://api.line.me/v2/bot/message/validate/reply` | 驗證 reply 訊息 | `Bot.ValidateReplyMessage*` |
| 7 | `POST https://api.line.me/v2/bot/chat/loading/start` | 顯示載入動畫 | `Bot.DisplayLoadingAnimation` |
| 8 | `GET https://api.line.me/v2/bot/message/quota` | 本月額度上限 | `Bot.GetTheTargetLimitForAdditionalMessages` |
| 9 | `GET https://api.line.me/v2/bot/message/quota/consumption` | 本月已用量 | `Bot.GetNumberOfMessagesSentThisMonth` |
| 10 | `GET https://api.line.me/v2/bot/message/delivery/reply?date=` | 當日 reply 則數 | `Bot.GetNumberOfSentReplyMessages` |
| 11 | `GET https://api.line.me/v2/bot/message/delivery/push?date=` | 當日 push 則數 | `Bot.GetNumberOfSentPushMessages` |
| 12 | `GET https://api.line.me/v2/bot/message/delivery/multicast?date=` | 當日 multicast 則數 | `Bot.GetNumberOfSentMulticastMessages` |
| 13 | `GET https://api.line.me/v2/bot/message/delivery/broadcast?date=` | 當日 broadcast 則數 | `Bot.GetNumberOfSentBroadcastMessages` |
| 14 | `GET https://api.line.me/v2/bot/insight/message/delivery?date=` | 洞察：訊息投遞數 | `Bot.GetNumberOfMessageDeliveries` |
| 15 | `GET https://api.line.me/v2/bot/insight/followers?date=` | 洞察：追蹤者數 | `Bot.GetNumberOfFollowers` |
| 16 | `GET https://api.line.me/v2/bot/insight/demographic` | 洞察：好友屬性 | `Bot.GetFriendDemographics` |
| 17 | `GET https://api.line.me/v2/bot/info` | Bot 資訊 | `Utility.GetBotInfo` |
| 18 | `GET https://api.line.me/v2/bot/profile/{userId}` | 使用者資料 | `Utility.GetUserInfo` |
| 19 | `GET https://api.line.me/v2/bot/group/{id}/summary` | 群組摘要 | `Utility.GetGroupSummary` |
| 20 | `GET https://api.line.me/v2/bot/group/{id}/members/count` | 群組人數 | `Utility.GetMembersInGroupCount` |
| 21 | `GET https://api.line.me/v2/bot/room/{id}/members/count` | 聊天室人數 | `Utility.GetMembersInRoomCount` |
| 22 | `GET https://api.line.me/v2/bot/group/{id}/members/ids` | 群組成員 ID | `Utility.GetGroupMemberUserIDs` |
| 23 | `GET https://api.line.me/v2/bot/room/{id}/members/ids` | 聊天室成員 ID | `Utility.GetRoomMemberUserIDs` |
| 24 | `GET https://api.line.me/v2/bot/group/{gid}/member/{uid}` | 群組成員資料 | `Utility.GetGroupMemberProfile` |
| 25 | `GET https://api.line.me/v2/bot/room/{rid}/member/{uid}` | 聊天室成員資料 | `Utility.GetRoomMemberProfile` |
| 26 | `POST https://api.line.me/v2/bot/group/{id}/leave` | 離開群組 | `Utility.LeaveGroup`（`Leave` 轉呼叫） |
| 27 | `POST https://api.line.me/v2/bot/room/{id}/leave` | 離開聊天室 | `Utility.LeaveRoom` |
| 28 | `GET https://api-data.line.me/v2/bot/message/{id}/content` | 下載使用者上傳內容 | `Utility.GetUserUploadedContent` |
| 29 | `GET https://api.line.me/v2/bot/channel/webhook/endpoint` | 查詢 webhook URL | `Utility.GetWebhookEndpointInformation` |
| 30 | `PUT https://api.line.me/v2/bot/channel/webhook/endpoint` | 設定 webhook URL | `Utility.SetWebhookEndpointURL` |
| 31 | `POST https://api.line.me/v2/bot/channel/webhook/test` | 測試 webhook | `Utility.TestWebhookEndpoint`（2 個多載） |
| 32 | `POST https://api.line.me/v2/bot/richmenu` | 建立 rich menu | `Utility.CreateRichMenu` |
| 33 | `POST https://api-data.line.me/v2/bot/richmenu/{id}/content` | 上傳 rich menu 圖 | `Utility.UploadRichMenuImage` |
| 34 | `GET https://api-data.line.me/v2/bot/richmenu/{id}/content` | 下載 rich menu 圖 | `Utility.DownloadRichMenuImage` |
| 35 | `GET https://api.line.me/v2/bot/richmenu/list` | rich menu 清單 | `Utility.GetRichMenuList` |
| 36 | `GET / DELETE https://api.line.me/v2/bot/richmenu/{id}` | 取得／刪除 rich menu | `Utility.GetRichMenu`／`DeleteRichMenu` |
| 37 | `POST / DELETE ... /v2/bot/user/all/richmenu[/{id}]`（`POST .../all/richmenu/{id}`、`GET`、`DELETE .../all/richmenu`） | 預設 rich menu 設定／查詢／取消 | `Utility.SetDefaultRichMenu`／`GetDefaultRichMenuID`／`CancelDefaultRichMenu` |
| 38 | `POST / GET / DELETE https://api.line.me/v2/bot/user/{uid}/richmenu[/{rid}]` | 個別使用者綁定／查詢／解除 | `Utility.LinkRichMenuToUser`／`GetRichMenuIdOfUser`／`UnlinkRichMenuFromUser` |
| 39 | `POST https://api.line.me/v2/bot/richmenu/bulk/link` | 批次綁定 | `Utility.LinkRichMenuToUsers` |
| 40 | `POST https://api.line.me/v2/bot/richmenu/bulk/unlink` | 批次解除 | `Utility.UnlinkRichMenuFromUsers` |
| 41 | `POST https://api.line.me/v2/bot/richmenu/batch` | 批次取代／解除 | `Utility.ReplaceOrUnlinkRichMenusInBatch` |
| 42 | `POST https://api.line.me/v2/bot/richmenu/validate/batch` | 驗證批次請求 | `Utility.ValidateRichMenuBatchRequest` |
| 43 | `GET https://api.line.me/v2/bot/richmenu/progress/batch?requestId=` | 批次進度 | `Utility.GetRichMenuBatchProgress` |
| 44 | `POST https://api.line.me/v2/bot/richmenu/alias` | 建立 alias | `Utility.CreateRichMenuAlias` |
| 45 | `POST / GET / DELETE https://api.line.me/v2/bot/richmenu/alias/{aliasId}` | 更新／取得／刪除 alias | `Utility.UpdateRichMenuAlias`／`GetRichMenuAlias`／`DeleteRichMenuAlias` |
| 46 | `GET https://api.line.me/v2/bot/richmenu/alias/list` | alias 清單 | `Utility.GetRichMenuAliasList` |
| 47 | `POST https://api.line.me/v2/bot/user/{uid}/linkToken` | 帳號連結 token（`[Obsolete]`） | `Utility.IssueLinkToken` |
| 48 | `POST https://api.line.me/v2/oauth/accessToken` | 短期 channel access token（舊版 v2） | `Utility.IssueChannelAccessToken` |
| 49 | `POST https://api.line.me/oauth2/v2.1/token` | LINE Login 以 code 換 token | `LineLoginV21.Utility.GetTokenFromCode` |
| 50 | `GET https://api.line.me/v2/profile` | LINE Login 使用者資料 | `LineLoginV21.Utility.GetUserProfile` |
| 51 | `GET https://api.line.me/friendship/v1/status` | 好友狀態 | `LineLoginV21.Utility.GetFriendshipStatus` |
| 52 | `POST https://api.line.me/v1/oauth/accessToken` | Login V1 換 token（過時） | `LineLoginV1.Utility.GetToeknFromCode` |
| 53 | `GET https://api.line.me/v1/profile` | Login V1 資料（過時） | `LineLoginV1.Utility.GetUserProfile` |
| 54 | `POST / GET https://api.line.me/liff/v1/apps`；`DELETE .../liff/v1/apps/{liffId}` | LIFF 新增／列表／刪除 | `LIFF.Utility.AddLiffApp`／`GetAllLiffApps`／`DeleteLiffApp` |
| 55 | `POST https://notify-api.line.me/api/notify` | LINE Notify 發送（已終止服務） | `LineNotify.Utility.SendNotify` ×2、`Bot.SendNotify` |
| 56 | `GET https://notify-api.line.me/api/status` | LINE Notify 狀態 | `LineNotify.Utility.GetStatus` |
| 57 | `POST https://notify-bot.line.me/oauth/token` | LINE Notify 換 token | `LineNotify.Utility.GetTokenFromCode` |

主機分布：`api.line.me`（主要）、`api-data.line.me`（內容與 rich menu 圖片，共 3 處）、`notify-api.line.me`（2）、`notify-bot.line.me`（1）。`access.line.me`：**0 處**。net45 DLL 額外含 `https://trialbot-api.line.me/` 字串（netstandard 版沒有）。

---

## 6. 與 Ozakboy.Line 對照初判（初判，待主代理確認）

> 以下僅根據任務描述中提供的 Ozakboy.Line 功能清單與本報告的反編譯結果比較；沒有實際閱讀 Ozakboy.Line 原始碼，凡標「待確認」處請主代理核對。

### (a) LineBotSDK 有、Ozakboy.Line（依清單）沒有或不確定的功能／方法

| 功能 | LineBotSDK 做法 | 備註 |
|---|---|---|
| Webhook endpoint 設定／測試 | `Get/SetWebhookEndpointURL`、`TestWebhookEndpoint`（含自訂 URL 版） | Ozakboy 清單無此項（待確認）；實作簡單，值得補齊 |
| 短期 channel access token（v2 舊制） | `IssueChannelAccessToken(client_id, client_secret)` | 注意：這是舊版短期 token；若 Ozakboy 需要，應該用 v2.1 JWT／stateless token 另做 |
| 發送數 per-type 日報 | `GetNumberOfSent{Reply,Push,Multicast,Broadcast}Messages(date)`（含 JST 轉換） | Ozakboy 有 quota／insight，但是否含 delivery per-type 待確認 |
| 洞察細項 | `GetNumberOfMessageDeliveries`、`GetNumberOfFollowers`、`GetFriendDemographics` + Demographic 6 個 DTO | Ozakboy 說有 insight，深度待確認 |
| 群組／聊天室輔助 | `GetGroupSummary`、`GetMembersInGroupCount/RoomCount`、`LeaveGroup/Room`、`GetGroupMemberProfile`、`GetRoomMemberProfile` | Ozakboy 清單僅列「群組／聊天室成員」，其餘待確認 |
| Rich menu 批次 API 家族 | `ReplaceOrUnlinkRichMenusInBatch`、`ValidateRichMenuBatchRequest`、`GetRichMenuBatchProgress`、`UnlinkRichMenuFromUsers` | Ozakboy 有 bulk link、ReplaceRichMenu 原子替換，但 `/richmenu/batch` 及進度查詢待確認 |
| LIFF Server API | `AddLiffApp`、`GetAllLiffApps`、`DeleteLiffApp` | Ozakboy 清單無 LIFF；但 LineBotSDK 版很簡陋且缺 update |
| Webhook 事件強型別範圍 | `Event` 直接有 `Beacon`、`Link`（account link）、`left`/`joined`（成員進出）、`Postback.Params`（datetimepicker）、`Message.emojis/mention/contentProvider/stickerResourceType/quoteToken` 型別 | Ozakboy 除 message/postback/follow/videoPlayComplete 外走 Raw JsonElement；LineBotSDK 有 beacon／memberJoined／memberLeft／accountLink 資料欄位（雖是扁平聯集，不是 discriminated union）。可考慮替 beacon、memberJoined/Left、unsend、accountLink 補強型別 |
| 送出訊息的 `quoteToken`、`sender` 屬性 | `MessageBase.quoteToken`、`MessageBase.sender` | Ozakboy 是否支援待確認 |
| 動作：clipboard、camera／cameraRoll／location | 皆有類別 | Ozakboy 清單說九種動作 + raw，應已涵蓋 |
| Postback 的 `inputOption`／`fillInText` | `PostbackAction` 有此二欄位 | Ozakboy 待確認 |
| Template 欄位驗證 | `imageAspectRatio`、`imageSize`、`imageBackgroundColor`(regex) | Ozakboy 有 template 四種，驗證程度待確認 |
| （僅 net45）對話狀態機、MS QnA Maker | `Conversation.InformationCollector<T>`、`MsQnAMaker.Client` | 不在 netstandard 版；QnA Maker 服務已停用（依我所知），不建議移植；對話收集器概念可與 Ozakboy 的關鍵字自動回覆／範本並存 |
| LINE Notify | `isRock.LineNotify` | 服務已終止，勿移植 |

### (b) 兩邊都有，但 LineBotSDK 做法有可取之處

1. **Setter／建構期 fail-fast 驗證**（`ButtonsTemplate.imageBackgroundColor` regex、`mode` 列舉值、`QuickReplyItemBase.imageUrl` https、`ImagemapMessage.baseUrl` https）：錯誤在賦值當下就暴露，比等 LINE 回 400 好除錯。Ozakboy 若用 record／init 屬性，可改在建構或 `Build()` 時集中驗證，而非丟泛型 `Exception`。
2. **Rich menu 批次請求預先驗證**（`operations` 逐項檢查必填欄位）：Ozakboy 的 `ReplaceRichMenu`／bulk link 可加同類本地驗證，減少無謂的 API 呼叫。
3. **「查不到回 null」的讀取 API**（`GetRichMenu`、`GetDefaultRichMenuID`、`GetRichMenuIdOfUser` 404 → null）：使用端免 try/catch；可學其意圖，但應用明確的 `NotFound` 結果型別而非吞掉所有 InnerException 判斷。
4. **錯誤訊息資訊量**：例外含 Endpoint、LINE 回應 JSON 與請求 JSON，除錯效率高；Ozakboy 可保留這類資訊，並加上脫敏（請求內容可能含 userId／訊息內容）及結構化例外屬性（狀態碼、`x-line-request-id`）。
5. **QuickReply 的工廠式子類別 + 可選 `imageUrl` 多載**，以及 `TextMessage.quickReply.items.Add(...)` 的惰性預設容器，使用起來直覺；Ozakboy 的 13 顆限制可照 `ToJSON()` 的集中檢查位置實作（LineBotSDK 把檢查放在序列化出口，單一入口）。
6. **Imagemap 單圖 `?` 技巧**（見 §3-1）：可作為 Ozakboy imagemap 範例／輔助方法（若尚未支援單張圖）；但須實測。
7. **JST 日期轉換封裝**（API 參數叫 `UTCDate`，內部統一轉 JST 並格式化 yyyyMMdd）：Ozakboy 在 quota／insight 的日期參數可採同樣語意，並用 `DateTimeOffset` 避免 Kind 問題。
8. **Obsolete 墊片策略**（拼字錯誤版本保留並導向新名稱）：對公開套件是良好的相容性實踐，只是不該一開始就拼錯。
9. **`LoadingAnimation` 的參數規則集中驗證**（5–60 且為 5 的倍數）：Ozakboy 清單有 loading animation，建議確認是否同樣在本地擋下。

### (c) LineBotSDK 有，但不值得學（理由）

| 項目 | 理由 |
|---|---|
| 字串拼 JSON + `Replace("'", "\"")` | 會毀損含撇號的內容、竄改 `"`、無跳脫（§4 #1–#3）。Ozakboy 用 `JsonSerializer`／`JsonElement` 是正確方向 |
| `WebClient` 每次 new、全同步、`.Result` | 已過時、無 async／取消／逾時／重試；Ozakboy 應維持 `HttpClient` + `IHttpClientFactory` + async |
| 每個靜態方法帶 `ChannelAccessToken` | 難整合 DI／多 channel／token 輪替；Ozakboy 的 Options／DI 方式較佳 |
| `ActionConverter`「逐型別嘗試轉型」 | 因 `ToObject` 不回 null，永遠命中第一個（§3-12）。應依 `type` 欄位 discriminator 分派 |
| 扁平聯集式 `Event`／`Message` | 欄位全為可空、需自行判斷 `type` 字串；Ozakboy 的 `LineWebhookEvent` + Raw 後備更乾淨 |
| `SignatureValidation(ControllerBase, secret)` | 吃掉 body、非固定時間比較；Ozakboy 的 HMAC 驗簽／認證方案設計較好（應確認使用 `CryptographicOperations.FixedTimeEquals`） |
| `LineWebHookControllerBase` 繼承式設計 | 強迫繼承 `ControllerBase`、手動設 token、無驗簽整合、無法用於 Minimal API；Ozakboy 的 `MapLineWebhook` 端點 + 認證方案是更現代的做法 |
| Validate*／LoadingAnimation 吞例外回 `false` | 失去錯誤資訊；Ozakboy 應回傳結果物件（含 LINE 的錯誤細節） |
| 假值字串 `dmy://dummy` + `Replace` 刪欄位 | 脆弱的字串手術（§3-15）；應直接用 `null` 忽略序列化 |
| `Bot.PushMulticast` 人數限制 `< 150`、字串 1800 字等硬編碼 | 與 LINE 現行限制（multicast 500、文字 5000）不符；限制應由 LINE 回應或單一常數管理 |
| 就地修改呼叫端集合、複製貼上重複碼 | 副作用與維護成本 |
| QuickReply 子類別的 `type` 錯誤 | 反面教材：以子類別代表每個 action，卻複製貼上寫錯 `type`（§4 #9）。Ozakboy 應有針對 13 顆限制與各 action 類型的單元測試 |
| LINE Notify、Login V1、LIFF v1 半成品、`UpdateLiffApp` 空殼、`RevokeChannelAccessToken` 空殼 | 已終止服務或未完成，不具參考價值 |
| net45 專屬的 Conversation／QnA Maker | 與 Ozakboy 的關鍵字自動回覆／範本重疊且技術過時 |

---

## 附錄：驗證紀錄與限制

- 讀過全部 108 個 `.cs`（含 `Utility.cs` 1,338 行、`Bot.cs`、`LineWebHookControllerBase.cs`、所有模型）。
- 端點以 `grep -o 'https://...line.me...'` 抽出並與逐檔閱讀交叉核對。HTTP 動詞依實際呼叫的 `LineHttpGet/Post/Put/Delete` 或 `UploadData/UploadValues(..., "PUT"/"DELETE")` 判定。
- 所有「依 LINE 文件」「LINE Notify 已終止」「quick reply type 應為 action」等外部事實屬我個人知識，未在此環境連網核對，已標註「待驗證」之處請主代理另行確認。
- 凡「推論」之處（`Parsing` 補丁無效、`ActionConverter` 永遠回 `MessageAction`、`StreamReader` 關閉 body 導致後續讀取失敗、`new` 隱藏成員的序列化結果）均來自程式碼閱讀，未執行任何程式碼。
