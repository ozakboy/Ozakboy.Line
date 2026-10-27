namespace Ozakboy.Line;

/// <summary>
/// LINE 各項服務的端點位址(頻道設定、帳號連結的補充端點)。
/// The endpoint addresses of LINE's services — the channel-settings and account-linking additions.
/// </summary>
public static partial class LineEndpoints
{
    /// <summary>
    /// 查詢與設定 webhook 位址的端點(GET 查、PUT 設)。
    /// The endpoint for reading and setting the webhook address (GET reads, PUT sets).
    /// </summary>
    public const string WebhookEndpoint = "https://api.line.me/v2/bot/channel/webhook/endpoint";

    /// <summary>
    /// 要求 LINE 對 webhook 位址送一次測試請求的端點。
    /// The endpoint that asks LINE to send a test request to the webhook address.
    /// </summary>
    public const string WebhookTest = "https://api.line.me/v2/bot/channel/webhook/test";

    /// <summary>
    /// 簽發帳號連結權杖(路徑為 <c>/v2/bot/user/{userId}/linkToken</c>)。
    /// Issues an account link token at <c>/v2/bot/user/{userId}/linkToken</c>.
    /// </summary>
    public const string UserLinkTokenBase = "https://api.line.me/v2/bot/user/";
}
