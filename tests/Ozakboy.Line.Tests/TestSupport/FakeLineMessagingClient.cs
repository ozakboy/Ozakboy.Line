using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Messaging;
using Ozakboy.Line.Messaging.Audience;
using Ozakboy.Line.Messaging.Insight;
using Ozakboy.Line.Messaging.Messages;
using Ozakboy.Line.Messaging.Narrowcast;
using Ozakboy.Line.Messaging.RichMenu;

namespace Ozakboy.Line.Tests.TestSupport;

/// <summary>
/// 記錄呼叫內容、回傳事先安排好結果的 <see cref="ILineMessagingClient"/>。
/// An <see cref="ILineMessagingClient"/> that records what it was asked to do and answers with arranged results.
/// </summary>
/// <remarks>
/// 自動回覆與 MCP 工具要驗的是「有沒有送、送了什麼」,而不是 HTTP 那一層 ——
/// 那一層已經由 <c>LineMessagingClientTests</c> 以假處理器驗過了。
/// 沒有安排過的方法一律擲 <see cref="NotSupportedException"/>:安靜地回成功會讓測試在錯的地方通過。
/// What auto reply and the MCP tools need verified is whether something was sent and what it was, not the HTTP
/// layer — that is already covered by <c>LineMessagingClientTests</c> and its fake handler. Anything not
/// arranged throws <see cref="NotSupportedException"/>: quietly answering success would let a test pass for the
/// wrong reason.
///
/// 這個檔案同時被 <c>Ozakboy.Line.Mcp.Tests</c> 以連結方式編譯,兩邊共用同一份實作。
/// This file is also compiled into <c>Ozakboy.Line.Mcp.Tests</c> as a link, so both projects share one
/// implementation.
/// </remarks>
internal sealed class FakeLineMessagingClient : ILineMessagingClient
{
    /// <summary>
    /// 每一次 <see cref="ReplyRawJsonAsync"/> 的參數。
    /// The arguments of every <see cref="ReplyRawJsonAsync"/> call.
    /// </summary>
    internal List<(string ReplyToken, string MessagesJson)> Replies { get; } = [];

    /// <summary>
    /// 每一次 <see cref="PushRawJsonAsync"/> 的參數。
    /// The arguments of every <see cref="PushRawJsonAsync"/> call.
    /// </summary>
    internal List<(string To, string MessagesJson)> Pushes { get; } = [];

    /// <summary>
    /// 每一次 <see cref="MulticastAsync"/> 的參數。
    /// The arguments of every <see cref="MulticastAsync"/> call.
    /// </summary>
    internal List<(IReadOnlyList<string> To, int MessageCount)> Multicasts { get; } = [];

    /// <summary>
    /// 每一次 <see cref="BroadcastRawJsonAsync"/> 的參數。
    /// The arguments of every <see cref="BroadcastRawJsonAsync"/> call.
    /// </summary>
    internal List<string> Broadcasts { get; } = [];

    /// <summary>
    /// 每一次 <see cref="ReplaceRichMenuAsync"/> 的參數。
    /// The arguments of every <see cref="ReplaceRichMenuAsync"/> call.
    /// </summary>
    internal List<(LineRichMenu Menu, int ImageBytes, string ContentType, LineRichMenuReplaceOptions? Options)> Replacements { get; } = [];

    /// <summary>
    /// 送出類方法要回的失敗;為 <see langword="null"/> 時一律成功。
    /// The failure the sending methods answer with, or <see langword="null"/> for success.
    /// </summary>
    internal Error? SendFailure { get; set; }

    /// <summary>
    /// <see cref="GetProfileAsync"/> 要回的顯示名稱;為 <see langword="null"/> 時回 404 失敗。
    /// The display name <see cref="GetProfileAsync"/> answers with, or <see langword="null"/> for a 404 failure.
    /// </summary>
    internal string? ProfileDisplayName { get; set; }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<LineSentMessage>>> PushAsync(
        string to,
        IReadOnlyList<LineMessage> messages,
        LinePushOptions? options = null,
        CancellationToken cancellationToken = default) => throw NotArranged();

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<LineSentMessage>>> PushTextAsync(
        string to,
        string text,
        LinePushOptions? options = null,
        CancellationToken cancellationToken = default) => throw NotArranged();

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<LineSentMessage>>> PushRawJsonAsync(
        string to,
        string messagesJson,
        LinePushOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Pushes.Add((to, messagesJson));
        return Task.FromResult(SendFailure is null
            ? Result.Success<IReadOnlyList<LineSentMessage>>([])
            : Result.Failure<IReadOnlyList<LineSentMessage>>(SendFailure));
    }

    /// <inheritdoc />
    public Task<Result> MulticastAsync(
        IReadOnlyList<string> to,
        IReadOnlyList<LineMessage> messages,
        LinePushOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Multicasts.Add((to, messages.Count));
        return Task.FromResult(Sent());
    }

    /// <inheritdoc />
    public Task<Result> BroadcastAsync(
        IReadOnlyList<LineMessage> messages,
        LinePushOptions? options = null,
        CancellationToken cancellationToken = default) => throw NotArranged();

    /// <inheritdoc />
    public Task<Result> BroadcastTextAsync(
        string text,
        LinePushOptions? options = null,
        CancellationToken cancellationToken = default) => throw NotArranged();

    /// <inheritdoc />
    public Task<Result> BroadcastRawJsonAsync(
        string messagesJson,
        LinePushOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Broadcasts.Add(messagesJson);
        return Task.FromResult(Sent());
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<LineSentMessage>>> ReplyAsync(
        string replyToken,
        IReadOnlyList<LineMessage> messages,
        bool notificationDisabled = false,
        CancellationToken cancellationToken = default) => throw NotArranged();

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<LineSentMessage>>> ReplyTextAsync(
        string replyToken,
        string text,
        CancellationToken cancellationToken = default) => throw NotArranged();

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<LineSentMessage>>> ReplyRawJsonAsync(
        string replyToken,
        string messagesJson,
        CancellationToken cancellationToken = default)
    {
        Replies.Add((replyToken, messagesJson));
        return Task.FromResult(SendFailure is null
            ? Result.Success<IReadOnlyList<LineSentMessage>>([])
            : Result.Failure<IReadOnlyList<LineSentMessage>>(SendFailure));
    }

    /// <inheritdoc />
    public Task<Result<LineMessageQuota>> GetQuotaAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(new LineMessageQuota()));

    /// <inheritdoc />
    public Task<Result<long>> GetQuotaConsumptionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(0L));

    /// <inheritdoc />
    public Task<Result<LineBotInfo>> GetBotInfoAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(new LineBotInfo()));

    /// <inheritdoc />
    public Task<Result<LineUserProfile>> GetProfileAsync(string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ProfileDisplayName is null
            ? Result.Failure<LineUserProfile>(Error.NotFound(LineErrorCodes.ApiError, "查不到個人檔案。"))
            : Result.Success(new LineUserProfile { UserId = userId, DisplayName = ProfileDisplayName }));

    /// <inheritdoc />
    public Task<Result<LineContent>> GetMessageContentAsync(string messageId, CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result<string>> CreateRichMenuAsync(LineRichMenu richMenu, CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result> UploadRichMenuImageAsync(
        string richMenuId,
        byte[] image,
        string contentType,
        CancellationToken cancellationToken = default) => throw NotArranged();

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<LineRichMenuInfo>>> GetRichMenuListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success<IReadOnlyList<LineRichMenuInfo>>([]));

    /// <inheritdoc />
    public Task<Result<LineRichMenuInfo>> GetRichMenuAsync(string richMenuId, CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result> DeleteRichMenuAsync(string richMenuId, CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result> SetDefaultRichMenuAsync(string richMenuId, CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result> ClearDefaultRichMenuAsync(CancellationToken cancellationToken = default) => throw NotArranged();

    /// <inheritdoc />
    public Task<Result<string?>> GetDefaultRichMenuIdAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success<string?>("richmenu-default"));

    /// <inheritdoc />
    public Task<Result> LinkRichMenuToUserAsync(string userId, string richMenuId, CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result> UnlinkRichMenuFromUserAsync(string userId, CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result<string?>> GetRichMenuIdOfUserAsync(string userId, CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result<string>> ReplaceRichMenuAsync(
        LineRichMenu menu,
        byte[] image,
        string contentType,
        LineRichMenuReplaceOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Replacements.Add((menu, image.Length, contentType, options));
        return Task.FromResult(SendFailure is null
            ? Result.Success("richmenu-new")
            : Result.Failure<string>(SendFailure));
    }

    /// <inheritdoc />
    public Task<Result> CreateRichMenuAliasAsync(string aliasId, string richMenuId, CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result> UpdateRichMenuAliasAsync(string aliasId, string richMenuId, CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result> DeleteRichMenuAliasAsync(string aliasId, CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result<LineRichMenuAlias>> GetRichMenuAliasAsync(string aliasId, CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<LineRichMenuAlias>>> GetRichMenuAliasListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success<IReadOnlyList<LineRichMenuAlias>>([]));

    /// <inheritdoc />
    public Task<Result> LinkRichMenuToUsersAsync(
        IReadOnlyList<string> userIds,
        string richMenuId,
        CancellationToken cancellationToken = default) => throw NotArranged();

    /// <inheritdoc />
    public Task<Result> UnlinkRichMenuFromUsersAsync(IReadOnlyList<string> userIds, CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result> ValidateRichMenuAsync(LineRichMenu richMenu, CancellationToken cancellationToken = default) =>
        Task.FromResult(Sent());

    /// <inheritdoc />
    public Task<Result<LineUserIdsPage>> GetFollowerIdsAsync(string? start = null, int? limit = null, CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result<LineGroupSummary>> GetGroupSummaryAsync(string groupId, CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result<int>> GetGroupMemberCountAsync(string groupId, CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result<LineUserIdsPage>> GetGroupMemberIdsAsync(string groupId, string? start = null, CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result> LeaveGroupAsync(string groupId, CancellationToken cancellationToken = default) => throw NotArranged();

    /// <inheritdoc />
    public Task<Result<LineUserProfile>> GetGroupMemberProfileAsync(string groupId, string userId, CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result<int>> GetRoomMemberCountAsync(string roomId, CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result<LineUserIdsPage>> GetRoomMemberIdsAsync(string roomId, string? start = null, CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result> LeaveRoomAsync(string roomId, CancellationToken cancellationToken = default) => throw NotArranged();

    /// <inheritdoc />
    public Task<Result<LineUserProfile>> GetRoomMemberProfileAsync(string roomId, string userId, CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result> StartLoadingAnimationAsync(string chatId, int? loadingSeconds = null, CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result> MarkAsReadAsync(string userId, CancellationToken cancellationToken = default) => throw NotArranged();

    /// <inheritdoc />
    public Task<Result> ValidateMessagesAsync(
        LineMessageValidationTarget target,
        IReadOnlyList<LineMessage> messages,
        CancellationToken cancellationToken = default) => throw NotArranged();

    /// <inheritdoc />
    public Task<Result> ValidateMessagesRawJsonAsync(
        LineMessageValidationTarget target,
        string messagesJson,
        CancellationToken cancellationToken = default) => throw NotArranged();

    /// <inheritdoc />
    public Task<Result<string>> NarrowcastAsync(
        IReadOnlyList<LineMessage> messages,
        LineNarrowcastOptions? options = null,
        CancellationToken cancellationToken = default) => throw NotArranged();

    /// <inheritdoc />
    public Task<Result<LineNarrowcastProgress>> GetNarrowcastProgressAsync(string requestId, CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result<LineAudienceGroupCreated>> CreateUploadAudienceGroupAsync(
        string description,
        IReadOnlyList<string> userIds,
        string? uploadDescription = null,
        CancellationToken cancellationToken = default) => throw NotArranged();

    /// <inheritdoc />
    public Task<Result> AddAudienceGroupMembersAsync(
        long audienceGroupId,
        IReadOnlyList<string> userIds,
        string? uploadDescription = null,
        CancellationToken cancellationToken = default) => throw NotArranged();

    /// <inheritdoc />
    public Task<Result<LineAudienceGroupDetail>> GetAudienceGroupAsync(long audienceGroupId, CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result<LineAudienceGroupPage>> GetAudienceGroupListAsync(
        int page = 1,
        int size = 20,
        string? description = null,
        CancellationToken cancellationToken = default) => throw NotArranged();

    /// <inheritdoc />
    public Task<Result> DeleteAudienceGroupAsync(long audienceGroupId, CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result<LineMessageDeliveryInsight>> GetMessageDeliveryInsightAsync(DateOnly date, CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result<LineFollowersInsight>> GetFollowersInsightAsync(DateOnly date, CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <inheritdoc />
    public Task<Result<LineDemographicInsight>> GetDemographicInsightAsync(CancellationToken cancellationToken = default) =>
        throw NotArranged();

    /// <summary>
    /// 送出類方法的共用結果。
    /// The shared result of the sending methods.
    /// </summary>
    /// <returns>成功或安排好的失敗。Success, or the arranged failure.</returns>
    private Result Sent() => SendFailure is null ? Result.Success() : Result.Failure(SendFailure);

    /// <summary>
    /// 沒有安排過的方法被呼叫時擲出的例外。
    /// The exception thrown when an unarranged method is called.
    /// </summary>
    /// <returns>例外。The exception.</returns>
    private static NotSupportedException NotArranged() =>
        new("這個方法在假用戶端上沒有安排行為。要用它請先在這裡實作,不要讓它安靜地回成功。This method has no arranged behaviour on the fake client; implement it here rather than letting it quietly answer success.");
}
