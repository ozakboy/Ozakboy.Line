using System.Globalization;
using System.Text.Json;
using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Messaging.Messages;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// <see cref="LineMessagingClient"/> 的聊天相關端點:好友清單、群組與聊天室、載入動畫、已讀標記、訊息驗證。
/// The chat-related endpoints of <see cref="LineMessagingClient"/>: the follower list, groups and rooms, the
/// loading animation, read marks, and message validation.
/// </summary>
public sealed partial class LineMessagingClient
{
    /// <inheritdoc />
    public async Task<Result<LineUserIdsPage>> GetFollowerIdsAsync(
        string? start = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        if (!Options.IsConfigured)
        {
            return NotConfigured<LineUserIdsPage>();
        }

        if (limit is { } size && size is < 1 or > LineMessagingLimits.MaxFollowerIdsPerPage)
        {
            return Error.Validation(
                LineErrorCodes.InvalidPageSize,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"好友清單的每頁筆數需為 1 到 {LineMessagingLimits.MaxFollowerIdsPerPage},這次是 {size}。The follower list's page size must be between 1 and {LineMessagingLimits.MaxFollowerIdsPerPage}; {size} was supplied."));
        }

        var query = new List<string>(2);
        if (limit is { } limitValue)
        {
            query.Add("limit=" + limitValue.ToString(CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(start))
        {
            query.Add("start=" + Uri.EscapeDataString(start));
        }

        var uri = query.Count == 0 ? LineEndpoints.FollowerIds : LineEndpoints.FollowerIds + "?" + string.Join('&', query);
        var page = await LineHttp
            .SendForJsonAsync<LineFollowerIdsResponse>(_http, Get(uri), cancellationToken)
            .ConfigureAwait(false);

        return page.IsFailure
            ? page.ToFailure<LineUserIdsPage>()
            : Result.Success(new LineUserIdsPage
            {
                UserIds = page.GetValueOrThrow().UserIds ?? [],
                Next = page.GetValueOrThrow().Next,
            });
    }

    /// <inheritdoc />
    public Task<Result<LineGroupSummary>> GetGroupSummaryAsync(string groupId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);

        return !Options.IsConfigured
            ? Task.FromResult(NotConfigured<LineGroupSummary>())
            : LineHttp.SendForJsonAsync<LineGroupSummary>(
                _http,
                Get($"{LineEndpoints.GroupBase}{Uri.EscapeDataString(groupId)}/summary"),
                cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<int>> GetGroupMemberCountAsync(string groupId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);

        return ReadMemberCountAsync($"{LineEndpoints.GroupBase}{Uri.EscapeDataString(groupId)}/members/count", cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<LineUserIdsPage>> GetGroupMemberIdsAsync(
        string groupId,
        string? start = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);

        return ReadMemberIdsAsync($"{LineEndpoints.GroupBase}{Uri.EscapeDataString(groupId)}/members/ids", start, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result> LeaveGroupAsync(string groupId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);

        return PostWithoutBodyAsync($"{LineEndpoints.GroupBase}{Uri.EscapeDataString(groupId)}/leave", cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<LineUserProfile>> GetGroupMemberProfileAsync(
        string groupId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        return ReadMemberProfileAsync(
            $"{LineEndpoints.GroupBase}{Uri.EscapeDataString(groupId)}/member/{Uri.EscapeDataString(userId)}",
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<int>> GetRoomMemberCountAsync(string roomId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        return ReadMemberCountAsync($"{LineEndpoints.RoomBase}{Uri.EscapeDataString(roomId)}/members/count", cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<LineUserIdsPage>> GetRoomMemberIdsAsync(
        string roomId,
        string? start = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        return ReadMemberIdsAsync($"{LineEndpoints.RoomBase}{Uri.EscapeDataString(roomId)}/members/ids", start, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result> LeaveRoomAsync(string roomId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        return PostWithoutBodyAsync($"{LineEndpoints.RoomBase}{Uri.EscapeDataString(roomId)}/leave", cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<LineUserProfile>> GetRoomMemberProfileAsync(
        string roomId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        return ReadMemberProfileAsync(
            $"{LineEndpoints.RoomBase}{Uri.EscapeDataString(roomId)}/member/{Uri.EscapeDataString(userId)}",
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result> StartLoadingAnimationAsync(
        string chatId,
        int? loadingSeconds = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chatId);

        if (!Options.IsConfigured)
        {
            return Task.FromResult(Result.Failure(NotConfiguredError()));
        }

        // 秒數只收 5、10、…、60。LINE 對不合規的值回一個沒指名欄位的 400,在本地擋下才說得出是哪個值不對。
        // Only 5, 10, …, 60 are accepted. LINE answers a non-conforming value with a 400 that names no field;
        // stopping it here is what lets the message say which value was wrong.
        if (loadingSeconds is { } seconds
            && (seconds is < LineMessagingLimits.MinLoadingSeconds or > LineMessagingLimits.MaxLoadingSeconds
                || seconds % LineMessagingLimits.LoadingSecondsStep != 0))
        {
            return Task.FromResult(Result.Failure(Error.Validation(
                LineErrorCodes.InvalidLoadingSeconds,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"載入動畫的秒數需為 {LineMessagingLimits.MinLoadingSeconds} 到 {LineMessagingLimits.MaxLoadingSeconds} 之間、{LineMessagingLimits.LoadingSecondsStep} 的倍數,這次是 {seconds}。The loading animation's seconds must be a multiple of {LineMessagingLimits.LoadingSecondsStep} between {LineMessagingLimits.MinLoadingSeconds} and {LineMessagingLimits.MaxLoadingSeconds}; {seconds} was supplied."))));
        }

        var request = Post(LineEndpoints.ChatLoadingStart, options: null, writer =>
        {
            writer.WriteString("chatId", chatId);

            if (loadingSeconds is { } value)
            {
                writer.WriteNumber("loadingSeconds", value);
            }
        });

        return LineHttp.SendAsync(_http, request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result> MarkAsReadAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        if (!Options.IsConfigured)
        {
            return Task.FromResult(Result.Failure(NotConfiguredError()));
        }

        // LINE 的形狀是巢狀的 chat.userId,不是平的 userId。
        // LINE's shape is the nested chat.userId, not a flat userId.
        var request = Post(LineEndpoints.MarkAsRead, options: null, writer =>
        {
            writer.WriteStartObject("chat");
            writer.WriteString("userId", userId);
            writer.WriteEndObject();
        });

        return LineHttp.SendAsync(_http, request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result> ValidateMessagesAsync(
        LineMessageValidationTarget target,
        IReadOnlyList<LineMessage> messages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        if (!Options.IsConfigured)
        {
            return Task.FromResult(Result.Failure(NotConfiguredError()));
        }

        // 本地能擋的先擋:LINE 那頭對超量的回答只有一句「請求內容有錯」,本地的訊息才說得出實際數量。
        // Whatever can be stopped locally is stopped first: LINE's answer to going over is one sentence saying
        // the body is wrong, and only the local message can say the actual count.
        var local = ValidateMessages(messages);
        if (local.IsFailure)
        {
            return Task.FromResult(local);
        }

        var request = Post(ValidationEndpoint(target), options: null, writer =>
        {
            writer.WritePropertyName("messages");
            LineMessageSerializer.WriteMessages(writer, messages);
        });

        return LineHttp.SendAsync(_http, request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result> ValidateMessagesRawJsonAsync(
        LineMessageValidationTarget target,
        string messagesJson,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messagesJson);

        if (!Options.IsConfigured)
        {
            return Result.Failure(NotConfiguredError());
        }

        var parsed = ParseRawMessages(messagesJson);
        if (parsed.IsFailure)
        {
            return parsed.ToResult();
        }

        using var document = parsed.GetValueOrThrow();
        var request = Post(ValidationEndpoint(target), options: null, writer => WriteRawMessages(writer, document.RootElement));

        return await LineHttp.SendAsync(_http, request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 訊息驗證端點的完整位址。
    /// The full address of a message validation endpoint.
    /// </summary>
    /// <param name="target">要模擬的送出方式。The kind of send to simulate.</param>
    /// <returns>位址。The address.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="target"/> 不是已定義的值時擲出。Thrown when <paramref name="target"/> is not a defined value.
    /// </exception>
    private static string ValidationEndpoint(LineMessageValidationTarget target) =>
        LineEndpoints.ValidateMessageBase + target switch
        {
            LineMessageValidationTarget.Push => "push",
            LineMessageValidationTarget.Multicast => "multicast",
            LineMessageValidationTarget.Broadcast => "broadcast",
            LineMessageValidationTarget.Reply => "reply",
            LineMessageValidationTarget.Narrowcast => "narrowcast",
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "不是已定義的驗證目標。Not a defined validation target."),
        };

    /// <summary>
    /// 讀 <c>{ "count": n }</c> 形狀的成員人數。
    /// Reads a member count of the shape <c>{ "count": n }</c>.
    /// </summary>
    /// <param name="uri">查詢位址。The address to read.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>人數。The count.</returns>
    private async Task<Result<int>> ReadMemberCountAsync(string uri, CancellationToken cancellationToken)
    {
        if (!Options.IsConfigured)
        {
            return NotConfigured<int>();
        }

        var count = await LineHttp
            .SendForJsonAsync<LineMemberCountResponse>(_http, Get(uri), cancellationToken)
            .ConfigureAwait(false);

        return count.IsFailure ? count.ToFailure<int>() : Result.Success(count.GetValueOrThrow().Count);
    }

    /// <summary>
    /// 讀 <c>{ "memberIds": [...], "next": ... }</c> 形狀的成員清單。
    /// Reads a member list of the shape <c>{ "memberIds": [...], "next": ... }</c>.
    /// </summary>
    /// <param name="uri">查詢位址(不含 query)。The address to read, without the query.</param>
    /// <param name="start">分頁游標。The paging cursor.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>一頁識別碼。One page of identifiers.</returns>
    private async Task<Result<LineUserIdsPage>> ReadMemberIdsAsync(string uri, string? start, CancellationToken cancellationToken)
    {
        if (!Options.IsConfigured)
        {
            return NotConfigured<LineUserIdsPage>();
        }

        if (!string.IsNullOrWhiteSpace(start))
        {
            uri = uri + "?start=" + Uri.EscapeDataString(start);
        }

        var page = await LineHttp
            .SendForJsonAsync<LineMemberIdsResponse>(_http, Get(uri), cancellationToken)
            .ConfigureAwait(false);

        return page.IsFailure
            ? page.ToFailure<LineUserIdsPage>()
            : Result.Success(new LineUserIdsPage
            {
                UserIds = page.GetValueOrThrow().MemberIds ?? [],
                Next = page.GetValueOrThrow().Next,
            });
    }

    /// <summary>
    /// 讀群組或聊天室成員的個人檔案。
    /// Reads a group or room member's profile.
    /// </summary>
    /// <param name="uri">查詢位址。The address to read.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>個人檔案。The profile.</returns>
    private Task<Result<LineUserProfile>> ReadMemberProfileAsync(string uri, CancellationToken cancellationToken) =>
        !Options.IsConfigured
            ? Task.FromResult(NotConfigured<LineUserProfile>())
            : LineHttp.SendForJsonAsync<LineUserProfile>(_http, Get(uri), cancellationToken);

    /// <summary>
    /// 送一個沒有內容的 POST(離開群組 / 聊天室)。
    /// Sends a bodiless POST (leaving a group or room).
    /// </summary>
    /// <param name="uri">目標位址。The target address.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>成功或失敗。Success or failure.</returns>
    private Task<Result> PostWithoutBodyAsync(string uri, CancellationToken cancellationToken) =>
        !Options.IsConfigured
            ? Task.FromResult(Result.Failure(NotConfiguredError()))
            : LineHttp.SendAsync(
                _http,
                Authorize(new HttpRequestMessage(HttpMethod.Post, new Uri(uri, UriKind.Absolute))),
                cancellationToken);
}
