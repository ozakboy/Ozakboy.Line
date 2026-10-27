# LineBot.SDK 0.2.0 逆向分析報告

- 作者:Martin Hsu(2018)
- 範例 repo:https://github.com/Martin-Hsu/LineBotSDK_Example
- 分析依據:ILSpy 反編譯的 40 個 .cs(約 1,160 行)、LineBotSDK.xml、IL 層級清單、nuspec、範例 repo README
- 方法:只讀分析,未執行任何反編譯出來的程式碼

> **先更正一個前提**:任務說明提到「它沒有相依 Newtonsoft」。實際情況相反。
> 組件參考(assembly refs)為 `mscorlib, RestSharp, System.Core, Newtonsoft.Json, System, Microsoft.CSharp`。
> HTTP 走 **RestSharp**,JSON 序列化與反序列化走 **Newtonsoft.Json**。
> 這兩個套件**沒有**寫在 nuspec 的 `<dependencies>` 裡,nuspec 只宣告了 `LineNotify.SDK 1.0.2`。詳見 1.2。

---

## 1. 套件概觀

### 1.1 基本資料

| 項目 | 內容 |
|---|---|
| NuGet ID / 版本 | `LineBot.SDK` 0.2.0 |
| 實際 DLL | `lib/net45/LineBotSDK.dll`,並附 `LineBotSDK.xml` |
| TFM | **net45**(.NET Framework 4.5,2012 年的目標框架) |
| 授權 | nuspec 沒有 `<license>`,`licenseUrl` 指向 gitbook(https://martinhsu.gitbook.io/linebotsdk),等於沒有明確授權條款 |
| 文件 | README 與 gitbook;XML doc 只覆蓋 LiffControl、MessageUtility 等少數成員 |
| 組件識別資訊 | `AssemblyTitle="ClassLibrary2"`、`Company="Toshiba"`、`AssemblyVersion=1.0.0.0`(見 `Properties/AssemblyInfo.cs`) |

組件識別資訊是 Visual Studio 範本沒改的預設值,與套件 ID 和版本都對不上。

### 1.2 相依

- nuspec 只宣告 `LineNotify.SDK 1.0.2`。
- **反編譯碼完全沒有呼叫 LineNotify.SDK。** 組件參考清單裡沒有它,IL 清單也沒有任何 Notify 相關符號。所以它是「捆綁」性質的相依,不是程式相依。
- LineNotify.SDK 是同作者的 LINE Notify 用戶端套件。依 README 它提供:
  - `GetToeknFromCode`(原文拼錯)
  - `SendNotification`(message、image、sticker)
  - `GetStatus`
  - `RevokeToken`
  - `GenerateHTMLString`
- 這些功能都不在 LineBot.SDK 這顆 DLL 裡。README 把兩個 SDK 寫在同一頁,標題是「C# LineBotSDK && LineNotifySDK」。
- LineNotify.SDK 的 nupkg 不在本機,所以下面是**推測**:RestSharp 與 Newtonsoft.Json 很可能是靠 LineNotify.SDK 傳遞進來的。這是本套件 nuspec 的一個隱性缺陷(見第 4 節)。

### 1.3 命名空間與型別數

頂層型別 39 個,巢狀型別 5 個,共 44 個。IL 清單列 46 個,多出的應是 `<Module>` 與 dynamic call-site 容器 `<>o__2` 之類的編譯器產物。

| 命名空間 | 型別數 | 內容 |
|---|---|---|
| (全域) | 1 | `Extension` |
| `LineBotSDK.Utility` | 2 | `MessageUtility`、`UserUtility`(空類別) |
| `LineBotSDK.Struct` | 10 | `Event`、`IMessage`、`ImagemapMessage`、`LineUserInfo`、`Message`、`Postback`、`ReceievedMessage`、`Size`、`Source`、`TemplateMessageBase` |
| `LineBotSDK.Struct.Messages` | 11 | `AudioMessage`、`ImageMessage`、`VideoMessage`、`LocationMessage`、`StickerMessage`、`TextMessage`、`SendPushMessage`、`IImagemapAction`、`ImagemapArea`、`ImagemapMessageAction`、`ImagemapURIAction` |
| `LineBotSDK.RichMenuSDK` | 1 + 巢狀 enum | `RichMenuControl`、`UploadImageType`(jpeg/png) |
| `LineBotSDK.RichMenuSDK.Struct` | 8 + 巢狀 4 | `Area`、`Bounds`、`CreateRichMenuResponse`、`GetRichMenuResponse`、`IAction`、`ListGetRichMenuListResponse`、`RichMenu`、`Size`;巢狀 `IAction.UrlAction`、`IAction.MessageAction`、`IAction.PostbackAction`、`Size.LineImageSize` |
| `LineBotSDK.LIFF` | 1 | `LiffControl` |
| `LineBotSDK.LIFF.Struct` | 5 | `AddingLIFFAppResponse`、`GetAllLIFFAppsResponse`、`LIFFApp`、`SizeOfLIFF`(enum)、`View` |

另有兩個易混淆的重名型別:`LineBotSDK.Struct.Size`(imagemap 用,建構式 `Size(width, height)`)與 `LineBotSDK.RichMenuSDK.Struct.Size`(Rich Menu 用,可由 enum 建構)。

### 1.4 架構風格

| 面向 | 做法 |
|---|---|
| 呼叫風格 | **混合**。`MessageUtility` 是全靜態 Utility,token 每次呼叫都要傳;`RichMenuControl`、`LiffControl` 是實例類別,token 由建構式注入並存成 `mChannelAccessToken` |
| 同步 / 非同步 | **全同步**。`RestClient.Execute(...)` 直接阻塞,沒有 `Task`、`async`、`CancellationToken` |
| HTTP 層 | RestSharp(同步 `IRestClient/IRestRequest`)。每次呼叫都 `new RestClient(完整 URL)`,沒有共用 client,沒有設 timeout |
| JSON | Newtonsoft.Json,`JsonConvert.SerializeObject(obj, Formatting.None, new JsonSerializerSettings{ NullValueHandling = Ignore })`。body 先轉成字串,再以 `AddParameter("application/json", text, ParameterType.RequestBody)` 送出 |
| 反序列化 | 只用在回應(`CreateRichMenuResponse`、`ListGetRichMenuListResponse`、`GetAllLIFFAppsResponse`、`AddingLIFFAppResponse`)。Webhook 模型有定義,但**套件內沒有任何解析入口** |
| 錯誤處理 | 一律「狀態碼不是 200 就 `throw new Exception($"StatusCode Error:{code} ; Content:{body}")`」。例外型別是裸 `System.Exception`,訊息是字串。唯一例外是 `Extension.ToFile`(吞例外回 false)與 `GetAllLIFFApps`(404 回 null) |
| 多型 | 訊息靠 `IMessage { string type {get;set;} }`。每個實作類別把 `type` 寫成「getter 回傳常數、setter 為空」。序列化時 Newtonsoft 依執行期型別輸出,所以不需要 JsonConverter |

---

## 2. 元件清單(依功能分類)

HTTP 動詞說明:反編譯器沒解開 RestSharp 的 `Method` enum,只剩 `(Method)0/1/2/3`。依 RestSharp 的 enum 序號對應為 GET=0、POST=1、PUT=2、DELETE=3,與各方法語意吻合。

### 2.1 Messaging 送訊息:`LineBotSDK.Utility.MessageUtility`

全部是 `public static void`,內部都呼叫同一個 private `PushMessage(SendPushMessage, token)`。

- `PushTextMessage(token, userId, message)` → `POST https://api.line.me/v2/bot/message/push`
- `PushImageMessage(token, userId, originalContentUrl, previewImageUrl)` → 同上
- `PushVideoMessage(token, userId, originalContentUrl, previewImageUrl)` → 同上
- `PushStickerMessage(token, userId, packageId, stickerId)` → 同上(兩個 ID 都是 string)
- `PushAudioMessage(token, userId, originalContentUrl, int duration)` → 同上
- `PushLocationMessage(token, userId, title, address, decimal latitude, decimal longitude)` → 同上
- `PushImagemapMessage(token, userId, baseUrl, altText, Size baseSize, List<IImagemapAction> actions)` → 同上

**整個套件只有 push 一條訊息路徑**:沒有 reply、multicast、broadcast、narrowcast。每次請求固定只帶 1 則訊息。

### 2.2 訊息型別模型(`Struct.Messages` 與 `Struct`)

| 型別 | 欄位 | `type` 值 | 可見性 |
|---|---|---|---|
| `TextMessage` | `text` | text | **public** |
| `ImageMessage` | `originalContentUrl`、`previewImageUrl` | image | internal |
| `VideoMessage` | `originalContentUrl`、`previewImageUrl` | video | internal |
| `AudioMessage` | `originalContentUrl`、`int duration` | audio | internal |
| `LocationMessage` | `title`、`address`、`decimal latitude`、`decimal longitude` | location | internal |
| `StickerMessage` | `string packageId`、`string stickerId` | sticker | internal |
| `ImagemapMessage`(在 `Struct`) | `baseUrl`、`altText`、`Size baseSize`、`List<IImagemapAction> actions` | imagemap | **public** |
| `ImagemapMessageAction` | `type="message"`、`label`、`text`、`ImagemapArea area` | message | public |
| `ImagemapURIAction` | `type="uri"`、`label`、`linkUri`、`area` | uri | public |
| `ImagemapArea` | `x, y, width, height` | (無) | public |
| `SendPushMessage` | `to`、`List<IMessage> messages` | (無) | public |
| `IMessage` | `string type {get;set;}` | (介面) | public |
| `IImagemapAction` | (空 marker interface) | (介面) | public |

- **Template 訊息**:只有 `TemplateMessageBase` 抽象類別,`type` 是沒有 setter、也沒有 backing 值的 getter-only 屬性,`altText` 一個欄位。沒有任何子類別,也沒有任何程式碼使用它,**實質上未實作**。
- **Flex、Quick Reply** 都沒有。

### 2.3 Webhook 解析(`LineBotSDK.Struct`,純 POCO,沒有解析或驗簽程式碼)

- `ReceievedMessage { List<Event> events }`:類別名稱拼錯(Receieved)。
- `Event { type, replyToken, Source source, long timestamp, Message message, Postback postback }`
- `Source { groupId, userId, type, roomId }`
- `Message { type, id, text, title, address, double latitude, double longitude, int packageId, int stickerId }`:所有訊息型別共用一個扁平類別。
- `Postback { data }`:沒有 `params`(例如 datetimepicker 的結果)。
- 套件內沒有 `Deserialize`、`Parse` 或 `VerifySignature`。使用者要自己 `JsonConvert.DeserializeObject<ReceievedMessage>(body)`(範例 repo 的做法,見第 3 節)。

### 2.4 Rich Menu:`LineBotSDK.RichMenuSDK.RichMenuControl`

建構式 `RichMenuControl(string aChannelAccessToken)`,全部同步。

- `CreateRichMenu(RichMenu)` → `string richMenuId`:`POST https://api.line.me/v2/bot/richmenu`
- `UploadRichMenuImage(richMenuId, path, UploadImageType)` → `bool`:`POST https://api.line.me/v2/bot/richmenu/{id}/content`
- `DownloadRichMenuImage(richMenuId, path)` → `bool`:`GET https://api.line.me/v2/bot/richmenu/{id}/content`,以 `RawBytes.ToFile(path)` 寫檔
- `LinkRichMenuToUser(userId, richMenuId)` → `bool`:`POST https://api.line.me/v2/bot/user/{uid}/richmenu/{rid}`
- `GetRichMenuIDOfUser(userId)` → `string`:`GET https://api.line.me/v2/bot/user/{uid}/richmenu`
- `UnlinkRichMenuFromUser(userId)` → `bool`:`DELETE https://api.line.me/v2/bot/user/{uid}/richmenu`
- `GetRichMenuList()` → `ListGetRichMenuListResponse`:`GET https://api.line.me/v2/bot/richmenu/list`
- `DeleteRichMenu(richMenuId)` → `bool`:`DELETE https://api.line.me/v2/bot/richmenu/{id}`
- `DeleteAllOfRichMenu()` → `bool`:組合方法,先 `GetRichMenuList()`,再逐筆 `DeleteRichMenu`,沒有額外的 HTTP 端點

模型(`RichMenuSDK.Struct`):

- `RichMenu { Size size; bool selected; string name; string chatBarText; List<Area> areas }`
  - 有無參數建構式,預設 `name="name"`、`chatBarText="chatBarText"`、`size=2500x843`。
  - 另有 5 參數建構式 `RichMenu(Size.LineImageSize, selected, name, chatBarText, areas)`。
- `Size`:enum `LineImageSize { size_2500_1686, size_2500_843 }`。
- `Area { Bounds bounds; IAction action }`、`Bounds { x, y, width, height }`
- `IAction`:**是 class 不是 interface**,見第 3 節與第 4 節。
  - 欄位為 `type, uri, text, data`。
  - 巢狀子類別:`UrlAction(uri)`、`MessageAction(text)`、`PostbackAction(text, data)`。
- 回應型別:`CreateRichMenuResponse { richMenuId }`、`GetRichMenuResponse : RichMenu { richMenuId }`、`ListGetRichMenuListResponse { List<GetRichMenuResponse> richmenus }`

### 2.5 LIFF:`LineBotSDK.LIFF.LiffControl`

建構式 `LiffControl(string aChannelAccessToken)`。

- `AddingLIFFApp(SizeOfLIFF, url)` → `AddingLIFFAppResponse{liffId}`:`POST https://api.line.me/liff/v1/apps`,body `{"view":{"type":"...","url":"..."}}`
- `UpdateLIFFApp(liffId, SizeOfLIFF, url)` → `bool`:`PUT https://api.line.me/liff/v1/apps/{liffId}/view`
- `GetAllLIFFApps()` → `GetAllLIFFAppsResponse{List<LIFFApp> apps}`:`GET https://api.line.me/liff/v1/apps`,**404 時回傳 null**
- `DeleteLIFFApp(liffId)` → `bool`:`DELETE https://api.line.me/liff/v1/apps/{liffId}`
- `DeleteAllLIFFApps()` → `bool`:組合方法,先 `GetAllLIFFApps()`,null 就直接回 true,否則逐筆刪除

模型:

- `View(SizeOfLIFF, url)`:把 enum 用 `ToString()` 存成 `type` 字串。
- `SizeOfLIFF { compact, tall, full }`
- `LIFFApp { liffId, View view }`

### 2.6 使用者資料

- `UserUtility`:**空類別**(僅有 `.ctor`),沒有任何成員。
- `LineUserInfo { displayName, userId, pictureUrl, statusMessage }`:模型有,但**沒有任何方法會呼叫 `GET /v2/bot/profile/{userId}` 來填它**。
- 結論:使用者資料功能只有模型,沒有 API。

### 2.7 擴充方法:`Extension`(全域命名空間)

- `static bool ToFile(this byte[] byteArray, string aFilePath)`:`FileMode.Create` 寫檔,`catch (Exception) { return false; }`。只被 `DownloadRichMenuImage` 使用。

---

## 3. 值得注意的實作細節與技巧

### 3.1 用「空 setter 的常數屬性」讓介面多型成立
檔案:`Struct.Messages/TextMessage.cs`、`ImageMessage.cs` 等(所有 `IMessage` 實作)

```csharp
public string type
{
    get { return "text"; }
    set { }          // 吞掉 setter,只為滿足 IMessage { get; set; }
}
```

`IMessage` 只有一個 `type` 屬性。`List<IMessage>` 序列化時,Newtonsoft 依各元素的執行期型別輸出,所以不用 `JsonConverter` 或 `$type`。代價是 `type` 看似可寫,實際寫了沒效果。

### 3.2 「胖 IAction」加巢狀子類別,靠 NullValueHandling.Ignore 輸出乾淨 JSON
檔案:`RichMenuSDK.Struct/IAction.cs`

```csharp
public class IAction {              // 注意是 class
    public string type, uri, text, data;
    public class UrlAction : IAction { public UrlAction(string uri){ base.uri = uri; type = "uri"; } }
    public IAction(UrlAction a){ type = a.type; uri = a.uri; }   // 複製建構式
}
```

三種動作共用同一份四欄位。所有序列化都帶 `NullValueHandling.Ignore`,沒用到的欄位不會輸出。使用者寫法是 `new Area(bounds, new IAction(new IAction.UrlAction("..." )))`,要建兩層物件,才能得到可放進 `Area.action` 的實例。

### 3.3 LIFF 把 404 當成「沒有資料」
檔案:`LIFF/LiffControl.cs`

```csharp
if (resp.StatusCode != HttpStatusCode.OK) {
    if (resp.StatusCode == HttpStatusCode.NotFound) return null;
    throw new Exception($"StatusCode Error:{resp.StatusCode} ; Content:{resp.Content}");
}
```

這是整個套件唯一有特別處理錯誤碼的地方。當頻道沒有任何 LIFF app 時,2018 年的 API 回 404,SDK 把它轉成 null。`DeleteAllLIFFApps` 也跟著檢查 `if (allLIFFApps == null) return true;`。

### 3.4 用 ExpandoObject 組出 `{"view":{...}}`
檔案:`LIFF/LiffControl.cs`(`AddingLIFFApp`)

```csharp
dynamic val3 = new ExpandoObject();
val3.view = view;                         // View{type,url}
string text = JsonConvert.SerializeObject(val3, Formatting.None,
    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
```

這是組出外層包裝物件的省事寫法。為此套件多引用了 `Microsoft.CSharp`(dynamic binder)。`UpdateLIFFApp` 則直接送 `View`(因為 URL 已帶 `/view`)。

### 3.5 Rich Menu 圖片以 raw bytes 做 RequestBody 上傳,Content-Type 取自 enum 名稱
檔案:`RichMenuSDK/RichMenuControl.cs`(`UploadRichMenuImage`)

```csharp
string text = "image/" + aUploadImageType.ToString("G");   // jpeg | png
req.AddHeader("Content-Type", text);
FileStream fs = new FileStream(path, FileMode.Open);
byte[] array = new byte[fs.Length];
fs.Read(array, 0, (int)fs.Length);
req.AddParameter(text, array, ParameterType.RequestBody);
```

enum 成員剛好命名為 `jpeg`、`png`,所以 `ToString("G")` 直接得到 MIME 後半段。這招很省事,但 enum 命名被 MIME 綁住。缺陷見第 4 節。

### 3.6 Rich Menu 尺寸預設值與 enum 建構式
檔案:`RichMenuSDK.Struct/Size.cs`、`RichMenu.cs`

```csharp
public enum LineImageSize { size_2500_1686, size_2500_843 }
public Size() { width = 2500; height = 843; }
public RichMenu() { size = new Size(); selected = false;
                    name = "name"; chatBarText = "chatBarText"; areas = new List<Area>(); }
```

官方兩種標準尺寸被做成 enum,並提供預設值,新手不用記像素數字。

### 3.7 全部 Push* 都是單一 private 方法的薄包裝
檔案:`Utility/MessageUtility.cs`

```csharp
PushMessage(new SendPushMessage {
    to = aUserID,
    messages = new List<IMessage>{ new ImageMessage(originalContentUrl, previewImageUrl) }
}, aChannelAccessToken);
```

七個 public 方法只負責建構訊息物件,HTTP、序列化、錯誤處理都集中在一處。好處是改一次就全部生效。壞處是 `messages` 永遠只有一則。

### 3.8 `ToFile` 擴充方法:吞例外換 bool
檔案:`Extension.cs`

```csharp
using FileStream fileStream = new FileStream(aFilePath, FileMode.Create, FileAccess.Write);
fileStream.Write(byteArray, 0, byteArray.Length);
return true;
...
catch (Exception) { return false; }
```

`using` 宣告式寫法會正確釋放資源。但它把所有失敗(權限、路徑不存在、磁碟滿)都壓成同一個 false。

### 3.9 Webhook 使用方式要看範例 repo,SDK 本身不提供入口
檔案:`Struct/ReceievedMessage.cs`、`Event.cs`

套件只給 POCO:`ReceievedMessage { List<Event> events }`。實際解析由使用者在自己的 WebAPI/MVC controller 以 `JsonConvert.DeserializeObject<ReceievedMessage>(...)` 完成。這件事是依模型形狀推論的,README 沒有展示範例程式碼。

### 3.10 README 與實際功能不一致
- README 的「Now support functions」只列了 MessageUtility 的 7 個 Push 與 LiffControl 的 5 個方法。
- **`RichMenuControl`(9 個方法)完全沒出現在 README**,卻是套件內最大的一塊。
- 另外,README 把 LineNotify 的函式列在同一頁,容易誤以為都在本 DLL 裡。

---

## 4. 設計缺陷 / 反模式 / 過時之處

| # | 問題 | 證據 |
|---|---|---|
| 1 | **目標框架 net45**,無 .NET Standard / .NET Core 版本 | `lib/net45` 是唯一資料夾,在 .NET 6+ 或 ASP.NET Core 專案只能靠相容層 |
| 2 | **全同步阻塞**,沒有 async/await,也沒有 CancellationToken | 每個方法都 `val.Execute(...)`(RestSharp 同步)。在 ASP.NET 請求執行緒上會佔用執行緒,webhook 處理延遲會被 LINE 重送 |
| 3 | **每次呼叫都新建 RestClient,沒有 timeout、重試或連線重用** | 所有方法都 `new RestClient("完整URL")`,沒有設 `Timeout` |
| 4 | **隱性相依**:RestSharp 與 Newtonsoft.Json 沒寫在 nuspec,而宣告的 LineNotify.SDK 卻完全沒被程式碼使用 | 組件參考含 RestSharp、Newtonsoft.Json;nuspec 只有 `LineNotify.SDK 1.0.2`。版本衝突時使用者無從得知該用哪個版本 |
| 5 | **例外處理粗糙**:一律裸 `Exception`,沒有自訂例外型別,沒有 HTTP 狀態碼屬性,也沒有 LINE 錯誤 JSON 的解析 | `throw new Exception($"StatusCode Error:{...} ; Content:{...}")`。呼叫端只能解析字串。連網路層失敗(StatusCode=0)也走同一路 |
| 6 | **中英文錯誤訊息不一致** | `LinkRichMenuToUser`、`GetRichMenuIDOfUser`、`DeleteRichMenu` 用「StatusCode 錯誤」,其餘用「StatusCode Error」,看起來是不同時間複製貼上的 |
| 7 | **訊息型別大多是 internal**,使用者無法自行組合訊息 | `ImageMessage`、`VideoMessage`、`AudioMessage`、`LocationMessage`、`StickerMessage` 都是 `internal class`;只有 `TextMessage`、`ImagemapMessage` 公開。`SendPushMessage` 雖然 public,外部卻只能放得進文字與 imagemap。要送多則訊息或自訂型別,只能自己實作 `IMessage` |
| 8 | **只有 push,沒有 reply** | 端點只有 `/v2/bot/message/push`。Webhook 的 `Event.replyToken` 被解析了,SDK 卻沒有任何方法可用它。回覆用戶還得耗 push 額度,在後來的 LINE 計費下是實質成本 |
| 9 | **單則訊息、無 `notificationDisabled`、無 retry key** | `messages = new List<IMessage>{ 單一元素 }`,而 LINE 一次最多可送 5 則 |
| 10 | **IAction 設計成一個胖 class,不是 interface** | 名字叫 `IAction` 卻是 `public class IAction`;`UrlAction : IAction` 繼承它;還要再用 `new IAction(UrlAction)` 轉一次。三種動作共用 `type/uri/text/data`,沒有 `label`、沒有 `displayText`;**datetimepicker、camera、cameraRoll、location、richmenuswitch 等動作都不支援**;使用者能建出 `type="uri"` 卻沒給 uri 的物件 |
| 11 | **Postback 動作用 `text`**:`PostbackAction(text, data)` 把第一個參數存進 `text` | 這是 2018 年的欄位;現行 LINE API 以 `label` 與 `displayText` 為主,`text` 屬舊欄位。不過 `label` 在 `IAction` 完全沒有,所以也無法補 |
| 12 | **Rich Menu 圖片上傳有資源洩漏與讀取不完整的風險** | `new FileStream(path, FileMode.Open)` 沒有 `using` 或 `Dispose`,檔案會一直被鎖到 GC;`fs.Read(array, 0, len)` 只呼叫一次,理論上可能讀不滿;**圖片全部載入記憶體**;沒檢查檔案大小(LINE 限 1MB) |
| 13 | **圖片上傳與下載仍走 `api.line.me`** | 依我對 LINE 官方文件的了解,rich menu 圖片的上傳與下載現行應走 `api-data.line.me`。此點我**無法在本環境驗證**,請以官方文件為準 |
| 14 | **LIFF 端點是 v1 風格** | `PUT /liff/v1/apps/{id}/view`。據我所知,現行官方 LIFF server API 是 `PUT /liff/v1/apps/{liffId}`(可更新 description、features、permanentLinkPattern、scope、botPrompt 等欄位)。`View` 也只有 `type`、`url`,缺 `moduleMode` 等後續欄位。同樣**無法在本環境驗證** |
| 15 | **LIFF 用 dynamic/ExpandoObject 組外層 body**,多拉進 `Microsoft.CSharp` 相依,也損失編譯期檢查 | `AddingLIFFApp` 用 `dynamic val3 = new ExpandoObject(); val3.view = view;` |
| 16 | **Webhook 模型粗糙** | `Message` 把全部訊息型別壓成一個類別(text、location 與 sticker 的欄位混在一起),數值欄位非 nullable,所以文字訊息解析後 `latitude=0`、`packageId=0`,**無法分辨「沒有」與「零」**;沒有 image/video/audio/file 的 `contentProvider`、`duration`、`fileName`;沒有 follow、join、leave、beacon 等事件所需欄位,也沒有 `mode`、`webhookEventId`、`deliveryContext`。`packageId` 在接收端是 int,送出端卻是 string,型別不一致 |
| 17 | **完全沒有簽章驗證**(`X-Line-Signature` / HMAC-SHA256) | 整個專案找不到任何驗簽邏輯。有 webhook 模型卻不教使用者驗簽,等於默許不安全的實作 |
| 18 | **`UserUtility` 是空殼,`LineUserInfo` 沒人用,`TemplateMessageBase` 沒有子類別** | 三個「半成品」留在公開 API 裡,誤導使用者以為已支援 profile 與 template |
| 19 | **公開 API 的命名與拼字問題** | `ReceievedMessage`(Receieved)、`AddingLIFFApp`(動名詞)、`GetRichMenuIDOfUser`/`GetRichMenuList`(大小寫與前後綴不統一)、`ListGetRichMenuListResponse`(單字順序怪)、兩個 `Size` 重名、`Extension` 放在全域命名空間且名稱過於籠統 |
| 20 | **混用 public 欄位與屬性** | `Struct.Messages.*`、`RichMenuSDK.Struct.*`、`LIFF.Struct.*` 用 public 欄位;`Struct.*` 用屬性。Newtonsoft 兩者都能序列化,但在資料繫結、MVC model binding、反射型工具上行為不一致 |
| 21 | **Token 每次呼叫都得傳(MessageUtility)** | 但 RichMenuControl 與 LiffControl 卻把 token 放在建構式,同一個套件有兩套使用習慣 |
| 22 | **`DeleteAllOfRichMenu` 沒有 null 檢查**,與 `DeleteAllLIFFApps` 不一致 | 前者直接 `richMenuList.richmenus.Count`;後者先判斷 null。另外 `richmenus` 欄位若 API 回空,Newtonsoft 預設留 null |
| 23 | **IMessage 的 `set` 空實作**:屬性可寫但寫了無效 | 見 3.1 |
| 24 | **套件中繼資料粗糙** | `AssemblyTitle=ClassLibrary2`、`Company=Toshiba`、`AssemblyVersion=1.0.0.0`(套件為 0.2.0)、無正式授權、description 只有一個連結 |
| 25 | **沒有單元測試可注入點** | 所有類別直接 `new RestClient`,無法替換成假的 HTTP 層,`MessageUtility` 又是靜態類別 |

---

## 5. LINE 端點總表

全部端點都在 `https://api.line.me`,認證為 `Authorization: Bearer {channelAccessToken}`。以下 URL 都取自 IL 字串常值。

| # | URL | 動詞 | 用途 | 所在型別.方法 |
|---|---|---|---|---|
| 1 | `/v2/bot/message/push` | POST | 推播訊息 | `MessageUtility.PushMessage`(private),由 7 個 `Push*Message` 呼叫 |
| 2 | `/v2/bot/richmenu` | POST | 建立 Rich Menu | `RichMenuControl.CreateRichMenu` |
| 3 | `/v2/bot/richmenu/{richMenuId}/content` | POST | 上傳 Rich Menu 圖片 | `RichMenuControl.UploadRichMenuImage` |
| 3 | `/v2/bot/richmenu/{richMenuId}/content` | GET | 下載 Rich Menu 圖片 | `RichMenuControl.DownloadRichMenuImage` |
| 4 | `/v2/bot/richmenu/{richMenuId}` | DELETE | 刪除 Rich Menu | `RichMenuControl.DeleteRichMenu`(`DeleteAllOfRichMenu` 迴圈呼叫) |
| 5 | `/v2/bot/richmenu/list` | GET | 列出 Rich Menu | `RichMenuControl.GetRichMenuList` |
| 6 | `/v2/bot/user/{userId}/richmenu/{richMenuId}` | POST | 將 Rich Menu 連結到使用者 | `RichMenuControl.LinkRichMenuToUser` |
| 7 | `/v2/bot/user/{userId}/richmenu` | GET | 取得使用者目前的 Rich Menu ID | `RichMenuControl.GetRichMenuIDOfUser` |
| 7 | `/v2/bot/user/{userId}/richmenu` | DELETE | 解除使用者的 Rich Menu | `RichMenuControl.UnlinkRichMenuFromUser` |
| 8 | `/liff/v1/apps` | POST | 新增 LIFF app | `LiffControl.AddingLIFFApp` |
| 8 | `/liff/v1/apps` | GET | 列出 LIFF apps(404 回 null) | `LiffControl.GetAllLIFFApps` |
| 9 | `/liff/v1/apps/{liffId}/view` | PUT | 更新 LIFF view | `LiffControl.UpdateLIFFApp` |
| 10 | `/liff/v1/apps/{liffId}` | DELETE | 刪除 LIFF app | `LiffControl.DeleteLIFFApp`(`DeleteAllLIFFApps` 迴圈呼叫) |

小計:10 個 URL 樣板、13 組(動詞 + URL)。

沒有的端點(以 IL 字串常值為準):`/v2/bot/message/reply`、`multicast`、`broadcast`、`narrowcast`、`/v2/bot/profile/{userId}`、`/v2/bot/message/{id}/content`、group/room 相關、`/v2/bot/user/all/richmenu/*`(預設 Rich Menu)、`/v2/bot/richmenu/alias/*`、`/v2/bot/info`、quota。

---

## 6. 與 Ozakboy.Line 對照初判(初判,待主代理確認)

### (a) 對方有、Ozakboy.Line 沒有的

1. **LIFF 管理 API**(add / update / get all / delete,共 4 個端點,外加 `DeleteAllLIFFApps` 組合)。這是唯一明確的功能缺口。
   - 若要補:不要照抄 `/view` 這條路,建議先對照官方現行 LIFF server API(新增、更新、列表、刪除),並支援 `view.type`(compact/tall/full)、`view.url`、`description`、`features`、`permanentLinkPattern`、`scope`、`botPrompt` 等欄位。這些欄位是我依記憶列的,需主代理查官方文件確認。
2. 兩個「便利組合」:`DeleteAllOfRichMenu`、`DeleteAllLIFFApps`。Ozakboy 如已有 rich menu 列表與刪除,只需要自己組合,價值低。
3. `byte[].ToFile(path)` 這類「下載後直接寫檔」的小工具。不確定 Ozakboy 是否有等價物。

### (b) 兩邊都有,但對方做法值得學的

1. **LIFF 與 Rich Menu 的清單端點對「查無資料」的語意處理**。對方把 LIFF 的 404 轉成 null 並讓 `DeleteAll` 容忍。如果 Ozakboy 的 LIFF 或 rich menu 清單在「空」時會丟例外,可以改成回空集合。注意:建議回**空集合**而不是 null。
2. **Rich Menu 尺寸預設值**:`RichMenu()` 無參數建構式預設 2500x843,另有 `LineImageSize` enum。Ozakboy 若尚未提供尺寸 enum 或預設值,可考慮加上。LINE 現行尺寸不只兩種,不要照抄 enum 的兩個成員。
3. **動作的便利建構式**:`new IAction.UrlAction(uri)`、`MessageAction(text)`、`PostbackAction(text, data)`。Ozakboy 已有九種動作,可檢查是否也有同樣簡短的建構方式或工廠方法。
4. **一行式 `PushTextMessage(token, userId, text)` 的簡潔度**。這是 facade 設計,Ozakboy 如果已有 DI 式用法,可以在文件或 README 中補一行式範例,降低入門門檻。
5. **Imagemap 的 `type` 預設值寫成欄位初始化**(`type = "message"`、`"uri"`),使用者不用自己填。Ozakboy 若已使用 enum 或常數就不需要。

### (c) 對方有、但不值得學的

| 項目 | 不值得學的理由 |
|---|---|
| RestSharp 同步呼叫、每次 new client | 阻塞執行緒、無 timeout、不重用連線,與 Ozakboy 的 ASP.NET Core 與非同步方向相反 |
| 裸 `Exception` 加字串 | 無法程式化處理。Ozakboy 應維持帶狀態碼、request id、LINE 錯誤 JSON 的自訂例外 |
| 訊息類別設為 internal | 讓使用者無法組合多則訊息,也無法重用型別 |
| 胖 `IAction` class 加複製建構式 | 命名誤導、欄位互相污染、不支援新動作,不如 Ozakboy 的九種動作各自成型別 |
| `ExpandoObject/dynamic` 組 body | 無編譯期檢查,多拉 `Microsoft.CSharp` |
| `UserUtility` 空殼、`TemplateMessageBase` 無子類別 | 半成品公開 API,會誤導使用者 |
| Token 每次呼叫都傳 | 與 Ozakboy 的設定注入相比易錯、易洩漏 |
| Webhook 模型把所有訊息壓成一個 `Message` 類別,非 nullable 數值欄位 | 無法區分「無」與「零」;Ozakboy 既有 HMAC 驗簽與解析,不需要退回 |
| 無驗簽 | 安全缺陷,Ozakboy 已有 HMAC 驗簽,應保持 |
| 只有 push、一次一則 | Ozakboy 已涵蓋 reply、multicast、broadcast、narrowcast |
| 圖片上傳 `FileStream` 不釋放加整檔讀入記憶體 | 資源洩漏。Ozakboy 應使用 `Stream` 或 `using` 的串流上傳 |
| 用 enum 名稱直接當 MIME(`jpeg`/`png`) | 小技巧但命名被 MIME 綁死,且不支援其他格式 |
| LIFF `PUT .../view` 的舊端點 | 疑似過時,見第 4 節 #14 |

### 結論

LineBot.SDK 0.2.0 是 2018 年早期、功能很窄的包裝層:push 7 種訊息、Rich Menu 9 個方法、LIFF 5 個方法,總共 13 組 HTTP 呼叫。對 Ozakboy.Line 來說**實質上只有 LIFF 值得參考**,而且應該以官方現行文件為準,不是複製它的實作。
