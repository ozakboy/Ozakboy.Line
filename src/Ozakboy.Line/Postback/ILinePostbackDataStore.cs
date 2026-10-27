using Ozakboy.Core.Abstractions;

namespace Ozakboy.Line.Postback;

/// <summary>
/// 暫存超長 postback 資料的儲存體:把長資料存起來、只把短代碼放進按鈕。
/// A store staging over-long postback data: the long data is kept here and only a short token goes into the button.
/// </summary>
/// <remarks>
/// <para>
/// LINE 的 postback <c>data</c> 上限是 <see cref="LineMessagingLimits.MaxPostbackDataLength"/> 字。
/// 超過時,送出前把資料 <see cref="StoreAsync"/> 起來,按鈕只帶 <see cref="LinePostbackDataToken"/> 格式的代碼,
/// webhook 收到後再 <see cref="GetAsync"/> 換回原資料(或用 <see cref="LinePostbackDataResolver"/>)。
/// LINE caps a postback's <c>data</c> at <see cref="LineMessagingLimits.MaxPostbackDataLength"/> characters.
/// Beyond that, <see cref="StoreAsync"/> the data before sending, let the button carry only a token in the
/// <see cref="LinePostbackDataToken"/> format, and have the webhook swap it back with <see cref="GetAsync"/> (or
/// <see cref="LinePostbackDataResolver"/>).
/// </para>
/// <para>
/// 資料有存活時間(TTL):過了就查不到,按鈕也就失效 —— 這是這個做法的固有代價,UI 上要有「請重新操作」的出口。
/// 本套件附 <see cref="InMemoryLinePostbackDataStore"/> 與 <see cref="JsonFileLinePostbackDataStore"/>;
/// 有資料庫或快取的宿主自己實作這個介面即可。
/// The data has a time to live (TTL): past it nothing is found and the button goes dead — an inherent cost of
/// this approach, so the UI needs a "please try again" way out. Two implementations ship with the package,
/// <see cref="InMemoryLinePostbackDataStore"/> and <see cref="JsonFileLinePostbackDataStore"/>; a host with a
/// database or a cache implements this interface instead.
/// </para>
/// </remarks>
public interface ILinePostbackDataStore
{
    /// <summary>
    /// 暫存一段資料。
    /// Stages a piece of data.
    /// </summary>
    /// <param name="data">要暫存的資料。The data to stage.</param>
    /// <param name="timeToLive">存活時間;<see langword="null"/> 時用 <see cref="LinePostbackDataOptions.DefaultTimeToLive"/>。The time to live, or <see langword="null"/> for <see cref="LinePostbackDataOptions.DefaultTimeToLive"/>.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>
    /// 成功時為代碼(<see cref="LinePostbackDataToken"/> 格式);資料超過上限為
    /// <see cref="LineErrorCodes.PostbackDataTooLong"/> 失敗。
    /// The token on success (in the <see cref="LinePostbackDataToken"/> format); a
    /// <see cref="LineErrorCodes.PostbackDataTooLong"/> failure when the data exceeds the limit.
    /// </returns>
    Task<Result<string>> StoreAsync(string data, TimeSpan? timeToLive = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 用代碼取回資料。
    /// Gets the data back by token.
    /// </summary>
    /// <param name="token">代碼。The token.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>
    /// 成功時為原資料;找不到或已過期為 <see cref="LineErrorCodes.PostbackExpired"/> 失敗(不分兩者)。
    /// The original data on success; a <see cref="LineErrorCodes.PostbackExpired"/> failure when it is missing or
    /// expired, the two not told apart.
    /// </returns>
    Task<Result<string>> GetAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清掉所有已過期的項目。
    /// Removes every expired entry.
    /// </summary>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>清掉幾筆。How many entries were removed.</returns>
    Task<Result<int>> PurgeExpiredAsync(CancellationToken cancellationToken = default);
}
