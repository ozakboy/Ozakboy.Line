using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Liff;

namespace Ozakboy.Line.Mcp.Tests.TestSupport;

/// <summary>
/// 記錄被呼叫的內容並回傳安排好結果的 <see cref="ILineLiffClient"/>。
/// An <see cref="ILineLiffClient"/> that records what it was asked and answers with arranged results.
/// </summary>
/// <remarks>
/// 沒有安排行為的路徑一律擲例外,而不是安靜地回成功 —— 與 <c>FakeLineMessagingClient</c> 同一個原則。
/// A path without arranged behaviour throws rather than quietly answering success, the same principle as
/// <c>FakeLineMessagingClient</c>.
/// </remarks>
internal sealed class FakeLineLiffClient : ILineLiffClient
{
    /// <summary>
    /// 每一次 <see cref="AddAsync"/> 收到的應用程式。
    /// The app every <see cref="AddAsync"/> call received.
    /// </summary>
    internal List<LineLiffApp> Added { get; } = [];

    /// <summary>
    /// 每一次 <see cref="DeleteAsync"/> 收到的識別碼。
    /// The identifier every <see cref="DeleteAsync"/> call received.
    /// </summary>
    internal List<string> Deleted { get; } = [];

    /// <summary>
    /// <see cref="AddAsync"/> 成功時回的 liffId。
    /// The liffId <see cref="AddAsync"/> answers with on success.
    /// </summary>
    internal string AddedLiffId { get; set; } = "1234567890-AbCdEfGh";

    /// <summary>
    /// <see cref="GetAllAsync"/> 要回的清單。
    /// The list <see cref="GetAllAsync"/> answers with.
    /// </summary>
    internal List<LineLiffApp> Apps { get; } = [];

    /// <summary>
    /// 三個方法共用要回的失敗;為 <see langword="null"/> 時一律成功。
    /// The failure all methods answer with, or <see langword="null"/> for success.
    /// </summary>
    internal Error? Failure { get; set; }

    /// <inheritdoc />
    public Task<Result<string>> AddAsync(LineLiffApp app, CancellationToken cancellationToken = default)
    {
        Added.Add(app);
        return Task.FromResult(Failure is null ? Result.Success(AddedLiffId) : Result.Failure<string>(Failure));
    }

    /// <inheritdoc />
    public Task<Result> UpdateAsync(string liffId, LineLiffApp app, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("這個方法在假用戶端上沒有安排行為。This method has no arranged behaviour on the fake client.");

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<LineLiffApp>>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Failure is null
            ? Result.Success<IReadOnlyList<LineLiffApp>>(Apps)
            : Result.Failure<IReadOnlyList<LineLiffApp>>(Failure));

    /// <inheritdoc />
    public Task<Result> DeleteAsync(string liffId, CancellationToken cancellationToken = default)
    {
        Deleted.Add(liffId);
        return Task.FromResult(Failure is null ? Result.Success() : Result.Failure(Failure));
    }
}
