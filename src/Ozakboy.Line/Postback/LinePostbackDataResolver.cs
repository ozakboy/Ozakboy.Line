using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Webhook;

namespace Ozakboy.Line.Postback;

/// <summary>
/// 在 webhook 端把 postback 的 <c>Data</c> 還原成原資料:是暫存代碼就查 store,否則原樣回。
/// On the webhook side, turns a postback's <c>Data</c> back into the original: a staging token is looked up in
/// the store, anything else is returned as is.
/// </summary>
public static class LinePostbackDataResolver
{
    /// <summary>
    /// 還原 postback 資料。
    /// Resolves the postback data.
    /// </summary>
    /// <param name="postback">收到的回傳事件內容。The received postback contents.</param>
    /// <param name="store">暫存儲存體。The staging store.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>
    /// <see cref="LineWebhookPostback.Data"/> 不是 <see cref="LinePostbackDataToken"/> 格式 → 原樣成功;
    /// 是代碼 → 查 <paramref name="store"/>,找不到或過期為 <see cref="LineErrorCodes.PostbackExpired"/> 失敗。
    /// Success with <see cref="LineWebhookPostback.Data"/> unchanged when it is not in the
    /// <see cref="LinePostbackDataToken"/> format; otherwise the token is looked up in <paramref name="store"/>,
    /// failing with <see cref="LineErrorCodes.PostbackExpired"/> when missing or expired.
    /// </returns>
    /// <remarks>
    /// 還原後的資料與原本的 <c>Data</c> 一樣<b>沒有簽章</b>:代碼是 122 位元隨機值,猜不中,
    /// 但按鈕走過使用者的裝置、可被轉貼,任何權限判斷仍要回頭用事件來源的使用者識別碼查自己的資料。
    /// The resolved data is just as <b>unsigned</b> as a plain <c>Data</c>: the token is 122 bits of randomness and
    /// cannot be guessed, but a button travels through the user's device and can be forwarded, so any authorisation
    /// decision still has to look the event source's user identifier up in one's own records.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="postback"/> 或 <paramref name="store"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="postback"/> or <paramref name="store"/> is <see langword="null"/>.
    /// </exception>
    public static async Task<Result<string>> ResolveAsync(
        LineWebhookPostback postback,
        ILinePostbackDataStore store,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(postback);
        ArgumentNullException.ThrowIfNull(store);

        if (!LinePostbackDataToken.IsToken(postback.Data))
        {
            return Result.Success(postback.Data);
        }

        return await store.GetAsync(postback.Data, cancellationToken).ConfigureAwait(false);
    }
}
