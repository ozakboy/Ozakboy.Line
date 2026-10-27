using Ozakboy.Core.Abstractions;

namespace Ozakboy.Line.Liff;

/// <summary>
/// LIFF server API 用戶端:新增、更新、列出與刪除 LIFF 應用程式。
/// The LIFF server API client: adds, updates, lists and deletes LIFF apps.
/// </summary>
/// <remarks>
/// <para>
/// LIFF 應用程式屬於 LINE Login channel,用的是該 channel 的 channel access token
/// (見 <see cref="LineLiffOptions.ChannelAccessToken"/>),不是 Messaging channel 的。
/// LIFF apps belong to a LINE Login channel and use that channel's channel access token
/// (see <see cref="LineLiffOptions.ChannelAccessToken"/>), not the Messaging channel's.
/// </para>
/// <para>
/// 憑證未設定時所有方法直接回 <see cref="LineErrorCodes.NotConfigured"/> 失敗,一個請求都不送。
/// When the credentials are not set every method fails with <see cref="LineErrorCodes.NotConfigured"/> and sends
/// no request at all.
/// </para>
/// </remarks>
public interface ILineLiffClient
{
    /// <summary>
    /// 新增一個 LIFF 應用程式。
    /// Adds a LIFF app.
    /// </summary>
    /// <param name="app">
    /// 應用程式設定;<see cref="LineLiffApp.View"/> 的 <see cref="LineLiffView.Type"/> 與
    /// <see cref="LineLiffView.Url"/> 必填。不合規則時回 <see cref="LineErrorCodes.InvalidLiffApp"/> 失敗且不送出。
    /// The app's settings; the <see cref="LineLiffView.Type"/> and <see cref="LineLiffView.Url"/> of
    /// <see cref="LineLiffApp.View"/> are required. A rule violation fails with
    /// <see cref="LineErrorCodes.InvalidLiffApp"/> and nothing is sent.
    /// </param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>新應用程式的 LIFF ID。The new app's LIFF ID.</returns>
    Task<Result<string>> AddAsync(LineLiffApp app, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新一個 LIFF 應用程式,只送有值的欄位。
    /// Updates a LIFF app, sending only the fields that have a value.
    /// </summary>
    /// <param name="liffId">要更新的 LIFF ID。The LIFF ID to update.</param>
    /// <param name="app">要改的欄位;<see cref="LineLiffApp.LiffId"/> 不會送出。The fields to change; <see cref="LineLiffApp.LiffId"/> is never sent.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>成功或失敗。Success or failure.</returns>
    Task<Result> UpdateAsync(string liffId, LineLiffApp app, CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出這個 channel 的所有 LIFF 應用程式。
    /// Lists every LIFF app of this channel.
    /// </summary>
    /// <remarks>
    /// <b>404 視為「沒有任何應用程式」</b>,回成功的空清單:channel 還沒建立過任何 LIFF 時 LINE 回 404,
    /// 那是「空」而不是「出錯」。其他狀態碼仍是失敗。
    /// <b>A 404 means "no apps at all"</b> and returns a successful empty list: LINE answers 404 for a channel
    /// that has never had a LIFF app, which is "empty" rather than "wrong". Other status codes still fail.
    /// </remarks>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>應用程式清單。The apps.</returns>
    Task<Result<IReadOnlyList<LineLiffApp>>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 刪除一個 LIFF 應用程式。
    /// Deletes a LIFF app.
    /// </summary>
    /// <param name="liffId">要刪除的 LIFF ID。The LIFF ID to delete.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>成功或失敗。Success or failure.</returns>
    Task<Result> DeleteAsync(string liffId, CancellationToken cancellationToken = default);
}
