# LineBotExtension 1.18.0 逆向分析報告

> 分析對象：NuGet 套件 `LineBotExtension` 1.18.0（作者 Denots，2018 年，net461）。
> 資料來源：ILSpy 反編譯的 17 個 .cs（約 4,900 行，全數讀完）與 IL 層級清單 `LineBotExtension.decomp.md`。
> 套件沒有 XML doc、沒有公開原始碼，所有「用途」皆由程式碼推斷；推斷處會標明「推測」。
> `AssemblyInfo.cs` 的殘留資訊：`AssemblyCompany = "HP Inc."`、`Copyright © HP Inc. 2017`、`AssemblyVersion 1.0.0.0`（NuGet 版本 1.18.0 與組件版本不一致）、無 `InternalsVisibleTo`、無強名稱。

---

## 1. 套件概觀

### 1.1 TFM 與相依

| 項目 | 內容 |
|---|---|
| TFM | net461（`System.Web`、`System.Web.Http`、`System.Drawing`、`System.Runtime.Caching` 皆為 .NET Framework 專屬組件） |
| IL 組件參考 | mscorlib、Quartz、Newtonsoft.Json、System.Net.Http、System.Drawing、System.Web、System、System.Core、System.Runtime.Caching、System.Web.Http |
| NuGet 相依 | Newtonsoft.Json 10、Microsoft.AspNet.WebApi.Core/Client 5.2.3、Quartz 2.6.1、Common.Logging 3.3.1 |
| Common.Logging | 原始碼完全沒有任何 logging 呼叫（grep `logging|ILog|Trace|Debug|Console` 零命中）；它是 Quartz 2.x 的傳遞相依，不是本套件自己使用 |
| WebApi 的實際用途 | 只有一處：`RequestHelper.GetHttpResponse(this HttpRequestMessage, string)` 用 `HttpRequestMessageExtensions.CreateResponse<string>` 建回應。Webhook 解析本身吃的是 MVC/System.Web 的 `HttpRequestBase` |

### 1.2 命名空間與型別數

所有型別都在 `LineBotExtension.*` 之下。**IL 清單中那 173 個「全域命名空間」型別，實際上是巢狀型別**（絕大多數巢在 `LineBotExtension.Helper.ComponentHelper` 裡），清單工具只是沒印出外層名稱，並不存在「全域命名空間汙染」。真正的問題是「巢狀型別暴增、單一檔案 2,552 行的上帝類別」。

| 命名空間 | 頂層型別 | 說明 |
|---|---|---|
| `LineBotExtension.Model` | 5：`ReceiveMessageModel`、`RichMenuDataModel`、`RichMenuListModel`、`UserDataModel`、`GroupRoomDataModel` | 純 POCO，Newtonsoft 反序列化用。巢狀共 11（ReceiveMessageModel）／5（RichMenuDataModel） 個宣告 |
| `LineBotExtension.Cache` | 3：`ICache`（介面）、`DefaultCache`（internal）、`Keys`（列舉） | 快取抽象與預設 MemoryCache 實作 |
| `LineBotExtension.Scheduler` | 1：`Schedule`（internal）＋私有巢狀 `BaseDataUpdateJob` | Quartz 排程 |
| `LineBotExtension.Helper` | 7：`BotHelper`、`CacheHelper`、`ComponentHelper`、`ExamineHelper`（internal）、`MessageHelper`、`RequestHelper`、`UtilHelper` | 全部邏輯；`ComponentHelper` 一個檔案含 130 個宣告（88 個巢狀 class、35 個 enum、6 個 interface，加上自身） |

宣告總數（以原始碼 regex 計）約 189；IL 清單列 249 型別，差額為編譯器產生的 closure、async 狀態機、`<>c` 等。

### 1.3 整體架構風格

- **Helper 靜態類為主＋抽象類別擴充點**。對外入口是靜態方法（`RequestHelper.Recieved`、`MessageHelper.Push`、`CacheHelper.*`、`UtilHelper.*`）；使用者唯一需要「繼承」的是 `BotHelper.IBot`（抽象類別，10 個非同步回呼：訊息、4 種 postback、follow/unfollow/join/leave、beacon，外加選擇性覆寫的 `Generate()`、`GetIdentity()`、`GetRichMenuMap()`）。
- **「具名工廠 + 內部建構子 + 建構即驗證」**：所有 LINE 訊息/元件模型都是 `public abstract class X` 內放 `public class Y : X`，`Y` 的建構子是 `internal`，只能透過 `X._Foo(...)`（底線開頭的靜態工廠）建立，工廠最後呼叫 `.Examine()` 做屬性驗證。不是流暢 API（無 `.WithXxx()` 串接），而是長參數清單＋預設值。
- **非同步**：HTTP 全部 `async Task<string>`（`HttpClient`）；但沒有 `CancellationToken`、沒有 `ConfigureAwait(false)`。快取層、Rich Menu 等本機邏輯全同步。Quartz 作業是 `async void`。
- **錯誤處理**：兩種風格混用。(1) 本地驗證失敗直接 `throw new Exception(多行中文訊息)`；(2) Webhook 流程把例外轉成「手拼的偽 JSON 字串」`{ ErrorMessage: ..., StackTrace: ... }` 放進回傳 `List<string>`；LINE API 回應則完全不檢查 HTTP 狀態碼，只回傳原始字串，成功與否靠呼叫端比對 `"{}"`。共 10 個 `catch`，其中數個是吞例外（見第 4 節）。
- **JSON 序列化**：Newtonsoft.Json。送出時統一走 `ComponentHelper.Serialize(this IShellModel)`：`NullValueHandling.Ignore` ＋ `StringEnumConverter`，不縮排。接收時直接 `JsonConvert.DeserializeObject<ReceiveMessageModel>`；Postback payload 用 `JObject.Parse` 再 `ToObject<T>` 做多型分派。
- **Quartz 與 Cache 的角色**：Cache 是「Bot 開發者的資料暫存層」（見 2.6），不是 HTTP 回應快取；Quartz 唯一用途是定期重建 `Keys.BaseData`（開發者提供的基礎資料表）。**沒有「排程推播」功能**，Quartz 不會自己發訊息。
- **全域靜態狀態**：`BotHelper.CurrentBot`（`Dictionary<string, BotData>`，以 sourceId 為鍵）、`BotDataIdMap`、`CacheHelper.Cache/DefaultCache/BaseDataGenerater`、`RequestHelper.ImageBaseRoute/LocalTestUrl`、`Schedule.scheduler` 都是 static 可變狀態，無任何 `lock`。

---

## 2. 元件清單（依功能分類）

### 2.1 Messaging API 送訊息

#### 2.1.1 送訊的資料模型（`ComponentHelper.Send`，internal 抽象類別，皆實作 `IShellModel`）

| 型別 | 屬性 | 本地驗證 |
|---|---|---|
| `Send.ReplySend` | `replyToken`、`messages` | 兩者必填；`messages` 數量 0–5 |
| `Send.PushSend` | `to`、`messages` | 必填；**不檢查數量** |
| `Send.MulticastSend` | `to`（`List<string>`）、`messages` | 必填；**不檢查數量與收件人上限** |
| 工廠 | `Send._Reply(replyToken, messages)`、`Send._Push(to, messages)`、`Send._Multicast(to, messages)` | 皆呼叫 `.Examine()` |

#### 2.1.2 API 呼叫方法（一行一個；基底 URL 皆為 `https://api.line.me/v2/bot/`）

- `MessageHelper.Reply(sourceId, replyToken, Message | List<Message>)`（private 實例方法，由 `ReceivedMsg` 內部呼叫）→ `POST https://api.line.me/v2/bot/message/reply`
- `MessageHelper.Push(sourceId, userId, Message | List<Message>)`（public static）→ `POST https://api.line.me/v2/bot/message/push`
- `MessageHelper.Multicast(sourceId, List<string> userIdList, Message | List<Message>)`（public static）→ `POST https://api.line.me/v2/bot/message/multicast`
- `RequestHelper.SendRequest(sourceId, MethodType, position, HttpContent)` → 通用：由 `BotHelper.CurrentBot[sourceId].AccessToken` 取 token，對 `https://api.line.me/v2/bot/{position}` 發送
- `RequestHelper.SendRequest(MethodType, host, position, parameter, HttpContent)` → 通用：自訂 host、不帶 token
- `RequestHelper.SendSetting(MethodType, accessToken, position, HttpContent)` → 通用：直接傳入 token（Rich Menu 初始化用）
- 另有 `Push/Multicast` 的「超過 5 則自動拆批遞迴」邏輯，但實作有 bug（見第 4 節 D3）

> 注意：`Push`／`Multicast` 的第一個參數 `sourceId` 不是收件者，而是「用來查 `CurrentBot` 取得 AccessToken 的鍵」，因此只能對「曾經透過 webhook 進來過的 sourceId」所屬的 Bot 發送。

### 2.2 訊息型別模型與 Flex Message 元件模型

#### 2.2.1 訊息（`ComponentHelper.Message`，public abstract，共通屬性 `type`、`quickReply`）

`Message.Type` 列舉：`unknown, text, image, video, audio, location, sticker, imagemap, template, flex`

| 型別 | 屬性 | 工廠（皆回傳驗證過的物件） | 本地驗證 |
|---|---|---|---|
| `TextMessage` | `text` | `Message._Text(text, quickReply)` | text 1–2000 |
| `UrlMessage`（抽象基底） | `originalContentUrl` | — | 必填、1–1000、https |
| `ImageMessage` | `previewImageUrl` | `_Image(DefaultUrlBase original, DefaultUrlBase preview, quickReply)` | 同上，兩個 URL 皆需 https |
| `VideoMessage` | `previewImageUrl` | `_Video(original, preview, quickReply)` | 同上 |
| `AudioMessage` | `duration`（int） | `_Audio(original, int duration, quickReply)` | duration 僅必填 |
| `LocationMessage` | `title, address, latitude, longitude` | `_Location(title, address, lat, lng, quickReply)` | title/address 1–100 |
| `StickerMessage` | `packageId, stickerId` | `_Sticker(packageId, stickerId, quickReply)` | 必填 |
| `ImagemapMessage` | `baseUrl, altText, baseSize, actions` | `_Imagemap(ImageMapBase, ImageMapBaseSize, List<ImageMapButton>, altText, quickReply)` | baseUrl https；altText 1–400；actions 0–50 |
| `TemplateMessage` | `altText, template` | `_Template(Template, altText, quickReply)` | altText 1–400 |
| `FlexMessage` | `altText, contents`（`Container`） | `_Flex(Container, altText, quickReply)` | altText 1–400 |

#### 2.2.2 Template（`ComponentHelper.Template`）

`Template.Type`：`buttons, confirm, carousel, image_carousel`

| 型別 | 屬性 | 工廠 | 驗證要點 |
|---|---|---|---|
| `ButtonsTemplate` | `title, text, defaultAction, actions, thumbnailImageUrl, imageAspectRatio, imageSize, imageBackgroundColor` | `Template._Buttons(title, text, List<DefaultButton> actions, SettingThumbnailImage, defaultAction)` | title 1–40；actions 1–4；text 長度依「是否有 title/圖」切換 0–60 或 0–160（自訂驗證委派） |
| `ConfirmTemplate` | `text, actions` | `_Confirm(text, actions)` | text 1–240；actions 剛好 2 |
| `CarouselTemplate` | `columns, imageAspectRatio, imageSize` | `_Carousel(List<CarouselColumn>, DefaultThumbnailImageSetting)` | columns 1–10；自訂檢查：所有 column 的「title 有無」「圖有無」「actions 數量」必須一致（LINE 的限制），錯誤訊息「請統一:…」 |
| `ImageCarouselTemplate` | `columns` | `_ImageCarousel(List<ImageCarouselColumn>)` | columns 1–10 |
| `Column.CarouselColumn` | `title, text, defaultAction, actions, thumbnailImageUrl, imageBackgroundColor` | `Column._Carousel(title, text, actions, DefaultThumbnailImage, defaultAction)` | title 1–40；text 依有無 title/圖為 0–60 或 0–120；actions 1–3 |
| `Column.ImageCarouselColumn` | `imageUrl, action`（`ImageButton`） | `Column._ImageCarousel(DefaultUrlBase, ImageButton)` | imageUrl 1–1000 |

輔助模型：

| 型別 | 屬性／列舉 | 工廠 |
|---|---|---|
| `ThumbnailImage.DefaultThumbnailImage` | `url, imageBackgroundColor` | `ThumbnailImage._Default(DefaultUrlBase, Color?)` |
| `ThumbnailImage.SettingThumbnailImage` | `url, thumbnailImageSetting, imageBackgroundColor` | `ThumbnailImage._Setting(DefaultUrlBase, DefaultThumbnailImageSetting, Color?)` |
| `ThumbnailImageSetting.DefaultThumbnailImageSetting` | `imageAspectRatio?`（`rectangle, square`）、`imageSize?`（`cover, contain`） | `ThumbnailImageSetting._Default(ratio?, size?)` |
| `Base.DefaultUrlBase` | `baseUrl` | `Base._DefaultUrl(url, type = null, version = null)`：組成 `url[_version][.type]` |
| `Base.ImageMapBase` | `fileName` | `Base._ImageMap(fileName, version = null)`；`GetBaseUrl()` = `{目前站台}/{ImageBaseRoute}/{fileName}` |
| `BaseSize.ImageMapBaseSize` | `width, height` | `BaseSize._ImageMap(height)`：width 固定 1040 |
| `BaseSize.RichMenuBaseSize` | `width, height`；列舉 `Height { high = 1686, low = 843 }` | `BaseSize._RichMenu(Height)`：width 固定 2500 |
| `Area.DefaultArea` | `x, y, width, height` | `Area._Default(x, y, w, h)` |

#### 2.2.3 Flex Message 元件模型（本套件最大區塊，以下為完整清單）

**架構**：`Flex Message → Container(bubble/carousel) → BoxComponent(header/body/footer) → 子元件`。全部以 `ComponentHelper` 的巢狀型別定義。

**(1) 容器**

| 型別 | 繼承 | 屬性（型別） | 工廠 | 驗證 |
|---|---|---|---|---|
| `Container`（抽象） | — | `type`：`Container.Type { bubble, carousel }` | — | 必填 |
| `Container.BubbleContainer` | Container | `direction`（`BubbleContainer.Direction { ltr, rtl }`）、`header`（`BoxComponent`）、`hero`（`ImageComponent`）、`body`（`BoxComponent`）、`footer`（`BoxComponent`）、`styles`（`BubbleStyleContainer`） | `Container._Bubble(header, hero, body, footer, styles, direction = ltr)` | direction 為列舉值 |
| `Container.CarouselContainer` | Container | `contents`（`List<BubbleContainer>`） | `Container._Carousel(List<BubbleContainer>)` | 必填、1–10 |

**(2) 元件基底 `Component`（抽象）**

| 成員 | 型別 | 說明 |
|---|---|---|
| `type` | `Component.Type { box, button, filler, icon, image, separator, spacer, text }` | 必填 |
| `flex` | `int?` | `Range(0, int.MaxValue)` |
| `margin` | `Component.Spacing?` | `Spacing { none, xs, sm, md, lg, xl, xxl }` |
| 標記介面 | `HorizontalComponent`、`VerticalComponent`、`BaselineComponent` | 無成員；用來在編譯期限制各種 Box 能放哪些子元件（見 3.3） |

**(3) 具體元件**

| 型別 | 實作的標記介面 | 屬性（型別） | 巢狀列舉 | 工廠 |
|---|---|---|---|---|
| `BoxComponent` | Horizontal、Vertical | `layout`、`contents`（`List<Component>`）、`spacing`（`Spacing`，非 nullable，預設 `none`）、`action`（**`ImageButton`**） | `Layout { horizontal, vertical, baseline }` | `Component._HorizontalBox(List<HorizontalComponent>, flex, spacing, margin, ImageButton action, beginningSpacer, endSpacer)`；`_VerticalBox(List<VerticalComponent>, …)`；`_BaseLineBox(List<BaselineComponent>, …)` |
| `ButtonComponent` | Horizontal、Vertical | `action`（`Button.DefaultButton`）、`height`、`style`、`color`、`gravity` | `Height { sm, md }`、`Style { link, primary, secondary }`、`Gravity { top, bottom, center }` | `Component._Button(DefaultButton action, flex, height = md, margin, style = link, Color? color, gravity = top)` |
| `FillerComponent` | Horizontal、Vertical、Baseline | （無額外屬性；工廠寫死 `flex = 1`） | — | `Component._Filler()` |
| `IconComponent` | Baseline | `url`、`size`（string）、`aspectRatio`（string） | `Size { xxs, xs, sm, md, lg, xl, xxl, txl, qxl, pxl }`（txl/qxl/pxl 對應字串 `3xl/4xl/5xl`）、`AspectRatio { one_to_one, two_to_one, three_to_one }`（對應 `1:1/2:1/3:1`） | `Component._Icon(DefaultUrlBase, margin, Size = md, AspectRatio = one_to_one)` |
| `ImageComponent` | Horizontal、Vertical | `url`、`align`、`gravity`、`size`（string）、`aspectRatio`（string）、`aspectMode`、`backgroundColor`、`action`（`ImageButton`） | `Align { start, end, center }`、`Gravity { top, bottom, center }`、`Size { xxs…pxl, full }`（11 值）、`AspectRatio { one_to_one, one_point_five_one_to_one, one_point_nine_one_to_one, four_to_three, sixteen_to_nine, twenty_to_thirteen, two_to_one, three_to_one, three_to_four, nine_to_sixteen, one_to_two, one_to_three }`（對應 `1:1, 1.51:1, 1.91:1, 4:3, 16:9, 20:13, 2:1, 3:1, 3:4, 9:16, 1:2, 1:3`）、`AspectMode { fit, cover }` | `Component._Image(DefaultUrlBase, flex, margin, Align = center, Gravity = top, Size = md, AspectRatio = one_to_one, AspectMode = fit, Color? backgroundColor, ImageButton action)` |
| `SeparatorComponent` | Horizontal、Vertical | `color` | — | `Component._Separator(margin, Color?)` |
| `SpacerComponent` | **無**（不能放進 `List<Horizontal/Vertical/BaselineComponent>`） | `size` | `Size { xs, sm, md, lg, xl, xxl }` | `Component._Spacer(Size = md)`；只能經由 Box 工廠的 `beginningSpacerComponent`／`endSpacerComponent` 參數放入 |
| `TextComponent` | Horizontal、Vertical、Baseline | `text`、`size`（string）、`align`、`gravity`、`wrap`（bool）、`maxLines`（int）、`weight`、`color`、`action`（**`Button.DefaultButton`**，與 Box/Image 的 `ImageButton` 不同） | `Size { xxs…pxl }`（10 值）、`Align { start, end, center }`、`Gravity { top, bottom, center }`、`Weight { regular, bold }` | `Component._Text(text, flex, margin, Size = md, Align = start, Gravity = top, bool wrap = false, int maxLines = 0, Weight = regular, Color? color, DefaultButton action)` |

**(4) 樣式**

| 型別 | 屬性 | 工廠 |
|---|---|---|
| `StyleContainer.BubbleStyleContainer` | `header, hero, body, footer`（皆 `Style.BlockStyle`） | `StyleContainer._Bubble(header, hero, body, footer)` |
| `Style.BlockStyle` | `backgroundColor`（string `#RRGGBB`）、`separator`（bool）、`separatorColor` | `Style._Block(Color? backgroundColor, bool separator = false, Color? separatorColor)` |

**(5) Flex 規格覆蓋度（推測）**：對照 LINE 官方 Flex 規格，本套件**沒有** `span`、`video`、`text.decoration/style/position/offset*`、`box.backgroundColor/borderWidth/borderColor/cornerRadius/padding*/width/height/maxWidth/maxHeight/justifyContent/alignItems`、`bubble.size`、`image.animated`、`icon.offset*`、`text.lineSpacing/adjustMode/scaling` 等較晚期加入的欄位（程式碼只對應 2018 年中的 Flex 規格）。Box 的 `spacing`、Bubble 的 `direction` 因為是非 nullable 列舉，**每次都會被序列化**（即使沒設）。

#### 2.2.4 Quick Reply

| 型別 | 屬性 | 工廠 | 驗證 |
|---|---|---|---|
| `QuickReply.DefaultQuickReply` | `items`（`List<QuickReplyButton>`） | `QuickReply._Default(items)` | 0–13 |
| `QuickReplyButton.ActionQuickReplyButton` | `type`（`QuickReplyButton.Type { action }`）、`imageUrl`、`action`（`Button.QuickReplyButton`） | `QuickReplyButton._Action(Button.QuickReplyButton action, string imageUrl)` | imageUrl 1–1000、https |

### 2.3 動作型別

LINE 的 Action 在本套件被拆成三套互不相容的類別階層（同名不同型別）：

| 階層 | 用在哪裡 | 成員（`Type` 列舉） | 型別與屬性 |
|---|---|---|---|
| `Button`（`Button.Type { message, postback, uri, datetimepicker, camera, cameraRoll, location }`；`label` 1–20） | Template 按鈕、Flex 的 `ButtonComponent`/`TextComponent`、Rich Menu、Quick Reply | `Button.DefaultButton`（介面，含 `label`）與 `Button.QuickReplyButton`（空標記介面）決定能用在哪 | `MessageButton`（`text` 1–300；Default+QuickReply）、`PostbackButton`（`data` 1–300、`text`、`displayText`；Default+QuickReply）、`UriButton`（`uri` 1–1000；**只有 Default**）、`DatetimePickerButton`（`data`、`mode`、`initial`、`max`、`min`；內含 `new enum Type { date, time, datetime }`；Default+QuickReply）、`CameraButton`／`CameraRollButton`／`LocationButton`（**只有 QuickReply**） |
| `ImageButton`（`ImageButton.Type { message, postback, uri, datetimepicker }`；`label` 1–12） | Flex 的 `BoxComponent.action`、`ImageComponent.action`、`ImageCarouselColumn.action` | — | `MessageImageButton`、`PostbackImageButton`、`UriImageButton`、`DatetimePickerImageButton`（欄位同上） |
| `ImageMapButton`（`ImageMapButton.Type { message, uri }`；`label` 1–50；必填 `area`） | `ImagemapMessage.actions` | — | `UriImageMapButton`（`linkUri` 1–1000）、`MessageImageMapButton`（`text` 1–400） |

工廠方法：

- `Button._Message(label, text)`、`_PostBack(label, PayloadValue data, text = null, onlyDisplay = true)`、`_Uri(label, uri)`、`_DatetimePicker(label, PayloadValue data, mode, DateTime initial, List<DateTime> range)`、`_Camera(label)`、`_CameraRoll(label)`、`_Location(label)`
- `ImageButton._Message(text, label)`、`_PostBack(data, text, label, onlyDisplay)`、`_Uri(uri, label)`、`_DatetimePicker(label, data, mode, initial, range)`
- `ImageMapButton._Uri(linkUri, area, label)`、`_Message(text, area, label)`、`_GenerateImageMapButtonArea(list, ix, iy, dx, dy, wl, hl, hc, vc)`（把一串按鈕依「起點、間距、單格寬高、橫向個數、縱向個數」自動鋪成格狀 area）

細節：`_PostBack` 的 `onlyDisplay` 決定 `text` 要放 `displayText`（只顯示）還是 `text`（會送出文字）；`_Uri` 會 `Replace(" ", "")`；DatetimePicker 的日期格式固定 `yyyy-MM-dd`／`HH:mm`／`yyyy-MM-ddTHH:mm`，`initial` 預設 `DateTime.Now`（伺服器時區）。

### 2.4 Webhook 接收與解析

**`ReceiveMessageModel`**（JSON 欄位名即屬性名，全小寫駝峰）

| 層級 | 屬性 |
|---|---|
| `ReceiveMessageModel` | `events`（`List<Event>`） |
| `Event` | `replyToken, type, timestamp(long), source, message, postback, beacon`；`Event.Type { unknown, message, follow, unfollow, join, leave, postback, beacon }` |
| `Event.Source` | `type`（`{ unknown, user, group, room }`）、`userId`、`groupId`、`roomId`；`GetSourceId()` = `roomId ?? groupId ?? userId` |
| `Event.Message` | `id, type, text, fileName, fileSize(string), title, address, latitude, longitude, packageId, stickerId`；`Message.Type { unknown, text, image, video, audio, file, location, sticker }` |
| `Event.PostBack` | `data`；`param`（以 `[JsonProperty("params")]` 對應，型別 `Param { date, time, datetime }`） |
| `Event.Beacon` | `hwid`、`dm`、`type { unknown, enter, leave, banner }` |

**接收管線**（`RequestHelper.Recieved` → `MessageHelper.Received` → 你的 `IBot`）：

1. `RequestHelper.Recieved(string id, HttpRequestBase request)`（注意拼字 `Recieved`）：讀 `InputStream` → 反序列化 → 以 `id` 取 `BotData` → 用 `ChannelSecret` 做 HMAC-SHA256 與 `X-Line-Signature` 比對 → 若通過且尚未 `Generate()` 則呼叫 `Bot.Generate()`（只做一次）→ `new MessageHelper(bot).Received(model)`。回傳 `List<string>`（每個 LINE API 呼叫的原始回應或錯誤字串）。
2. `MessageHelper.ReceivedMsg(Event)`：先檢查 `BotData.IsTestMode` 與 `AdminIdList`（非管理員一律回 `TestMessage`，預設「系統維護中，此帳號無測試權限」）；再依事件型別分派：
   - message → `IBot.GetReceivedMessage(msg, source)`
   - follow／unfollow／join／leave → `GetFollowMessage`／`GetUnfollowMessage`／`GetJoinMessage`／`GetLeaveMessage`（unfollow、leave 不回覆）
   - postback → 解析 `data`：若是 GUID 則從 MemoryCache 取回（見 2.7 / 3.5），否則 `JObject.Parse(data)`；依 payload `type` 呼叫 `GetPostBackKeyMessage`／`GetPostBackSearchMessage`／`GetPostBackCacheMessage`（依 `actiontype` 反序列化為 Add/Set/Delete 三種）／`GetPostBackExpiredMessage`
   - beacon → `GetBeaconMessage(beacon, source)`
3. 回覆：所有 `IBot` 回傳的 `List<Message>` 會自動 `POST message/reply`。
4. 例外：若 sourceId 在 `AdminIdList` 內，會把例外與 StackTrace 以 `GetRequestLog` 切成 ≤2000 字的多則文字訊息回覆給管理員。

其他：`RequestHelper.GetHttpResponse(this HttpRequestMessage, string result)` 把結果字串包成 WebApi 回應；`ReceivedMessageModel`（在 `UtilHelper.Received`）見 2.9 的 UtilHelper.Received。

### 2.5 Rich Menu

| 型別 | 說明 |
|---|---|
| `Model.RichMenuDataModel`（回應模型） | `richMenuId, size(Size{width,height}), selected, name, chatBarText, areas(List<Area>), linkUserIdList`；`Area { bounds(Bound{x,y,width,height}), action(Action{type,data}) }`。`linkUserIdList` 不是 LINE API 欄位，是本套件為記錄「哪些使用者已連結」加的欄位 |
| `Model.RichMenuListModel` | `richmenus`（`List<RichMenuDataModel>`） |
| `ComponentHelper.RichMenu`（抽象，`IShellModel`） | `richMenuId, name, selected` |
| `RichMenu.DefaultRichMenu` | `chatBarText`（1–14）、`size`（`RichMenuBaseSize`，高度只能 1686 或 843）、`areas`（`List<RichMenuButton>`，0–20）；工廠 `RichMenu._Default(chatBarText, size, areas, selected = false)`（`name` 暫填 "Temp"，註冊時被覆蓋成字典鍵） |
| `RichMenuButton.DefaultRichMenuButton` | `bounds`（`Area`）、`action`（`Button.DefaultButton`）；工廠 `RichMenuButton._Default(Area bounds, DefaultButton action)`（**會把傳入 action 的 `label` 設為 null**） |

Rich Menu 的 API 方法：

- `BotHelper.BotData.SetRichMenu()`（internal，啟動時由 `BotHelper.SetBotDataIdMap` 觸發）→ `GET https://api.line.me/v2/bot/richmenu/list`，逐一 `DELETE https://api.line.me/v2/bot/richmenu/{richMenuId}`，再對 `IBot.GetRichMenuMap()` 的每一項：`POST https://api.line.me/v2/bot/richmenu` → `POST https://api.line.me/v2/bot/richmenu/{richMenuId}/content`（image/png）→ `GET https://api.line.me/v2/bot/richmenu/{richMenuId}`；對被選為預設的選單，對所有已快取使用者 `DELETE https://api.line.me/v2/bot/user/{userId}/richmenu` 後 `POST https://api.line.me/v2/bot/user/{userId}/richmenu/{richMenuId}`
- `UtilHelper.RichMenu.GetList(source)` → `GET https://api.line.me/v2/bot/richmenu/list`
- `UtilHelper.RichMenu.GetById(source, richMenuId)` → `GET https://api.line.me/v2/bot/richmenu/{richMenuId}`
- `UtilHelper.RichMenu.GetIdByUser(source)` → `GET https://api.line.me/v2/bot/user/{userId}/richmenu`
- `UtilHelper.RichMenu.LinkFromUserById(source, richMenuId)` → 先 `GetIdByUser` 與 `UnLinkFromUser`，再 `POST https://api.line.me/v2/bot/user/{userId}/richmenu/{richMenuId}`；失敗時嘗試把舊選單連回去並丟出例外
- `UtilHelper.RichMenu.LinkFromUserByKey(source[, key])` → 以 `RichMenuMap` 的鍵（＝圖檔名）找選單 id，再走 `LinkFromUserById`；無 key 版本取 `selected` 或第一個
- `UtilHelper.RichMenu.UnLinkFromUser(source)` → `DELETE https://api.line.me/v2/bot/user/{userId}/richmenu`
- `UtilHelper.RichMenu.GetImage(source, richMenuId)` → `GET https://api.line.me/v2/bot/richmenu/{richMenuId}/content`（回傳 base64 字串，找不到回 null）
- 圖檔來源：`RequestHelper.GetRichMenuBaseData(fileName)`（internal）讀 `{AppDomainAppPath}/Images/RichMenu/{fileName}.png`
- 限制檢查：`RichMenuMap.Count ≤ 1000`、鍵（name）長度 1–300（`BotData.SetRichMenu` 內以 `throw new Exception` 實作）

### 2.6 使用者／群組／聊天室資料模型

| 型別 | 屬性 | 取得方式 |
|---|---|---|
| `UserDataModel` | `displayName, userId, pictureUrl, statusMessage` | `CacheHelper.GetUserData(userId)` → `GET https://api.line.me/v2/bot/profile/{userId}`；`UtilHelper.User.Init(source)` 包裝並建立使用者的 CacheData 空間 |
| `GroupRoomDataModel` | `memberList`（`List<UserDataModel>`） | `CacheHelper.GetGroupRoomData(Source.Type, id)`：`GET https://api.line.me/v2/bot/{group\|room}/{id}/members/ids[?start={next}]`，再對每個成員 `GET https://api.line.me/v2/bot/{group\|room}/{id}/members/{userId}`（N+1 次請求）；私有 `GroupMemberModel { memberIds, next }` 解分頁 |
| 離開群組 | — | `UtilHelper.GroupRoom.Leave(source)` → `POST https://api.line.me/v2/bot/{group\|room}/{id}/leave` |
| 取得訊息內容 | — | `UtilHelper.File.GetContent(source, msg)` → `GET https://api.line.me/v2/bot/message/{messageId}/content`（回傳 base64 字串，「Not found」回 null） |

### 2.7 Cache（`ICache` / `DefaultCache` / `Keys` / `CacheHelper`）

**抽象**：

```
public interface ICache { object GetValue(Keys key); bool Add(Keys key, object value);
                          bool Delete(Keys key); bool Clear(); }
public enum Keys { BaseData, CacheData, UserData, GroupRoomData, RichMenuData }
```

整個快取只有 5 個固定的鍵，每個鍵底下放一整個大 `Dictionary`（整包讀、整包寫）。

**`DefaultCache`（internal）**：包 `System.Runtime.Caching.MemoryCache.Default`。`Add` 用 `ObjectCache.Add(key, value, DateTimeOffset.MaxValue)`，亦即**永不過期**（只會被記憶體壓力驅逐）；`Add(key, null)` 等同 `Delete`；`Clear` 逐項移除（但回傳值是「清完後是否還有項目」，語意反了）。`CacheHelper.SetCache(ICache)` 可替換實作；`GetCache()` = `Cache ?? DefaultCache`。

**五個鍵各快取什麼**：

| `Keys` | 內容型別 | 用途與組鍵 |
|---|---|---|
| `BaseData` | `Dictionary<string, List<object>>`（資料表名 → 列） | 開發者以 `CacheHelper.SetBaseDataGenerater(Func<Task<Dictionary<string, List<object>>>>, UpdateFrequency)` 提供的唯讀「基礎資料」（如商品清單）。第一次取用時懶載入，`UpdateBaseData()` 清空再重建。postback `search` 的資料來源 |
| `CacheData` | `Dictionary<string, Dictionary<string, List<object>>>`（使用者空間 → 資料表名 → 列） | 每個 Bot × 每個 sourceId 的「個人暫存區」（如購物車、表單填寫進度）。**外層鍵**：`{BotData.Id}.{IBot 的型別名}({IBot.GetIdentity() ?? "Default"}).{sourceId}`；操作：`AddCacheData`／`SetCacheData`（取代整表）／`DeleteCacheData(sourceId)`（刪整個空間）／`DeleteCacheData(sourceId, key, filter, param)`（依條件刪列）／`GetCacheData`。寫入時每筆都過 `UtilHelper.Common.CustomReplace(param, data)` 做 `{{...}}` 替換 |
| `UserData` | `Dictionary<string, UserDataModel>`（userId → 個資） | `GetUserData`：**每次都呼叫 profile API**，成功後存入字典；字典只在 API 失敗時當備援，並未用來省請求 |
| `GroupRoomData` | `Dictionary<string, GroupRoomDataModel>` | 同上，每次重抓全部成員；`DeleteGroupRoomData` 回傳舊成員清單供後續清理 |
| `RichMenuData` | `Dictionary<botId, Dictionary<richMenuId, RichMenuDataModel>>` | `SetRichMenu` 成功建立後存入；`AddRichMenuUserLink`／`DeleteRichMenuUserLink` 維護 `linkUserIdList` |

**「過期策略」**：`Keys.*` 這一層**沒有任何過期／TTL／容量上限**，唯一的刷新機制是 Quartz 重建 `BaseData`。另有一條**繞過 `ICache`** 的快取：`PayloadValue.DataEncrypt` 直接寫 `MemoryCache.Default`，key 為 `"PayloadValue-" + Guid`，絕對到期 `DateTimeOffset.Now.AddDays(1)`（唯一有 TTL 的資料）；`CacheHelper.GetData("PayloadValue")` 可列出。

**自訂 ICache 的相容處理**：每次讀取都寫成 `value as Dictionary<...> ?? JObject.FromObject(value).ToObject<Dictionary<...>>()`，也就是容忍外部快取（如 Redis）把物件序列化後回傳 `JObject`/JSON 的情況，之後再 `Add` 回去轉成強型別。

`CacheHelper.GetData(string key)`：以 `Enum.Parse` 取 `Keys` 或特例 `"PayloadValue"`，回傳排序後的字典快照（除錯／管理頁用）；`ClearData()` 清全部。

### 2.8 Scheduler（`Schedule` + Quartz）

```
internal class Schedule {
  private class BaseDataUpdateJob : IJob { public async void Execute(ctx) => await CacheHelper.UpdateBaseData(); }
  private static IScheduler scheduler = new StdSchedulerFactory().GetScheduler();
  public static void RegisterBaseDataUpdateJobs(CacheHelper.UpdateFrequency f) { ... }
}
```

- **排程什麼**：只有一個 Job：`BaseDataUpdateJob`，執行 `CacheHelper.UpdateBaseData()`（清掉 `Keys.BaseData` 再呼叫開發者的 generator 重建）。
- **怎麼觸發**：開發者呼叫 `CacheHelper.SetBaseDataGenerater(generater, UpdateFrequency.ByDaily(hour, minute) | ByMonthly(day, hour, minute))`，內部呼叫 `Schedule.RegisterBaseDataUpdateJobs`：必要時 `scheduler.Start()`；Daily 用 `CronScheduleBuilder.DailyAtHourAndMinute(h, m)`、Monthly 用 `CronScheduleBuilder.MonthlyOnDayAndHourAndMinute(d, h, m)`；JobKey 為 `typeof(T).Name`，Trigger 名為 `...DailyTrigger`／`...MonthlyTrigger`。傳 `null` 則只註冊 generator 不排程。
- **死碼**：`registerMinuteJob<T>(int)`、`registerSecondJob<T>(int)` 已寫好（`SimpleSchedule ... RepeatForever`）但無任何呼叫端。
- 時區：Cron 觸發使用 Quartz 預設（主機本地時區）。

### 2.9 Helper 類

#### `BotHelper`（public static）
- 巢狀 `IBot`（抽象）、`BotData`。
- `BotData`：`Bot, AccessToken, ChannelSecret, AdminIdList, IsTestMode, TestMessage`，internal `Id`、`HasGenerate`；公開建構子 `BotData(IBot, accessToken, channelSecret)`；流暢設定 `AddAdmin(string | List<string>)`、`TestMode(bool, message)`；internal `SetId`、`SetRichMenu`。
- 靜態：`GetBotData(key)`、`GetBotDataIdMap()`、`SetBotDataIdMap(Dictionary<string,BotData>)`／`SetBotDataIdMap(key, botData)`（登錄時順便 `SetId(key).SetRichMenu()`，即**登錄即同步 Rich Menu**）；`internal static CurrentBot`（sourceId → BotData）。

#### `ComponentHelper`（public static，2,552 行）
- 全部 LINE 訊息／Flex／Template／Action／Rich Menu／Quick Reply／Postback payload 模型，以及 `internal static string Serialize(this IShellModel)`。
- Postback payload：`PayloadValue.Type { key, search, cache, expired }`；`KeyPayloadValue(key, param)`、`SearchPayloadValue(key, tableList, filter, range, param)`、`CachePayloadValue`＋`AddCachePayloadValue/SetCachePayloadValue(dataList)`／`DeleteCachePayloadValue(filter)`（`ActionType { add, set, delete }`）、`ExpiredPayloadValue`；`CompareModel { type: contain|equal, value: List<string> }`；`PayloadParam`（含 `DatetimePickerPayloadParam.data` DateTime，`Type { unknown, date, time, datetime }`）。工廠：`PayloadValue._Key/_Search/_AddCache/_SetCache/_DeleteCache/_Expired`。

#### `ExamineHelper`（internal static）
- 見 3.2。屬性型驗證：`Required`、`Range(min,max)`、`Contains(params string[] | Type enum, byValue)`、`Length(min,max)`、`Count(min,max)`、`Url(needHttps)`、`Color`；`Examine<T>(this T model, customExamineMap)`；多個 `get*ExceptionContent` 產生中文錯誤訊息（例如「`{型別全名}`的欄位`{屬性}`的長度`{n}`超出限制範圍[`{min}-{max}`]」）。

#### `MessageHelper`（public class，建構子 internal）
- 見 2.4／2.1。`Received`、`ReceivedMsg`、`GetMsgContent`×4（依 message/事件型別/postback/beacon 多載）、`GetRequestLog`、`Reply`×2、`Push`×2、`Multicast`×2。

#### `RequestHelper`（public static）
- `Recieved(id, HttpRequestBase)`（Webhook 入口＋驗簽）。
- `SendRequest`×3／`SendSetting`：以**每次新建 `HttpClient`**（`using`）發送；固定標頭 `user-agent: linebot`、`Accept: application/json`、`Authorization: Bearer {token}`；POST 無內容時送 `{}`；回應處理：若 `Content-Type` 有 charset 則以該編碼轉字串，**否則回傳 base64**（為了圖片/二進位內容）。
- `GetImageMapBaseData(fileName, size, Func<Bitmap,Bitmap> func)`：讀 `Images/ImageMap/{fileName}.png`，等比縮放到 `size` 寬後快取於 `Images/ImageMap/Mimage/{fileName}_{size}.png`，可套 `func` 後處理（如動態疊字），輸出 `ImageBaseData { Stream, ContentType, FileName }`（給開發者的 Controller 回傳；LINE 對 imagemap 的 baseUrl 會附加 `/1040`、`/700` 等尺寸）。
- `GetRichMenuBaseData(fileName)`（internal）。
- `GetImageBaseRoute()/SetImageBaseRoute(path)`（預設 `Webhook/ImageBase`）、`GetCurrentUrl()`（`HttpContext.Current` 的 scheme+host，**不含 port**；host 為 localhost 且設了 `SetLocalTestUrl` 時用覆寫值）、`GetCurrentRootPath()`（`HttpRuntime.AppDomainAppPath`）。
- `GetHttpResponse(this HttpRequestMessage, string)`（WebApi 回應包裝）。

#### `UtilHelper`（public class；全為巢狀工具）
- `Common.CustomReplace<T>(object map, T input)`（internal）：`{{a.b.0.c}}` 範本替換（見 3.8）。
- `Received.ReceivedMessageModel`：文字指令路由器。`GenerateReceivedMessageModel(List<CommandMap>, List<FuzzyMap>, multiResult)`；`CommandMap{Src, Drct}` 精確比對；`FuzzyMap{Src, Drct, Rgx}`：正則的**數字命名群組**（`(?<1>...)`）擷取為參數，或以 `Src` 當前綴、其餘文字當參數；`GetReceivedMessageResultList(text)` 回傳 `Result{Type, ParamList}`，預設只取第一個。
- `PostbackSearch`：`DataSearch`（套 filter）、`GetRange`（依 `range=[起,訖,(上限)]` 切片並回傳 `RangeType { Unknown, Over, First, Middle, Last }`）、`GetSeeMorePostBackButton(...)`（產生「上一頁／下一頁」postback 按鈕，`SeeMoreTemplateType { Confirm, Button }`，可加第三顆自訂按鈕）。
- `PostbackCache.DataCache(payload, param, source)`：依 `actiontype` 分派到 `CacheHelper.Add/Set/DeleteCacheData`。
- `User.Init/Delete`、`GroupRoom.Init/Delete/Leave`、`File.GetContent`、`RichMenu.*`：見 2.5、2.6。

---

## 3. 值得注意的實作細節與技巧

### 3.1 「具名工廠 + internal 建構子 + 建構即驗證」（非流暢 API）
檔案：`Helper/ComponentHelper.cs`。所有模型的建構子是 `internal`，外部只能呼叫底線開頭的靜態工廠；工廠以物件初始化器填值後立刻 `.Examine()`，驗證失敗當場丟例外，因此拿到手的物件一定通過本地規則。
```csharp
public static TextMessage _Text(string text, QuickReply quickReply = null)
{
    return new TextMessage { type = Type.text, text = text, quickReply = quickReply }.Examine();
}
// TextMessage 的建構子：internal TextMessage() { }
```
Flex 元件也是同一套（`Component._Text(...)` 有 11 個參數、`_Image(...)` 有 10 個，皆有預設值），沒有 `.WithXxx()` 串接。例外：`ButtonComponent`、`FillerComponent`、`IconComponent`、`ImageComponent`、`SeparatorComponent`、`SpacerComponent`、`TextComponent` 在反編譯碼中沒有宣告 internal 建構子，等於有隱含的 public 預設建構子，可繞過驗證（與其他型別不一致）。

### 3.2 `ExamineHelper`：以 Attribute 宣告 LINE 限制，反射執行 —— 有本地驗證 LINE 上限
檔案：`Helper/ExamineHelper.cs`。七個 internal Attribute：`Required`、`Range`、`Contains`、`Length`、`Count`、`Url(needHttps)`、`Color`。`Examine<T>(this T model, Dictionary<string, Func<T,string,string>> customExamineMap)` 對 `typeof(T).GetProperties()` 逐一檢查，**收集所有錯誤後一次丟出**（中文多行訊息），並可針對特定屬性掛自訂驗證委派。
```csharp
case "Length": {
    string text3 = (string)value; Length length = (Length)obj;
    if (text3.Length < length.Min || text3.Length > length.Max)
        list.Add(getLengthExceptionContent(typeof(T).FullName, propertyInfo.Name, text3.Length, length.Min, length.Max));
    break; }
```
本地已實作的 LINE 上限（節錄）：

| 項目 | 本地規則 |
|---|---|
| Reply `messages` | 0–5 |
| Text message `text` | 1–2000 |
| image/video/audio URL | 1–1000，必須 https |
| location `title`/`address` | 各 1–100 |
| imagemap `altText`／`actions` | 1–400／0–50 |
| template／flex `altText` | 1–400 |
| Buttons template | title 1–40、actions 1–4、text 依有無 title/圖為 ≤60 或 ≤160 |
| Confirm template | text 1–240、actions 恰 2 |
| Carousel／ImageCarousel columns | 1–10；column title 1–40、text ≤60/≤120、actions 1–3 |
| Flex carousel `contents` | 1–10 |
| Button／ImageButton／ImageMapButton `label` | 1–20／1–12／1–50 |
| `text`／`data`／`displayText` | 1–300 |
| uri | 1–1000 |
| Rich Menu `chatBarText`／`areas` | 1–14／0–20；高度只能 1686 或 843 |
| Quick Reply `items` | 0–13 |
| 色碼 | `^#(?:[0-9a-fA-F]{3}){1,2}$` |
| Rich Menu 數量／名稱 | 在 `SetRichMenu` 內檢查 ≤1000、名稱長度 1–300 |

沒檢查的：Push/Multicast 的 messages 上限與 multicast 收件人上限（500）、Flex 內部各欄位的細部限制（例如 Flex 整體 JSON 大小）、audio `duration` 範圍、postback `data` 以外的 JSON 大小。

### 3.3 以標記介面在編譯期限制 Flex 子元件
檔案：`Helper/ComponentHelper.cs`。三個空介面 `HorizontalComponent`／`VerticalComponent`／`BaselineComponent` 標在元件類別上，Box 工廠的 `contents` 參數型別即為對應介面的 List，放錯元件編譯不過。
```csharp
public class TextComponent : Component, HorizontalComponent, VerticalComponent, BaselineComponent, ...
public class IconComponent : Component, BaselineComponent, ...          // icon 只能放 baseline
public static BoxComponent _BaseLineBox(List<BaselineComponent> contents, int? flex = null, ...)
```
副作用：`SpacerComponent` 沒有實作任何標記介面，只能經由 Box 工廠的 `beginningSpacerComponent`／`endSpacerComponent` 參數頭尾插入，無法放在中間；巢狀 `BoxComponent` 實作 Horizontal／Vertical，因此 baseline 內放 box 不被允許（與 LINE 規格一致）。

### 3.4 以字典把列舉值對應成 LINE 的字串（`3xl`、`1.91:1`）
檔案：`Helper/ComponentHelper.cs`（`_Icon`、`_Image`、`_Text`）。C# 列舉無法含 `3xl`、`1.91:1`，所以用 `txl/qxl/pxl`、`one_point_nine_one_to_one` 命名，再於工廠內以 `Dictionary<Enum,string>` 轉成字串屬性（屬性型別因此是 `string`，列舉只是輸入端）。
```csharp
{ ImageComponent.AspectRatio.one_point_nine_one_to_one, "1.91:1" },
{ ImageComponent.Size.txl, "3xl" },
...
size = dictionary[size], aspectRatio = dictionary2[aspectRatio],
```
每次呼叫都重新 new 字典（無快取），且 `_Icon`／`_Image`／`_Text` 各複製一份，是重複碼（可用 `EnumMember` 或轉換器取代）。

### 3.5 Postback `data` 超過 300 字自動轉存 MemoryCache，以 GUID 取回
檔案：`Helper/ComponentHelper.cs`（`PayloadValue.DataEncrypt`/`DataDecrypt`）。LINE postback data 上限 300 字，本套件把 payload 序列化成 JSON，過長就暫存並只送 GUID；取回失敗回傳 `Expired` payload，由 `IBot.GetPostBackExpiredMessage` 處理。
```csharp
string text = data.Serialize();
if (text.Length < 300) return text;
Guid guid = Guid.NewGuid();
MemoryCache.Default.Add("PayloadValue-" + guid, data, DateTimeOffset.Now.AddDays(1.0), null);
return guid.ToString();
```
`MessageHelper.GetMsgContent(PostBack)` 先 `Guid.TryParse(msg.data, ...)`：是 GUID 就查快取，否則 `JObject.Parse`。名稱 `DataEncrypt` 其實不是加密。

### 3.6 型別化 Postback 協定＋分頁按鈕產生器
檔案：`Helper/ComponentHelper.cs`、`Helper/UtilHelper.cs`、`Helper/MessageHelper.cs`。四種 payload：`key`（一般鍵值＋param）、`search`（對 BaseData 表搜尋，含 `filter`、`range`）、`cache`（add/set/delete 個人暫存）、`expired`。`PostbackSearch.GetSeeMorePostBackButton` 依目前 `range=[start,end,limit]` 算出上一頁／下一頁 payload 並依位置（`First/Middle/Last/Over`）決定顯示哪些按鈕。
```csharp
int num  = payload.range[1] - payload.range[0];           // 頁寬 - 1
int num2 = payload.range[0] - (num + 1);                  // 上一頁起點
int num3 = payload.range[0] + (num + 1);                  // 下一頁起點
// ... ComponentHelper.PayloadValue._Search(payload.key, payload.tableList, new int[3]{ num3, num3 + num, num4 }, ...)
```
`DataFilter` 支援 `equal`／`contain` 兩種比較，鍵路徑可為屬性名或陣列索引，比較值可含 `{{...}}`。

### 3.7 `MessageHelper` 怎麼組訊息：IBot 回傳 `List<Message>`，框架負責包 Reply
檔案：`Helper/MessageHelper.cs`。開發者只需實作 `IBot.GetReceivedMessage(...)` 等回呼並回傳 `List<ComponentHelper.Message>`；框架把它們包進 `Send._Reply(replyToken, msgList)`（觸發 `Count(0,5)` 驗證）→ `Serialize()` → `StringContent(..., "application/json")` → `POST message/reply`。空清單（如 unfollow）不送請求。
```csharp
string content = ComponentHelper.Send._Reply(replyToken, msgList).Serialize();
result.Add(await RequestHelper.SendRequest(sourceId, RequestHelper.MethodType.Post, "message/reply",
           new StringContent(content, Encoding.UTF8, "application/json")));
```
Push／Multicast 則是 public static，開發者自己組 `List<Message>`；超過 5 則的拆批邏輯有誤（見 D3）。

### 3.8 `{{path}}` 範本替換：把 postback 參數（例如 DatetimePicker 選到的日期）注入快取資料
檔案：`Helper/UtilHelper.cs`（`Common.CustomReplace<T>`）。先把物件序列化成 JSON，用 regex `{{(.+?)}}` 找佔位符，以 `.` 分段在 `map`（`JToken.FromObject`）上逐層取值（物件取屬性、陣列取索引），整串替換後再反序列化回 `T`；找不到鍵或取到非葉值就把佔位符改寫成 `path(KeyNotFound)`／`path(TypeError)`。
```csharp
Regex regex = new Regex("{{(.+?)}}(?![^{]+?}}.*?)");
string text = JsonConvert.SerializeObject(input);
foreach (Match item in regex.Matches(text)) { /* 逐段下鑽 map */
    text = text.Replace(item.Groups[0].Value, val.ToString()); }
return JsonConvert.DeserializeObject<T>(text);
```
用途：`PostbackCache.DataCache` 寫入快取前、`DataFilter` 比較前，讓 payload 裡能寫 `{{data}}` 之類佔位符，由使用者實際選到的日期時間填入。

### 3.9 使用者資料快取策略：只寫不讀、整包字典、鍵以 Bot+型別+身分隔離
檔案：`Helper/CacheHelper.cs`（程式碼片段為簡化示意）。
- `GetUserData(userId)` 每次都打 `profile/{userId}`，成功後寫入 `Keys.UserData` 整包字典；快取不會短路 API 呼叫。
- `Keys.CacheData` 的外層鍵：`BotData.Id + "." + Bot.GetType().Name + "(" + (Bot.GetIdentity() ?? "Default") + ")." + sourceId`，所以同一個 userId 在不同 Bot（或同一 Bot 的不同 `GetIdentity()`）下資料互不干擾；Rich Menu 初始化時也用 `StartsWith(prefix)` 加 `Split('.').Last()` 反推出已知使用者清單。
- 整個字典每次「取出→改→`cache.Add`」；`DefaultCache.Add` 是「不存在才加」，所以靠的是**字典是同一個參考、原地修改**；換成外部快取（回傳 JObject）時才會真的重新寫入。
```csharp
Dictionary<string, UserDataModel> userData = (cacheObject as Dictionary<string, UserDataModel>)
    ?? JToken.FromObject(cacheObject).ToObject<Dictionary<string, UserDataModel>>();
...
userData[userId] = userDataModel;  cache.Add(Keys.UserData, userData);
```

### 3.10 Quartz 排程：只為了「定時重建基礎資料」，不是推播
檔案：`Scheduler/Schedule.cs`、`Helper/CacheHelper.cs`。`CacheHelper.SetBaseDataGenerater(generater, UpdateFrequency.ByDaily(3, 0))` 一次完成「登記資料產生器」與「註冊每日 03:00 重建」；每月版本用 `MonthlyOnDayAndHourAndMinute`。Job 內容是清空 `BaseData` 再呼叫 generater。
```csharp
case CacheHelper.UpdateFrequency.Type.Daily:
    registerDailyJob<BaseDataUpdateJob>(f.Hour, f.Minute); break;
...
TriggerBuilder.Create().WithIdentity(typeof(T).Name + "DailyTrigger")
    .WithSchedule(CronScheduleBuilder.DailyAtHourAndMinute(hour, minute)).Build();
```
**本套件沒有任何排程推播功能**：要在特定時間推播，必須自己用 `MessageHelper.Push` 搭配自己的排程器。

### 3.11 啟動時把 Rich Menu「全刪重建」並自動連結給既有使用者
檔案：`Helper/BotHelper.cs`（`BotData.SetRichMenu`）。流程：列出現有 Rich Menu 全部刪除 → 呼叫 `IBot.GetRichMenuMap()`（`Dictionary<圖檔名, RichMenu>`）→ 逐一建立、以 `Images/RichMenu/{key}.png` 上傳圖 → 取回完整物件存入 `RichMenuData` → 對被標為 `selected`（或第一個）的選單，把所有已知使用者重新 link。
```csharp
foreach (RichMenuDataModel richMenu in richMenuListModel.richmenus) {
    await RequestHelper.SendSetting(MethodType.Delete, AccessToken, "richmenu/" + richMenu.richMenuId);
    CacheHelper.DeleteRichMenuData(Id, richMenu.richMenuId); }
```
把 Rich Menu 當成「程式碼＋圖檔即設定」的宣告式部署，缺點是具破壞性（見 D12）。

### 3.12 Imagemap 圖片動態縮放與站台網址推導
檔案：`Helper/RequestHelper.cs`。Imagemap 的 `baseUrl` 由 `GetCurrentUrl() + "/" + GetImageBaseRoute() + "/" + fileName` 組成（取目前請求的 scheme 與 host）；開發者在該路由實作 Controller 並呼叫 `GetImageMapBaseData(fileName, size, func)`，由套件把原圖縮到 LINE 要求的寬度並落地快取到 `Mimage/` 資料夾，還能傳入 `Func<Bitmap,Bitmap>` 在輸出前再加工。`SetLocalTestUrl` 讓 localhost 開發時能換成 ngrok 之類的公開網址。
```csharp
Bitmap val3 = new Bitmap(val2, new Size(size, val2.Height * size / val2.Width));
val3.Save(Path.Combine(text2, fileName + "_" + size + ".png"), ImageFormat.Png);
```

### 3.13 Webhook 驗簽與「一個端點多個 Bot」
檔案：`Helper/RequestHelper.cs`（程式碼片段為簡化示意）。路由參數 `id` 對應 `BotDataIdMap`（可在同一個站台掛多個 LINE channel，各有各的 `AccessToken`／`ChannelSecret`／`IBot` 實作）；以 `HMACSHA256(channelSecret, body)` 的 base64 與 `X-Line-Signature` 比較。
```csharp
byte[] key = Encoding.UTF8.GetBytes(BotHelper.CurrentBot[sourceId].ChannelSecret ?? "");
string sig = Convert.ToBase64String(new HMACSHA256(key).ComputeHash(Encoding.UTF8.GetBytes(text ?? "")));
if (sig == request.Headers["X-Line-Signature"]) { /* 處理事件 */ }
```
之後所有 API 呼叫都以 `CurrentBot[sourceId]` 取 token，這就是為什麼 `Push/Multicast` 需要 `sourceId` 參數。

### 3.14 測試模式（維護模式）與管理員除錯訊息
檔案：`Helper/MessageHelper.cs`、`Helper/BotHelper.cs`（下方程式碼為簡化示意，原碼每處都寫 `BotHelper.CurrentBot[_event.source.GetSourceId()]`）。`BotData.TestMode(true, "維護中")`＋`AddAdmin(userId)`：非管理員收到固定訊息，管理員正常使用；管理員在處理例外時還會在 LINE 直接收到錯誤與 StackTrace。
```csharp
if (CurrentBot[id].IsTestMode && !CurrentBot[id].AdminIdList.Contains(id))
    return await Reply(id, replyToken, Message._Text(CurrentBot[id].TestMessage ?? "系統維護中，此帳號無測試權限"));
```

### 3.15 長錯誤訊息切成多則文字（每則 ≤2000 字）
檔案：`Helper/MessageHelper.cs`（`GetRequestLog`）。遞迴切割，第一則帶標頭 `類型(sourceId):`。
```csharp
if (content.Length > 2000) { text = content.Substring(2000); content = content.Substring(0, 2000); }
if (requestType != null) list.Add(Message._Text(requestType + "(" + sourceId + "):"));
list.Add(Message._Text(content));
if (text.Length > 0) list.AddRange(GetRequestLog(null, sourceId, replyToken, text));
```
但回覆受 5 則上限，過長的堆疊仍會被 `ReplySend` 驗證擋下。

### 3.16 以 Content-Type 是否有 charset 區分文字與二進位回應
檔案：`Helper/RequestHelper.cs`（`SendRequest`）。有 charset（JSON）→ 依編碼轉字串；無 charset（圖片／內容下載）→ 回傳 base64 字串。這讓所有 API 共用 `Task<string>` 回傳型別，`UtilHelper.File.GetContent`、`RichMenu.GetImage` 因而拿到 base64。
```csharp
return (response.Content.Headers.ContentType.CharSet != null)
    ? Encoding.GetEncoding(response.Content.Headers.ContentType.CharSet).GetString(array)
    : Convert.ToBase64String(array);
```

### 3.17 Template Carousel 的跨欄位一致性檢查
檔案：`Helper/ComponentHelper.cs`（`Template._Carousel`）。LINE 要求 carousel 每個 column 的「有無 title」「有無圖」「action 數量」一致，套件用 `customExamineMap["columns"]` 掛自訂驗證，違規時回報「請統一:所有column內title的有無, …」。這是少數超出單一欄位範圍的本地驗證。

### 3.18 其他小技巧
- `Message.Type`、`Container.Type` 等以 `[Contains(typeof(Type), false)]` 反射列舉名稱做集合檢查，`byValue: true` 則用列舉整數值（Rich Menu 高度 1686/843）。
- `ImageMapButton._GenerateImageMapButtonArea(list, ix, iy, dx, dy, wl, hl, hc, vc)`：把 N 個按鈕自動依格狀鋪 area，省去手算座標。
- `Serialize()` 統一 `NullValueHandling.Ignore` ＋ `StringEnumConverter`，所以模型可以放心用 `null` 代表「不送」。
- `ThumbnailImage`／`Style` 的 `Color?` 參數轉 `#RRGGBB`：`"#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2")`（遺失 alpha；`Color.White` 僅作 null 佔位）。

---

## 4. 設計缺陷／反模式／過時之處

> 以下為靜態閱讀得到的判斷，未執行程式；標註「推測」者表示依賴 LINE 行為或 .NET 版本細節。

| # | 問題 | 證據 |
|---|---|---|
| D1 | **綁死 .NET Framework＋System.Web**，無法用於 ASP.NET Core／.NET 5+ | `RequestHelper.Recieved(string id, HttpRequestBase request)`；`HttpContext.Current.Request.Url`（`GetCurrentUrl`）；`HttpRuntime.AppDomainAppPath`（`GetCurrentRootPath`）；`HttpRequestMessageExtensions.CreateResponse`（WebApi 5.2.3）；`System.Drawing.Bitmap`（Windows GDI+）；`System.Runtime.Caching.MemoryCache.Default` |
| D2 | **每個請求 new 一個 `HttpClient`** 並 `using` 釋放，經典的 socket 耗盡反模式；並且回應不檢查 HTTP 狀態碼 | `RequestHelper.SendRequest`：`using HttpClient client = new HttpClient();`；呼叫端以 `text == "{}"` 判斷成功（`RichMenu.LinkFromUserById`、`UnLinkFromUser`、`BotData.SetRichMenu`） |
| D3 | **Push／Multicast 超過 5 則時，後面的訊息被靜默丟棄**（意圖是遞迴拆批） | `MessageHelper.Push`：`msgList = msgList.GetRange(0, 5); _overLength = msgList.GetRange(5, msgList.Count - 5);` —— 第二行的 `msgList` 已被截成 5 筆，`Count - 5 = 0`，`_overLength` 恆為空 |
| D4 | **`GetGroupRoomData` 疑似無窮迴圈** | `text = (groupMemberModel.next != null) ? "?start=" + next : ""; while (text != null);` —— `text` 永遠不是 null，最後一頁後會重抓第一頁；且 `groupMemberModel` 為 null 時 `.next` 直接 NRE。同時每個成員一個請求（N+1） |
| D5 | **`TextComponent.size` 的驗證與轉換自相矛盾**：`3xl/4xl/5xl` 永遠驗證失敗 | `[Contains(typeof(Size), false)] public string size`：允許值是列舉「名稱」(`txl, qxl, pxl`)，但 `_Text` 設定的是字典轉出的 `"3xl"/"4xl"/"5xl"`。相對地 `ImageComponent`/`IconComponent` 用字串清單所以正常 |
| D6 | **Webhook 解析會在合法請求上失敗** | `receiveMessageModel.events[0].source.GetSourceId()`：LINE 後台「Verify」送出 `{"events":[]}` 會 `ArgumentOutOfRangeException`；`source` 為 null 的事件亦 NRE。事件／訊息 `type` 為未知字串（如日後新增的 `memberJoined`）時，Newtonsoft 對列舉反序列化直接丟例外，整個 payload 失敗（推測）。反序列化在 `try` 之外 |
| D7 | **驗簽前就改動全域狀態；比對非常數時間；未知 id 以空金鑰驗簽** | `BotHelper.CurrentBot[sourceId] = BotHelper.GetBotData(id);` 發生在 HMAC 比較**之前**（未認證請求可塞入任意 sourceId 鍵）；`text2 == text3` 為一般字串比對；`GetBotData` 對未知 id 吞例外回傳空 `BotData`，`ChannelSecret ?? ""` 讓任何人都能用空金鑰算出合法簽章（之後因 `Bot` 為 null 在 try 內 NRE，故影響有限，但設計脆弱） |
| D8 | **靜態可變全域狀態、無執行緒安全** | `BotHelper.CurrentBot`/`BotDataIdMap` 是 `Dictionary`；`CacheHelper` 取出共享 `Dictionary<,>` 後原地修改；整套程式碼沒有 `lock`。並行 webhook 會有競爭條件；`CurrentBot` 以 sourceId 為鍵，同一使用者先後對不同 Bot 發話時，後者覆蓋前者 |
| D9 | **快取語意問題** | `DefaultCache.Add` 用 `ObjectCache.Add`（不存在才新增，已存在不覆寫），整個設計靠「改同一參考」才正確；永不過期（`DateTimeOffset.MaxValue`）、無容量控制；`UserData`／`GroupRoomData` 每次都重抓、快取形同虛設；`Clear()` 回傳值語意相反；Postback 暫存直接用 `MemoryCache.Default` **繞過 `ICache`**，換成 Redis 後仍只存在單機記憶體，重啟或多節點即失效（按鈕「過期」） |
| D10 | **Quartz 2.x 與 `async void` Job** | `Quartz 2.6.1`（Quartz 3 起 `IJob.Execute` 回傳 `Task`、`GetScheduler` 為 async）；`BaseDataUpdateJob.Execute` 為 `async void`，Job 內例外無人觀察，可能直接使程序崩潰；`new StdSchedulerFactory().GetScheduler()` 在靜態欄位初始化時同步執行；`RegisterBaseDataUpdateJobs` 每次都以固定 JobKey 排程，重複呼叫 `SetBaseDataGenerater` 會 `ObjectAlreadyExistsException`；`registerMinuteJob`/`registerSecondJob` 為死碼；IIS 回收應用程式集區即中止排程 |
| D11 | **例外吞掉／錯誤以偽 JSON 字串回傳** | `BotHelper.GetBotData` `catch (Exception) { return new BotData(); }`；`UtilHelper.PostbackSearch.GetRange` 兩個空 `catch (Exception) {}`；`CacheHelper.GetData` 吞 Overflow/ArgumentNull/Argument 例外；Webhook 結果 `"{ ErrorMessage: ..., StackTrace: ... }"` 不是合法 JSON（無引號），且把 StackTrace 放進 HTTP 回應。另 `innerException` 蒐集迴圈每圈加入的是「外層」`Message`（先 Add 再往內走），內層訊息遺失 |
| D12 | **Rich Menu 初始化具破壞性** | `BotData.SetRichMenu` 對 `richmenu/list` 的**每一個**選單（含 LINE OA Manager 手動建立的）逐一 `DELETE`；每次應用程式啟動（`SetBotDataIdMap`）都會觸發，且在啟動路徑上 `await` 多個網路請求；若新建過程中途失敗，已刪除的選單無法回復 |
| D13 | **疑似錯誤的端點／邏輯** | (a) `GetGroupRoomData` 取成員個資用 `.../members/{userId}`（複數），LINE 官方為 `.../member/{userId}`（單數）（推測，需對照官方文件）；(b) `GroupRoom.Leave`：`if (text == "{}") throw new Exception(text);` 與成功判斷顛倒（成功回 `{}` 反而丟例外）；(c) `GroupRoom.Init`/`Delete` 用 `sourceId + "-" + member.userId` 當 `CurrentBot` 鍵呼叫 `AddCacheData/DeleteCacheData`，該鍵不存在於 `CurrentBot` ⇒ `KeyNotFoundException`（靜態推論）；(d) 管理員判斷 `AdminIdList.Contains(_event.source.GetSourceId())` 在群組內拿到的是 groupId 而非 userId，群組中管理員永遠不成立；(e) Rich Menu 與 User API 都用 `source.GetSourceId()` 當 userId，群組事件會帶入 groupId |
| D14 | **本地 URL 驗證過嚴／錯誤** | Regex `^(https?://(www\.)?)?[a-z0-9]+([\-\.]{1}[a-z0-9]+)*\.[a-z]{2,5}(:[0-9]{1,5})?(\/.*)?$`：僅小寫、TLD 2–5 字母，拒絕大寫主機名、IP、`localhost`、新式長 TLD、底線；`UriButton` 掛 `Url(false)`，因此 `tel:`、`line://`、`mailto:` 等 URI scheme 無法使用 |
| D15 | **API 涵蓋面窄且過時** | 僅 reply/push/multicast/profile/群組成員/leave/content/Rich Menu 少數端點。缺：broadcast、narrowcast、quota、bot info、default rich menu、rich menu alias、批次 link、audience、insight、mark as read、loading animation、validate、LINE Login、`memberJoined/Left` 等事件、`file` 以外的新訊息欄位（`mention`、`emojis`、`quoteToken`…）、Flex 較新欄位。content 與 Rich Menu 上傳仍用 `api.line.me`，LINE 現行文件對二進位內容改用 `api-data.line.me`（推測，需對照官方文件） |
| D16 | **God class 與巢狀型別暴增、命名不一致** | `ComponentHelper.cs` 2,552 行、130 個宣告；使用端需寫 `ComponentHelper.Component.TextComponent`、`ComponentHelper.Button.DefaultButton` 等；`IBot` 是抽象類別卻以 `I` 命名；拼字錯誤 `Recieved`、`NeedHttpes`；三套同名 Action 階層（`Button`／`ImageButton`／`ImageMapButton`）近乎複製貼上；`_` 前綴的靜態工廠非 .NET 慣例 |
| D17 | **本機化與時區問題** | `DateTime.Parse(param.date)` 受伺服器文化特性影響；`initial` 預設 `DateTime.Now`（伺服器時區）；Cron 使用主機時區 |
| D18 | **System.Drawing 資源洩漏與怪寫法** | `GetImageMapBaseData` 的 `Bitmap` 均未 `Dispose`；以 `WebClient.OpenRead(本機路徑)` 讀本機檔；反編譯碼中 `(int)val == 0` 之類判斷為殘留的空檢查 |
| D19 | **`CustomReplace` 未轉義** | 替換值直接字串拼入 JSON（`text.Replace(..., val.ToString())`），值含 `"` 或 `\` 時會破壞 JSON 或造成欄位注入；來源通常是 datetimepicker 參數，風險低，但設計不穩健 |
| D20 | **無日誌、無取消權杖、無 DI** | 全程式碼無 logging（Common.Logging 只是 Quartz 的傳遞相依）；所有 async 無 `CancellationToken`；靜態單例難以測試 |
| D21 | **沒有硬編碼 token（正面）** | 全程式碼只有一個固定外部 URL `https://api.line.me/v2/bot/`；`AccessToken`／`ChannelSecret` 都由 `BotData` 建構參數傳入 |

---

## 5. LINE 端點總表（去重）

基底：`RequestHelper.SendRequest(sourceId, ...)` 與 `RequestHelper.SendSetting(...)` 內寫死 `"https://api.line.me/v2/bot/"`，整個組件**只有這一個固定主機字串**（已用 grep 與 IL 清單交叉核對，無其他 `http` 字串常值，Regex 與 `https` 檢查字串除外）。下表 URL 都是基底＋各處字串拼接而成。

| 動詞 | URL | 用途 | 所在型別.方法 |
|---|---|---|---|
| POST | `https://api.line.me/v2/bot/message/reply` | 回覆訊息 | `MessageHelper.Reply`（private） |
| POST | `https://api.line.me/v2/bot/message/push` | 推播訊息 | `MessageHelper.Push` |
| POST | `https://api.line.me/v2/bot/message/multicast` | 多人推播 | `MessageHelper.Multicast` |
| GET | `https://api.line.me/v2/bot/message/{messageId}/content` | 下載訊息內容（回 base64） | `UtilHelper.File.GetContent` |
| GET | `https://api.line.me/v2/bot/profile/{userId}` | 使用者個資 | `CacheHelper.GetUserData` |
| GET | `https://api.line.me/v2/bot/group/{groupId}/members/ids[?start=]` | 群組成員 ID（分頁） | `CacheHelper.GetGroupRoomData` |
| GET | `https://api.line.me/v2/bot/room/{roomId}/members/ids[?start=]` | 聊天室成員 ID（分頁） | `CacheHelper.GetGroupRoomData` |
| GET | `https://api.line.me/v2/bot/group/{groupId}/members/{userId}` | 群組成員個資（路徑疑似應為 `member`） | `CacheHelper.GetGroupRoomData` |
| GET | `https://api.line.me/v2/bot/room/{roomId}/members/{userId}` | 聊天室成員個資（同上疑慮） | `CacheHelper.GetGroupRoomData` |
| POST | `https://api.line.me/v2/bot/group/{groupId}/leave` | 離開群組 | `UtilHelper.GroupRoom.Leave` |
| POST | `https://api.line.me/v2/bot/room/{roomId}/leave` | 離開聊天室 | `UtilHelper.GroupRoom.Leave` |
| POST | `https://api.line.me/v2/bot/richmenu` | 建立 Rich Menu | `BotData.SetRichMenu` |
| POST | `https://api.line.me/v2/bot/richmenu/{richMenuId}/content` | 上傳 Rich Menu 圖片 | `BotData.SetRichMenu` |
| GET | `https://api.line.me/v2/bot/richmenu/{richMenuId}/content` | 下載 Rich Menu 圖片（base64） | `UtilHelper.RichMenu.GetImage` |
| GET | `https://api.line.me/v2/bot/richmenu/list` | 列出 Rich Menu | `BotData.SetRichMenu`、`UtilHelper.RichMenu.GetList` |
| GET | `https://api.line.me/v2/bot/richmenu/{richMenuId}` | 取得單一 Rich Menu | `BotData.SetRichMenu`、`UtilHelper.RichMenu.GetById` |
| DELETE | `https://api.line.me/v2/bot/richmenu/{richMenuId}` | 刪除 Rich Menu | `BotData.SetRichMenu` |
| GET | `https://api.line.me/v2/bot/user/{userId}/richmenu` | 查使用者目前的 Rich Menu | `UtilHelper.RichMenu.GetIdByUser`（`LinkFromUserById`／`UnLinkFromUser` 內部） |
| POST | `https://api.line.me/v2/bot/user/{userId}/richmenu/{richMenuId}` | 連結使用者與 Rich Menu | `UtilHelper.RichMenu.LinkFromUserById`、`BotData.SetRichMenu` |
| DELETE | `https://api.line.me/v2/bot/user/{userId}/richmenu` | 解除使用者的 Rich Menu | `UtilHelper.RichMenu.UnLinkFromUser`、`BotData.SetRichMenu` |

另有一個**入站**路由約定（非 LINE 端點）：`Webhook/ImageBase`（`RequestHelper.GetImageBaseRoute()` 預設值），由開發者自行實作，用來提供 imagemap 圖片。通用方法 `RequestHelper.SendRequest(MethodType, host, position, parameter, content)` 可打任意 host，但套件內沒有呼叫端。

---

## 6. 與 Ozakboy.Line 對照初判（初判，待主代理確認）

> 以下根據任務說明列出的 Ozakboy.Line 功能清單做初步比較，未實際閱讀 Ozakboy.Line 原始碼；「LineBotExtension 有／Ozakboy 沒有」的判斷以任務說明為準。

### (a) LineBotExtension 有、Ozakboy.Line 沒有的功能

1. **強型別 Flex 元件模型**（bubble／carousel／box／text／image／button／separator／spacer／icon／filler＋樣式區塊，含 compile-time 子元件限制）。Ozakboy.Line 的 Flex 只收 `JsonElement`。但本套件僅涵蓋 2018 年規格（缺 span、video、offset、position、padding、cornerRadius…），要借用的是「建模思路」，欄位需依現行規格補齊。
2. **建構時的本地驗證框架**（`ExamineHelper` 風格的屬性式驗證＋中文錯誤訊息）。Ozakboy.Line 有伺服端 validate API，但離線預檢（text 長度、Template 數量、色碼、https 網址、quick reply 上限、carousel 一致性）可在送出前攔截。
3. **快取層**：`ICache` 抽象（可換 Redis）、使用者／群組／Rich Menu 資料快取、Bot 個人暫存區（`CacheData`）。Ozakboy.Line 無。
4. **排程**：Quartz 每日／每月重建基礎資料。Ozakboy.Line 無排程器（但本套件並無排程推播，不是補足推播排程的現成方案）。
5. **Postback 協定層**：型別化 payload（key／search／cache／expired）、超長 payload（>300 字）轉 GUID 暫存、分頁「上一頁／下一頁」產生器、`{{path}}` 範本注入 DatetimePicker 結果。
6. **多 Bot 管理與 IBot 事件抽象**：一個站台、`id` 對應多個 channel；`IBot` 抽象類別把 webhook 事件拆成 10 個回呼；測試／維護模式（`TestMode`＋`AdminIdList`）與管理員接收錯誤訊息。
7. **Imagemap 圖片動態縮放服務**（依 LINE 要求的各寬度輸出並快取）與 `_GenerateImageMapButtonArea` 格狀 area 計算。
8. **Rich Menu 宣告式部署**：圖檔名即 key、啟動同步、自動 link 既有使用者、`linkUserIdList` 追蹤（注意 D12 的破壞性）。
9. **群組／聊天室成員清單抓取與快取**（Ozakboy 已有群組／聊天室 API，但沒有「整包成員＋快取」這層）。
10. **Webhook 事件模型**含 `beacon`、`file` 訊息、`datetimepicker` 的 `params`、`postback`（是否已在 Ozakboy 內，待確認）。
11. **文字指令路由器**（精確指令＋正則數字群組／前綴模糊比對）；Ozakboy 有關鍵字自動回覆，兩者功能重疊，非完全空缺。

### (b) 兩邊都有，但 LineBotExtension 的做法值得學

1. **「建構即驗證」＋ 把 LINE 限制寫成宣告式屬性**，而不是散落在各處的 `if`。可在 Ozakboy 的訊息／Template／Rich Menu 模型上加 DataAnnotations 或 source generator（限制值需對照現行官方文件重新確認，部分已過時，例如單則文字上限、Flex 欄位）。
2. **Template carousel 的跨欄位一致性檢查**（title 有無／圖有無／action 數量一致）——容易漏掉、官方錯誤訊息不友善。
3. **Postback 資料超長自動轉存＋過期友善處理**（Ozakboy 的 webhook 解析應考慮同等機制，但儲存要走可抽換的 cache／distributed cache，而非本套件的 `MemoryCache.Default`）。
4. **Push／Multicast 超過 5 則自動分批** 的需求（實作有 bug，但需求合理；Ozakboy 可正確實作並考慮 Reply 5 則上限、Multicast 500 人上限分批）。
5. **管理員錯誤回報管道**：例外以 LINE 訊息通知管理員，並自動切 ≤2000 字（Ozakboy 可做成可選擇的 error notifier，並避免在群組外洩）。
6. **Rich Menu 以「檔名＝key」的簡單約定管理圖檔**，以及送出前檢查 Rich Menu 尺寸（2500×1686/843）、`chatBarText` ≤14、areas ≤20。
7. **Imagemap `baseUrl` 自動推導與多尺寸輸出**的使用體驗（Ozakboy 若提供 imagemap，可考慮提供 ASP.NET Core 版的圖片尺寸中介層，用 ImageSharp／SkiaSharp 而非 System.Drawing）。
8. **`ICache` 對序列化後物件（JObject）的容忍寫法**——提示外部快取介面應以序列化為前提設計；若 Ozakboy 日後加快取，介面宜直接用 `Get<T>/Set<T>(key, value, TTL)`，而非整包字典。

### (c) LineBotExtension 有、但不值得學的做法（理由）

| 做法 | 不值得學的理由 |
|---|---|
| `System.Web`／WebApi 綁定（`HttpRequestBase`、`HttpContext.Current`） | 與 ASP.NET Core 不相容；Ozakboy 已有 Core 版認證與 webhook 端點，不需要倒退 |
| 全域靜態狀態（`CurrentBot`、靜態 `CacheHelper`） | 無執行緒安全、不可測試、多租戶下會互相覆蓋（D8）；應改用 DI＋`IOptions` |
| 每次 `new HttpClient()` | socket 耗盡風險；應用 `IHttpClientFactory` |
| 回應一律回字串、以 `"{}"` 比對成功 | 無強型別結果、無狀態碼檢查；Ozakboy 應維持強型別回應與例外 |
| `DefaultCache` 的永不過期＋add-if-absent 語意、整包字典快取、`UserData` 只寫不讀 | 設計上快取沒有省到任何請求，且記憶體無上限 |
| Postback 暫存繞過 `ICache` 直接用 `MemoryCache.Default` | 多節點／重啟即失效，與可抽換快取的目標矛盾 |
| Quartz 2.6 ＋ `async void` Job ＋靜態排程器 | 過時且危險；若需要排程應採 `BackgroundService`／Quartz 3+ hosted service；況且本套件排程目的只是重建自家資料，不是推播 |
| `SetRichMenu` 啟動時全刪重建 | 會刪掉營運人員在 OA Manager 建立的選單；Ozakboy 已有完整 Rich Menu／alias API，應提供「同步」而非「清空」 |
| `System.Drawing` 圖片處理 | Windows 專屬、未 Dispose；跨平台應改 ImageSharp／SkiaSharp |
| 三套同名 Action 階層、130 個巢狀型別的 `ComponentHelper` | 重複碼與巨型檔案，Ozakboy 的九種動作已更精簡，不需倒退 |
| 偽 JSON 錯誤字串、吞例外 | 難以診斷；Ozakboy 應用結構化例外與日誌 |
| 手寫 URL Regex 驗證 | 規則錯誤（擋掉 `tel:`、`line://`、大寫主機、IP）；應用 `Uri.TryCreate` 加 scheme 白名單 |
| `Send._Reply` 的「資料表→列→`List<object>`」弱型別個人暫存（`CacheData`） | 以 `List<object>` 搭配 JToken 路徑過濾，型別不安全；對照 Ozakboy 的「訊息範本＋變數替換」較簡潔 |
| 文字指令路由使用 `group.GetType().GetProperty("Name")` 反射取群組名 | 依賴 .NET 執行階段版本才有的公開屬性（推測），跨版本脆弱；Ozakboy 的關鍵字自動回覆已夠用 |
