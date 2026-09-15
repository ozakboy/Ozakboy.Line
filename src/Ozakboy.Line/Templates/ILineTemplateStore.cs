namespace Ozakboy.Line.Templates;

/// <summary>
/// 訊息範本的儲存體。
/// The store holding message templates.
/// </summary>
/// <remarks>
/// 本套件附兩個實作:<see cref="InMemoryLineTemplateStore"/>(重啟即失憶,適合測試與範例)與
/// <see cref="JsonFileLineTemplateStore"/>(一份 JSON 檔,適合單機或小型部署)。
/// 有資料庫的宿主自己實作這個介面就能接上去 —— 本套件不碰任何 ORM。
/// Two implementations ship with the package: <see cref="InMemoryLineTemplateStore"/>, which forgets everything
/// on restart and suits tests and samples, and <see cref="JsonFileLineTemplateStore"/>, one JSON file, which
/// suits a single machine or a small deployment. A host with a database implements this interface instead; the
/// package touches no ORM.
/// </remarks>
public interface ILineTemplateStore
{
    /// <summary>
    /// 列出所有範本。
    /// Lists every template.
    /// </summary>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>範本清單。The templates.</returns>
    Task<IReadOnlyList<LineMessageTemplate>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得單一範本。
    /// Gets one template.
    /// </summary>
    /// <param name="id">範本識別碼。The template identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>找不到時為 <see langword="null"/>。<see langword="null"/> when there is no such template.</returns>
    Task<LineMessageTemplate?> GetAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增或更新範本。
    /// Adds or updates a template.
    /// </summary>
    /// <param name="template">範本;<see cref="LineMessageTemplate.Id"/> 留空代表新增。The template; a blank <see cref="LineMessageTemplate.Id"/> means an insert.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>存好的範本,識別碼與時間戳記都已填上。The stored template, with its identifier and timestamps filled in.</returns>
    /// <remarks>
    /// 識別碼留空時由 store 產生,<see cref="LineMessageTemplate.UpdatedAt"/> 一律由 store 蓋上目前時間 ——
    /// 讓呼叫端自己填時間戳記,遲早會出現「更新時間比建立時間早」這種查不出來的資料。
    /// A blank identifier is filled in by the store, and <see cref="LineMessageTemplate.UpdatedAt"/> is always
    /// stamped by the store: letting callers set timestamps eventually produces rows whose update time precedes
    /// their creation time, with nothing to say how.
    /// </remarks>
    Task<LineMessageTemplate> UpsertAsync(LineMessageTemplate template, CancellationToken cancellationToken = default);

    /// <summary>
    /// 刪除範本。
    /// Deletes a template.
    /// </summary>
    /// <param name="id">範本識別碼。The template identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>
    /// 真的刪掉時為 <see langword="true"/>;本來就不存在時為 <see langword="false"/>,不是失敗。
    /// <see langword="true"/> when something was removed; <see langword="false"/> when there was nothing to
    /// remove, which is not a failure.
    /// </returns>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
}
