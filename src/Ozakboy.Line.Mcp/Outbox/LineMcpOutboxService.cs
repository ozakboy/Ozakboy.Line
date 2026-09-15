using System.Globalization;
using System.Text.Json;
using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Messaging;
using Ozakboy.Line.Messaging.RichMenu;

namespace Ozakboy.Line.Mcp.Outbox;

/// <summary>
/// <see cref="ILineMcpOutboxService"/> 的實作。
/// The implementation of <see cref="ILineMcpOutboxService"/>.
/// </summary>
public sealed class LineMcpOutboxService : ILineMcpOutboxService
{
    /// <summary>
    /// 圖文選單圖片的大小上限。
    /// The size cap on a rich menu image.
    /// </summary>
    /// <remarks>
    /// LINE 自己的上限是 1 MB。在抓取時就擋下來,而不是抓完整份再交給 LINE 退回 ——
    /// 位址由外部 AI 提供,沒有上限的話一個指向大檔的位址就能把宿主的記憶體吃掉。
    /// LINE's own limit is 1 MB. It is enforced while fetching rather than after downloading the whole thing and
    /// letting LINE refuse it: the address comes from an outside AI, and without a cap one pointing at a large
    /// file is enough to exhaust the host's memory.
    /// </remarks>
    private const int MaxImageBytes = 1024 * 1024;

    private static readonly string[] AllowedImageTypes = ["image/jpeg", "image/png"];

    private readonly ILineMcpOutboxStore _store;
    private readonly ILineMessagingClient _messaging;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TimeProvider _clock;

    /// <summary>
    /// 建立服務。
    /// Creates the service.
    /// </summary>
    /// <param name="store">待發佇列。The outbox.</param>
    /// <param name="messaging">Messaging API 用戶端。The Messaging API client.</param>
    /// <param name="httpClientFactory">抓圖用的用戶端工廠。The client factory used to fetch images.</param>
    /// <param name="clock">時間來源;為 <see langword="null"/> 時用系統時鐘。The time source, or <see langword="null"/> for the system clock.</param>
    /// <exception cref="ArgumentNullException">
    /// 前三個參數之一為 <see langword="null"/> 時擲出。
    /// Thrown when any of the first three arguments is <see langword="null"/>.
    /// </exception>
    public LineMcpOutboxService(
        ILineMcpOutboxStore store,
        ILineMessagingClient messaging,
        IHttpClientFactory httpClientFactory,
        TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(messaging);
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        _store = store;
        _messaging = messaging;
        _httpClientFactory = httpClientFactory;
        _clock = clock ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<Result<LineMcpOutboxItem>> ApproveAndSendAsync(string id, CancellationToken cancellationToken = default)
    {
        var found = await RequirePendingAsync(id, cancellationToken).ConfigureAwait(false);
        if (found.IsFailure)
        {
            return found;
        }

        var item = found.GetValueOrThrow();
        var now = _clock.GetUtcNow();

        var sent = await SendAsync(item, cancellationToken).ConfigureAwait(false);

        item.DecidedAt = now;
        item.SentAt = sent.IsSuccess ? now : null;
        item.Status = sent.IsSuccess ? LineMcpOutboxStatus.Sent : LineMcpOutboxStatus.Failed;
        item.Error = sent.IsSuccess ? null : sent.Error.Message;

        await _store.UpdateAsync(item, cancellationToken).ConfigureAwait(false);

        // 送出失敗時回的仍然是<b>成功</b>的 Result,值是那筆記為 Failed 的項目。
        // 這個方法的契約是「把決定套用上去並如實記錄」,而那件事做到了;
        // 送出本身的成敗看 Status 與 Error,不是看這個 Result —— 兩者混在一起的話,
        // 呼叫端會分不清「失敗」指的是「這筆沒能更新」還是「這則訊息沒發出去」。
        // A failed send still returns a <b>successful</b> Result whose value is the item marked Failed. This
        // method's contract is to apply the decision and record it faithfully, and it did; whether the send
        // itself worked is Status and Error's business, not this Result's. Merged, a caller could not tell a
        // failure to record from a failure to send.
        return Result.Success(item);
    }

    /// <inheritdoc />
    public async Task<Result<LineMcpOutboxItem>> RejectAsync(
        string id,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var found = await RequirePendingAsync(id, cancellationToken).ConfigureAwait(false);
        if (found.IsFailure)
        {
            return found;
        }

        var item = found.GetValueOrThrow();
        item.Status = LineMcpOutboxStatus.Rejected;
        item.DecidedAt = _clock.GetUtcNow();
        item.Error = reason;

        await _store.UpdateAsync(item, cancellationToken).ConfigureAwait(false);
        return Result.Success(item);
    }

    /// <inheritdoc />
    public async Task<Result<LineMcpOutboxItem>> CancelAsync(string id, CancellationToken cancellationToken = default)
    {
        var found = await RequirePendingAsync(id, cancellationToken).ConfigureAwait(false);
        if (found.IsFailure)
        {
            return found;
        }

        var item = found.GetValueOrThrow();
        item.Status = LineMcpOutboxStatus.Canceled;
        item.DecidedAt = _clock.GetUtcNow();

        await _store.UpdateAsync(item, cancellationToken).ConfigureAwait(false);
        return Result.Success(item);
    }

    /// <summary>
    /// 取出項目並確認它還在待審狀態。
    /// Fetches an item and checks that it is still pending.
    /// </summary>
    /// <param name="id">項目識別碼。The item identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>待審中的項目。The pending item.</returns>
    private async Task<Result<LineMcpOutboxItem>> RequirePendingAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var item = await _store.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return Error.NotFound(
                LineErrorCodes.McpOutboxNotFound,
                $"找不到待發項目 {id}。There is no outbox item {id}.");
        }

        return item.Status == LineMcpOutboxStatus.PendingReview
            ? Result.Success(item)
            : Error.Conflict(
                LineErrorCodes.McpOutboxInvalidStatus,
                $"待發項目 {id} 目前是 {item.Status},只有待審中的項目可以做決定。Outbox item {id} is {item.Status}; only a pending item can be decided.");
    }

    /// <summary>
    /// 依項目種類呼叫對應的 Messaging API。
    /// Calls the Messaging API that goes with the item's kind.
    /// </summary>
    /// <param name="item">項目。The item.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>送出的結果。The send's result.</returns>
    private async Task<Result> SendAsync(LineMcpOutboxItem item, CancellationToken cancellationToken)
    {
        JsonDocument payload;
        try
        {
            payload = JsonDocument.Parse(item.PayloadJson);
        }
        catch (JsonException exception)
        {
            var error = Error.Validation(
                LineErrorCodes.InvalidJson,
                "待發項目的內容無法解析。The outbox item's payload could not be parsed.");
            return Result.Failure(error with { Exception = exception });
        }

        using (payload)
        {
            var root = payload.RootElement;

            return item.Kind switch
            {
                LineMcpOutboxKind.Push => await SendPushAsync(root, cancellationToken).ConfigureAwait(false),
                LineMcpOutboxKind.Multicast => await SendMulticastAsync(root, cancellationToken).ConfigureAwait(false),
                LineMcpOutboxKind.Broadcast => await _messaging
                    .BroadcastRawJsonAsync(ReadRaw(root, "messages"), options: null, cancellationToken)
                    .ConfigureAwait(false),
                _ => await ReplaceRichMenuAsync(root, cancellationToken).ConfigureAwait(false),
            };
        }
    }

    /// <summary>
    /// 送出推播。
    /// Sends a push.
    /// </summary>
    /// <param name="root">內容的根物件。The payload's root object.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>送出的結果。The send's result.</returns>
    /// <remarks>
    /// 推播的回應帶有已送出訊息的清單,但這裡不留 —— 待發項目要記的是「送出去了沒有」,
    /// 而訊息識別碼在這條路徑上沒有後續用途。
    /// A push answers with the list of messages it sent, which is not kept here: what an outbox item records is
    /// whether it went out, and the message identifiers have no further use on this path.
    /// </remarks>
    private async Task<Result> SendPushAsync(JsonElement root, CancellationToken cancellationToken)
    {
        var pushed = await _messaging
            .PushRawJsonAsync(ReadString(root, "to"), ReadRaw(root, "messages"), options: null, cancellationToken)
            .ConfigureAwait(false);

        return pushed.ToResult();
    }

    /// <summary>
    /// 送出群發。
    /// Sends a multicast.
    /// </summary>
    /// <param name="root">內容的根物件。The payload's root object.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>送出的結果。The send's result.</returns>
    /// <remarks>
    /// 群發沒有 raw JSON 版本,所以這裡先把訊息 JSON 轉成 <c>RawMessage</c> 再送。
    /// There is no raw JSON form of multicast, so the message JSON is turned into <c>RawMessage</c> objects first.
    /// </remarks>
    private async Task<Result> SendMulticastAsync(JsonElement root, CancellationToken cancellationToken)
    {
        var userIds = new List<string>();
        if (root.TryGetProperty("userIds", out var ids) && ids.ValueKind == JsonValueKind.Array)
        {
            foreach (var id in ids.EnumerateArray())
            {
                if (id.ValueKind == JsonValueKind.String && id.GetString() is { } value)
                {
                    userIds.Add(value);
                }
            }
        }

        var messages = LineMcpMessages.Parse(ReadRaw(root, "messages"));
        return messages.IsFailure
            ? messages.ToResult()
            : await _messaging.MulticastAsync(userIds, messages.GetValueOrThrow(), options: null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 抓圖並替換圖文選單。
    /// Fetches the image and replaces the rich menu.
    /// </summary>
    /// <param name="root">內容的根物件。The payload's root object.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>替換的結果。The replacement's result.</returns>
    private async Task<Result> ReplaceRichMenuAsync(JsonElement root, CancellationToken cancellationToken)
    {
        if (!root.TryGetProperty("menu", out var menuElement) || menuElement.ValueKind != JsonValueKind.Object)
        {
            return Error.Validation(
                LineErrorCodes.InvalidJson,
                "待發項目缺少 menu 物件。The outbox item has no menu object.");
        }

        var parsed = LineMcpRichMenu.Parse(menuElement);
        if (parsed.IsFailure)
        {
            return parsed.ToResult();
        }

        var menu = parsed.GetValueOrThrow();

        var image = await FetchImageAsync(ReadString(root, "imageUrl"), cancellationToken).ConfigureAwait(false);
        if (image.IsFailure)
        {
            return image.ToResult();
        }

        var (bytes, contentType) = image.GetValueOrThrow();
        var options = new LineRichMenuReplaceOptions
        {
            SetAsDefault = root.TryGetProperty("setAsDefault", out var setAsDefault) && setAsDefault.ValueKind == JsonValueKind.True,
            OldRichMenuId = NullIfEmpty(ReadString(root, "oldRichMenuId")),
            AliasId = NullIfEmpty(ReadString(root, "aliasId")),
        };

        var replaced = await _messaging
            .ReplaceRichMenuAsync(menu, bytes, contentType, options, cancellationToken)
            .ConfigureAwait(false);

        return replaced.ToResult();
    }

    /// <summary>
    /// 抓取圖文選單圖片。
    /// Fetches a rich menu image.
    /// </summary>
    /// <param name="imageUrl">圖片位址。The image's address.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>圖片位元組與其型別。The image bytes and their type.</returns>
    /// <remarks>
    /// 三道檢查缺一不可:位址必須是絕對的 http / https(擋掉 <c>file://</c> 這類本機協定)、
    /// 型別必須是 JPEG 或 PNG、大小不得超過 1 MB。位址是外部 AI 給的,
    /// 這是整個套件裡唯一一條目的地不由自己決定的請求。
    /// All three checks matter: the address must be absolute http or https, which keeps schemes like
    /// <c>file://</c> out; the type must be JPEG or PNG; and the size must stay under 1 MB. The address comes
    /// from an outside AI, and this is the one request in the package whose destination is not its own.
    /// </remarks>
    private async Task<Result<(byte[] Bytes, string ContentType)>> FetchImageAsync(string imageUrl, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return Error.Validation(
                LineErrorCodes.McpImageFetchFailed,
                "圖片位址必須是絕對的 http 或 https 網址。The image address must be an absolute http or https URL.");
        }

        var client = _httpClientFactory.CreateClient(LineMcpHttpClientNames.ImageFetch);

        using var response = await client
            .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return Error.Validation(
                LineErrorCodes.McpImageFetchFailed,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"抓取圖片失敗,狀態碼 {(int)response.StatusCode}。Fetching the image failed with status {(int)response.StatusCode}."));
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        if (!AllowedImageTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            return Error.Validation(
                LineErrorCodes.McpImageFetchFailed,
                $"圖片型別必須是 image/jpeg 或 image/png,目前是「{contentType}」。The image type must be image/jpeg or image/png; it is \"{contentType}\".");
        }

        // 先看宣告的長度。伺服器誠實時,超大的檔案一個位元組都不用讀就能擋掉。
        // The declared length is checked first: when the server is honest, an oversized file is refused without
        // reading a byte of it.
        if (response.Content.Headers.ContentLength is { } declared && declared > MaxImageBytes)
        {
            return TooLarge(declared);
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

        // 再看實際讀到的長度。Content-Length 可以是錯的、也可以根本沒有(分塊傳輸),
        // 只信標頭等於沒有上限。
        // And then the length actually read: Content-Length can be wrong, or absent altogether under chunked
        // transfer, and trusting the header alone is the same as having no cap.
        return bytes.Length > MaxImageBytes
            ? TooLarge(bytes.Length)
            : Result.Success((bytes, contentType));
    }

    /// <summary>
    /// 「圖片太大」的失敗。
    /// The "image is too large" failure.
    /// </summary>
    /// <param name="size">實際大小。The actual size.</param>
    /// <returns>失敗。The failure.</returns>
    private static Error TooLarge(long size) => Error.Validation(
        LineErrorCodes.McpImageFetchFailed,
        string.Create(
            CultureInfo.InvariantCulture,
            $"圖片大小為 {size} 位元組,超過上限 {MaxImageBytes} 位元組。The image is {size} bytes, over the {MaxImageBytes}-byte cap."));

    /// <summary>
    /// 讀一個字串欄位,沒有時回空字串。
    /// Reads a string field, or an empty string when it is absent.
    /// </summary>
    /// <param name="root">根物件。The root object.</param>
    /// <param name="name">欄位名。The field name.</param>
    /// <returns>欄位值。The value.</returns>
    private static string ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    /// <summary>
    /// 讀一個欄位的原始 JSON,沒有時回空陣列。
    /// Reads a field's raw JSON, or an empty array when it is absent.
    /// </summary>
    /// <param name="root">根物件。The root object.</param>
    /// <param name="name">欄位名。The field name.</param>
    /// <returns>原始 JSON。The raw JSON.</returns>
    private static string ReadRaw(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) ? value.GetRawText() : "[]";

    /// <summary>
    /// 空字串轉 <see langword="null"/>。
    /// Turns an empty string into <see langword="null"/>.
    /// </summary>
    /// <param name="value">字串。The string.</param>
    /// <returns>有內容時為原值。The original value when it has content.</returns>
    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
