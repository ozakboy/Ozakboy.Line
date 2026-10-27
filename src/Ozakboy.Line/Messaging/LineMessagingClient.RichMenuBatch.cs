using System.Globalization;
using System.Text.Json;
using Ozakboy.Core.Abstractions;
using Ozakboy.Http;
using Ozakboy.Line.Messaging.RichMenu;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// <see cref="LineMessagingClient"/> 的圖文選單批次作業端點。
/// The rich menu batch endpoints of <see cref="LineMessagingClient"/>.
/// </summary>
public sealed partial class LineMessagingClient
{
    /// <inheritdoc />
    public async Task<Result<string>> ReplaceOrUnlinkRichMenusInBatchAsync(
        IReadOnlyList<LineRichMenuBatchOperation> operations,
        string? resumeRequestKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operations);

        if (!Options.IsConfigured)
        {
            return NotConfigured<string>();
        }

        var valid = CheckRichMenuBatch(operations);
        if (valid.IsFailure)
        {
            return valid.ToFailure<string>();
        }

        // 請求識別碼在回應標頭而不在內容裡,所以和 NarrowcastAsync 一樣直接走 HttpPipelineClient.SendAsync,
        // 請求的擁有權留在這裡。
        // The request id is in a response header, not the body, so like NarrowcastAsync this calls
        // HttpPipelineClient.SendAsync directly and the request stays owned, and disposed, here.
        using var request = JsonRequest(HttpMethod.Post, LineEndpoints.RichMenuBatch, retryKey: null, writer =>
        {
            WriteBatchOperations(writer, operations);

            if (!string.IsNullOrWhiteSpace(resumeRequestKey))
            {
                writer.WriteString("resumeRequestKey", resumeRequestKey);
            }
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
                "圖文選單批次作業的回應沒有 X-Line-Request-Id 標頭。The rich menu batch response carried no X-Line-Request-Id header.")
            : Result.Success(requestId);
    }

    /// <inheritdoc />
    public Task<Result> ValidateRichMenuBatchAsync(
        IReadOnlyList<LineRichMenuBatchOperation> operations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operations);

        if (!Options.IsConfigured)
        {
            return Task.FromResult(Result.Failure(NotConfiguredError()));
        }

        var valid = CheckRichMenuBatch(operations);
        if (valid.IsFailure)
        {
            return Task.FromResult(valid);
        }

        var request = JsonRequest(HttpMethod.Post, LineEndpoints.RichMenuValidateBatch, retryKey: null, writer =>
            WriteBatchOperations(writer, operations));

        return LineHttp.SendAsync(_http, request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<LineRichMenuBatchProgress>> GetRichMenuBatchProgressAsync(string requestId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);

        return !Options.IsConfigured
            ? Task.FromResult(NotConfigured<LineRichMenuBatchProgress>())
            : LineHttp.SendForJsonAsync<LineRichMenuBatchProgress>(
                _http,
                Get(LineEndpoints.RichMenuBatchProgress + "?requestId=" + Uri.EscapeDataString(requestId)),
                cancellationToken);
    }

    /// <summary>
    /// 檢查批次作業的數量,以及其中沒有空值。
    /// Checks the number of operations in a batch and that none of them is null.
    /// </summary>
    /// <param name="operations">作業。The operations.</param>
    /// <returns>合法時為成功。Success when valid.</returns>
    private static Result CheckRichMenuBatch(IReadOnlyList<LineRichMenuBatchOperation> operations)
    {
        if (operations.Count is 0 or > LineMessagingLimits.MaxRichMenuBatchOperations)
        {
            return Error.Validation(
                LineErrorCodes.InvalidRichMenuBatch,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"圖文選單批次作業需為 1 到 {LineMessagingLimits.MaxRichMenuBatchOperations} 個,這次是 {operations.Count} 個。A rich menu batch takes between 1 and {LineMessagingLimits.MaxRichMenuBatchOperations} operations; {operations.Count} were supplied."));
        }

        for (var index = 0; index < operations.Count; index++)
        {
            if (operations[index] is null)
            {
                return Error.Validation(
                    LineErrorCodes.InvalidRichMenuBatch,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"圖文選單批次作業的第 {index} 個為空值。Operation {index} of the rich menu batch is null."));
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// 寫出 <c>operations</c> 陣列。
    /// Writes the <c>operations</c> array.
    /// </summary>
    /// <param name="writer">JSON 寫入器。The JSON writer.</param>
    /// <param name="operations">作業。The operations.</param>
    private static void WriteBatchOperations(Utf8JsonWriter writer, IReadOnlyList<LineRichMenuBatchOperation> operations)
    {
        writer.WriteStartArray("operations");
        for (var index = 0; index < operations.Count; index++)
        {
            operations[index].WriteTo(writer);
        }

        writer.WriteEndArray();
    }
}
