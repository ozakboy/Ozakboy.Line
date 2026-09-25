using System.Globalization;
using System.Text.Json;
using Ozakboy.Core.Abstractions;
using Ozakboy.Http;
using Ozakboy.Line.Messaging.Audience;
using Ozakboy.Line.Messaging.Messages;
using Ozakboy.Line.Messaging.Narrowcast;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// <see cref="LineMessagingClient"/> 的分眾推播與受眾端點。
/// The narrowcast and audience endpoints of <see cref="LineMessagingClient"/>.
/// </summary>
public sealed partial class LineMessagingClient
{
    /// <summary>
    /// 分眾推播回應裡帶請求識別碼的標頭。
    /// The response header carrying a narrowcast's request id.
    /// </summary>
    private const string RequestIdHeader = "X-Line-Request-Id";

    /// <inheritdoc />
    public async Task<Result<string>> NarrowcastAsync(
        IReadOnlyList<LineMessage> messages,
        LineNarrowcastOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        if (!Options.IsConfigured)
        {
            return NotConfigured<string>();
        }

        var count = ValidateMessages(messages);
        if (count.IsFailure)
        {
            return count.ToFailure<string>();
        }

        // 這一條走 HttpPipelineClient.SendAsync 而不是 LineHttp 的包裝:請求識別碼在回應標頭而不在內容裡,
        // SendForStringAsync 只給內容。請求的擁有權因此留在這裡,要自己釋放。
        // This path calls HttpPipelineClient.SendAsync directly rather than LineHttp's wrapper: the request id is
        // in a response header, not the body, and SendForStringAsync yields only the body. Ownership of the
        // request therefore stays here and it is disposed here.
        using var request = JsonRequest(HttpMethod.Post, LineEndpoints.NarrowcastMessage, options?.RetryKey, writer =>
        {
            writer.WritePropertyName("messages");
            LineMessageSerializer.WriteMessages(writer, messages);
            WriteNarrowcastOptions(writer, options);
        });

        var sent = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (sent.IsFailure)
        {
            return Result.Failure<string>(LineApiErrorMapper.Map(sent.Error));
        }

        using var response = sent.GetValueOrThrow();
        if (!response.IsSuccessStatusCode)
        {
            var error = await HttpErrorMapper
                .FromResponseAsync(response, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return Result.Failure<string>(LineApiErrorMapper.Map(error));
        }

        var requestId = response.Headers.TryGetValues(RequestIdHeader, out var values) ? values.FirstOrDefault() : null;
        return string.IsNullOrWhiteSpace(requestId)
            ? Error.Internal(
                LineErrorCodes.ApiInvalidResponse,
                "分眾推播的回應沒有 X-Line-Request-Id 標頭。The narrowcast response carried no X-Line-Request-Id header.")
            : Result.Success(requestId);
    }

    /// <inheritdoc />
    public Task<Result<LineNarrowcastProgress>> GetNarrowcastProgressAsync(string requestId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);

        return !Options.IsConfigured
            ? Task.FromResult(NotConfigured<LineNarrowcastProgress>())
            : LineHttp.SendForJsonAsync<LineNarrowcastProgress>(
                _http,
                Get(LineEndpoints.NarrowcastProgress + "?requestId=" + Uri.EscapeDataString(requestId)),
                cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<LineAudienceGroupCreated>> CreateUploadAudienceGroupAsync(
        string description,
        IReadOnlyList<string> userIds,
        string? uploadDescription = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(userIds);

        if (!Options.IsConfigured)
        {
            return Task.FromResult(NotConfigured<LineAudienceGroupCreated>());
        }

        // 建立時允許零人:先建空受眾再分批加成員是 LINE 文件建議的用法。
        // Zero members are allowed on creation: creating an empty audience and filling it in batches is what
        // LINE's documentation suggests.
        var members = ValidateAudienceMembers(userIds.Count, allowEmpty: true);
        if (members.IsFailure)
        {
            return Task.FromResult(members.ToFailure<LineAudienceGroupCreated>());
        }

        var request = JsonRequest(HttpMethod.Post, LineEndpoints.AudienceGroupUpload, retryKey: null, writer =>
        {
            writer.WriteString("description", description);

            if (!string.IsNullOrWhiteSpace(uploadDescription))
            {
                writer.WriteString("uploadDescription", uploadDescription);
            }

            WriteAudiences(writer, userIds);
        });

        return LineHttp.SendForJsonAsync<LineAudienceGroupCreated>(_http, request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result> AddAudienceGroupMembersAsync(
        long audienceGroupId,
        IReadOnlyList<string> userIds,
        string? uploadDescription = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        if (!Options.IsConfigured)
        {
            return Task.FromResult(Result.Failure(NotConfiguredError()));
        }

        var members = ValidateAudienceMembers(userIds.Count, allowEmpty: false);
        if (members.IsFailure)
        {
            return Task.FromResult(members);
        }

        // 加成員是 PUT 到同一個 upload 端點,不是 POST 到 /{id}/members。
        // Adding members is a PUT to the same upload endpoint, not a POST to /{id}/members.
        var request = JsonRequest(HttpMethod.Put, LineEndpoints.AudienceGroupUpload, retryKey: null, writer =>
        {
            writer.WriteNumber("audienceGroupId", audienceGroupId);

            if (!string.IsNullOrWhiteSpace(uploadDescription))
            {
                writer.WriteString("uploadDescription", uploadDescription);
            }

            WriteAudiences(writer, userIds);
        });

        return LineHttp.SendAsync(_http, request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<LineAudienceGroupDetail>> GetAudienceGroupAsync(long audienceGroupId, CancellationToken cancellationToken = default) =>
        !Options.IsConfigured
            ? Task.FromResult(NotConfigured<LineAudienceGroupDetail>())
            : LineHttp.SendForJsonAsync<LineAudienceGroupDetail>(
                _http,
                Get(LineEndpoints.AudienceGroupBase + audienceGroupId.ToString(CultureInfo.InvariantCulture)),
                cancellationToken);

    /// <inheritdoc />
    public Task<Result<LineAudienceGroupPage>> GetAudienceGroupListAsync(
        int page = 1,
        int size = 20,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (!Options.IsConfigured)
        {
            return Task.FromResult(NotConfigured<LineAudienceGroupPage>());
        }

        if (page < 1 || size is < 1 or > LineMessagingLimits.MaxAudienceGroupsPerPage)
        {
            return Task.FromResult(Result.Failure<LineAudienceGroupPage>(Error.Validation(
                LineErrorCodes.InvalidPageSize,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"受眾清單的頁碼需從 1 起算、每頁筆數需為 1 到 {LineMessagingLimits.MaxAudienceGroupsPerPage},這次是第 {page} 頁、每頁 {size} 筆。The audience list's page starts at 1 and its size must be between 1 and {LineMessagingLimits.MaxAudienceGroupsPerPage}; page {page} and size {size} were supplied."))));
        }

        var uri = string.Create(CultureInfo.InvariantCulture, $"{LineEndpoints.AudienceGroupList}?page={page}&size={size}");
        if (!string.IsNullOrWhiteSpace(description))
        {
            uri = uri + "&description=" + Uri.EscapeDataString(description);
        }

        return LineHttp.SendForJsonAsync<LineAudienceGroupPage>(_http, Get(uri), cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result> DeleteAudienceGroupAsync(long audienceGroupId, CancellationToken cancellationToken = default) =>
        !Options.IsConfigured
            ? Task.FromResult(Result.Failure(NotConfiguredError()))
            : LineHttp.SendAsync(
                _http,
                Authorize(new HttpRequestMessage(
                    HttpMethod.Delete,
                    new Uri(LineEndpoints.AudienceGroupBase + audienceGroupId.ToString(CultureInfo.InvariantCulture), UriKind.Absolute))),
                cancellationToken);

    /// <summary>
    /// 寫出分眾推播的可選欄位:<c>recipient</c>、<c>filter.demographic</c>、<c>limit</c>、<c>notificationDisabled</c>。
    /// Writes a narrowcast's optional fields: <c>recipient</c>, <c>filter.demographic</c>, <c>limit</c>,
    /// <c>notificationDisabled</c>.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    /// <param name="options">可選參數。The optional parameters.</param>
    private static void WriteNarrowcastOptions(Utf8JsonWriter writer, LineNarrowcastOptions? options)
    {
        if (options is null)
        {
            return;
        }

        if (options.Recipient is { ValueKind: JsonValueKind.Object } recipient)
        {
            writer.WritePropertyName("recipient");
            recipient.WriteTo(writer);
        }

        if (options.Demographic is { ValueKind: JsonValueKind.Object } demographic)
        {
            writer.WriteStartObject("filter");
            writer.WritePropertyName("demographic");
            demographic.WriteTo(writer);
            writer.WriteEndObject();
        }

        if (options.MaxRecipients is not null || options.UpToRemainingQuota is not null)
        {
            writer.WriteStartObject("limit");

            if (options.MaxRecipients is { } max)
            {
                writer.WriteNumber("max", max);
            }

            if (options.UpToRemainingQuota is { } upToRemainingQuota)
            {
                writer.WriteBoolean("upToRemainingQuota", upToRemainingQuota);
            }

            writer.WriteEndObject();
        }

        if (options.NotificationDisabled)
        {
            writer.WriteBoolean("notificationDisabled", true);
        }
    }

    /// <summary>
    /// 寫出 <c>audiences: [{ "id": … }]</c>。
    /// Writes <c>audiences: [{ "id": … }]</c>.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    /// <param name="userIds">使用者識別碼。The user identifiers.</param>
    /// <remarks>
    /// 每個成員是一個 <c>{ "id": … }</c> 物件,不是裸字串 —— 與 multicast 的 <c>to</c> 陣列不同。
    /// Each member is an <c>{ "id": … }</c> object rather than a bare string, unlike a multicast's <c>to</c>
    /// array.
    /// </remarks>
    private static void WriteAudiences(Utf8JsonWriter writer, IReadOnlyList<string> userIds)
    {
        if (userIds.Count == 0)
        {
            return;
        }

        writer.WriteStartArray("audiences");
        for (var index = 0; index < userIds.Count; index++)
        {
            writer.WriteStartObject();
            writer.WriteString("id", userIds[index]);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    /// <summary>
    /// 檢查一次加入受眾的人數。
    /// Checks the number of users added to an audience in one request.
    /// </summary>
    /// <param name="count">人數。The count.</param>
    /// <param name="allowEmpty">是否允許零人。Whether zero is allowed.</param>
    /// <returns>在允許範圍內時為成功。Success when within the allowed range.</returns>
    private static Result ValidateAudienceMembers(int count, bool allowEmpty) =>
        count <= LineMessagingLimits.MaxAudienceMembersPerRequest && (allowEmpty || count > 0)
            ? Result.Success()
            : Error.Validation(
                LineErrorCodes.TooManyAudienceMembers,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"一次加入受眾的人數需為 {(allowEmpty ? 0 : 1)} 到 {LineMessagingLimits.MaxAudienceMembersPerRequest} 位,這次是 {count} 位。One request adds between {(allowEmpty ? 0 : 1)} and {LineMessagingLimits.MaxAudienceMembersPerRequest} users to an audience; {count} were supplied."));
}
