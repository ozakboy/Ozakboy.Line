using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Messaging.RichMenu;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// <see cref="ILineMessagingClient"/> 的圖文選單批次作業部分。
/// The rich menu batch part of <see cref="ILineMessagingClient"/>.
/// </summary>
public partial interface ILineMessagingClient
{
    /// <summary>
    /// 批次替換或解除圖文選單的連結(非同步)。
    /// Replaces or unlinks rich menus in batch (asynchronous).
    /// </summary>
    /// <param name="operations">作業,1 到 1000 個,依序執行。The operations, between 1 and 1000, run in order.</param>
    /// <param name="resumeRequestKey">
    /// 續跑用的請求鍵:前一次請求的識別碼,LINE 會接著上次中斷處繼續;不是續跑時留空。
    /// The resume key: the request id of an earlier request, so LINE continues where that one stopped; leave it
    /// empty when not resuming.
    /// </param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>
    /// 請求識別碼(<c>X-Line-Request-Id</c>),之後用 <see cref="GetRichMenuBatchProgressAsync"/> 查進度;
    /// 回應沒有該標頭時以 <see cref="LineErrorCodes.ApiInvalidResponse"/> 失敗。作業數不合法時以
    /// <see cref="LineErrorCodes.InvalidRichMenuBatch"/> 失敗且不送出。
    /// The request id (<c>X-Line-Request-Id</c>), to be passed to <see cref="GetRichMenuBatchProgressAsync"/>; a
    /// response without that header fails with <see cref="LineErrorCodes.ApiInvalidResponse"/>. An invalid
    /// operation count fails with <see cref="LineErrorCodes.InvalidRichMenuBatch"/> without sending anything.
    /// </returns>
    Task<Result<string>> ReplaceOrUnlinkRichMenusInBatchAsync(
        IReadOnlyList<LineRichMenuBatchOperation> operations,
        string? resumeRequestKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 驗證批次作業的內容但不執行。
    /// Validates a batch's contents without running it.
    /// </summary>
    /// <param name="operations">作業,1 到 1000 個。The operations, between 1 and 1000.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>內容可接受時為成功。Success when the contents are acceptable.</returns>
    Task<Result> ValidateRichMenuBatchAsync(
        IReadOnlyList<LineRichMenuBatchOperation> operations,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 查批次作業的進度。
    /// Gets a batch's progress.
    /// </summary>
    /// <param name="requestId"><see cref="ReplaceOrUnlinkRichMenusInBatchAsync"/> 回的請求識別碼。The request id from <see cref="ReplaceOrUnlinkRichMenusInBatchAsync"/>.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>進度。The progress.</returns>
    Task<Result<LineRichMenuBatchProgress>> GetRichMenuBatchProgressAsync(string requestId, CancellationToken cancellationToken = default);
}
