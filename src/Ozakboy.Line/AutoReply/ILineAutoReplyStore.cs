namespace Ozakboy.Line.AutoReply;

/// <summary>
/// 自動回覆規則的儲存體。
/// The store holding auto reply rules.
/// </summary>
/// <remarks>
/// 形狀與 <see cref="Templates.ILineTemplateStore"/> 相同,而且是刻意的:兩者的宿主實作
/// (接資料庫的那一份)幾乎可以照抄,不必為了兩套不同的方法名再想一次。
/// The shape matches <see cref="Templates.ILineTemplateStore"/> on purpose: a host's implementation — the one
/// backed by a database — can be written once and copied, instead of being thought through again for a second
/// set of method names.
/// </remarks>
public interface ILineAutoReplyStore
{
    /// <summary>
    /// 列出所有規則。
    /// Lists every rule.
    /// </summary>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>規則清單。The rules.</returns>
    Task<IReadOnlyList<LineAutoReplyRule>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得單一規則。
    /// Gets one rule.
    /// </summary>
    /// <param name="id">規則識別碼。The rule identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>找不到時為 <see langword="null"/>。<see langword="null"/> when there is no such rule.</returns>
    Task<LineAutoReplyRule?> GetAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增或更新規則。
    /// Adds or updates a rule.
    /// </summary>
    /// <param name="rule">規則;<see cref="LineAutoReplyRule.Id"/> 留空代表新增。The rule; a blank <see cref="LineAutoReplyRule.Id"/> means an insert.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>存好的規則。The stored rule.</returns>
    Task<LineAutoReplyRule> UpsertAsync(LineAutoReplyRule rule, CancellationToken cancellationToken = default);

    /// <summary>
    /// 刪除規則。
    /// Deletes a rule.
    /// </summary>
    /// <param name="id">規則識別碼。The rule identifier.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>
    /// 真的刪掉時為 <see langword="true"/>;本來就不存在時為 <see langword="false"/>,不是失敗。
    /// <see langword="true"/> when something was removed; <see langword="false"/> when there was nothing to
    /// remove, which is not a failure.
    /// </returns>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
}
