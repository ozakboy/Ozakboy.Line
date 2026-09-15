using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Messaging;
using Ozakboy.Line.Templates;
using Ozakboy.Line.Webhook;

namespace Ozakboy.Line.AutoReply;

/// <summary>
/// <see cref="ILineAutoReplyService"/> 的實作。
/// The implementation of <see cref="ILineAutoReplyService"/>.
/// </summary>
/// <remarks>
/// 一律用 <b>reply token 回覆</b>而不是推播:回覆不計推播額度,而自動回覆是所有功能裡最會消耗額度的那一個 ——
/// 改用推播的話,一個熱門帳號的免費額度撐不過幾天。
/// It always <b>replies with the reply token</b> rather than pushing. A reply costs no push quota, and auto reply
/// is the heaviest consumer of quota there is: on push, a busy account's free allowance does not last the week.
/// </remarks>
public sealed partial class LineAutoReplyService : ILineAutoReplyService
{
    /// <summary>
    /// 使用者原訊息的佔位符名稱。
    /// The placeholder name for the message the user sent.
    /// </summary>
    private const string TextVariable = "text";

    /// <summary>
    /// 使用者顯示名稱的佔位符名稱。
    /// The placeholder name for the user's display name.
    /// </summary>
    private const string DisplayNameVariable = "displayName";

    private readonly ILineAutoReplyStore _store;
    private readonly ILineMessagingClient _messaging;
    private readonly IOptions<LineAutoReplyOptions> _options;
    private readonly ILogger<LineAutoReplyService>? _logger;

    /// <summary>
    /// 建立服務。
    /// Creates the service.
    /// </summary>
    /// <param name="store">規則儲存體。The rule store.</param>
    /// <param name="messaging">Messaging API 用戶端。The Messaging API client.</param>
    /// <param name="options">自動回覆設定。The auto reply settings.</param>
    /// <param name="logger">記錄器,可為 <see langword="null"/>。The logger, which may be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// 前三個參數之一為 <see langword="null"/> 時擲出。
    /// Thrown when any of the first three arguments is <see langword="null"/>.
    /// </exception>
    public LineAutoReplyService(
        ILineAutoReplyStore store,
        ILineMessagingClient messaging,
        IOptions<LineAutoReplyOptions> options,
        ILogger<LineAutoReplyService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(messaging);
        ArgumentNullException.ThrowIfNull(options);

        _store = store;
        _messaging = messaging;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<LineAutoReplyOutcome>> HandleAsync(
        LineWebhookEvent webhookEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(webhookEvent);

        if (!_options.Value.Enabled || string.IsNullOrWhiteSpace(webhookEvent.ReplyToken))
        {
            return Result.Success(LineAutoReplyOutcome.Skipped);
        }

        // standby 模式的事件不回。那代表有 LINE 模組(例如人工客服介面)接手了這段對話,
        // 這時自動回覆等於跟真人搶著講話。
        // A standby event is left alone: another LINE module — a human operator console, say — has taken the
        // conversation, and answering then means talking over a person.
        if (string.Equals(webhookEvent.Mode, "standby", StringComparison.Ordinal))
        {
            return Result.Success(LineAutoReplyOutcome.Skipped);
        }

        if (string.Equals(webhookEvent.Type, LineWebhookEventTypes.Follow, StringComparison.Ordinal))
        {
            return await HandleFollowAsync(webhookEvent, cancellationToken).ConfigureAwait(false);
        }

        if (string.Equals(webhookEvent.Type, LineWebhookEventTypes.Message, StringComparison.Ordinal)
            && webhookEvent.Message is { Type: LineWebhookMessageTypes.Text, Text: { } text })
        {
            return await HandleTextAsync(webhookEvent, text, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(LineAutoReplyOutcome.Skipped);
    }

    /// <summary>
    /// 處理加好友事件:找歡迎規則並回覆。
    /// Handles an add-friend event by finding the welcome rule and answering.
    /// </summary>
    /// <param name="webhookEvent">事件。The event.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>處理結果。The outcome.</returns>
    private async Task<Result<LineAutoReplyOutcome>> HandleFollowAsync(
        LineWebhookEvent webhookEvent,
        CancellationToken cancellationToken)
    {
        var rules = await _store.ListAsync(cancellationToken).ConfigureAwait(false);
        var rule = LineAutoReplyMatcher.FindFirst(rules, LineAutoReplyMatchMode.Follow);

        return rule is null
            ? Result.Success(LineAutoReplyOutcome.Skipped)
            : await ReplyAsync(webhookEvent, rule, text: string.Empty, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 處理文字訊息:先比對一般規則,沒命中再找備援規則。
    /// Handles a text message: the ordinary rules first, then the fallback rule.
    /// </summary>
    /// <param name="webhookEvent">事件。The event.</param>
    /// <param name="text">訊息文字。The message text.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>處理結果。The outcome.</returns>
    private async Task<Result<LineAutoReplyOutcome>> HandleTextAsync(
        LineWebhookEvent webhookEvent,
        string text,
        CancellationToken cancellationToken)
    {
        var rules = await _store.ListAsync(cancellationToken).ConfigureAwait(false);
        var rule = LineAutoReplyMatcher.Match(rules, text)
            ?? LineAutoReplyMatcher.FindFirst(rules, LineAutoReplyMatchMode.Fallback);

        return rule is null
            ? Result.Success(LineAutoReplyOutcome.NoMatch)
            : await ReplyAsync(webhookEvent, rule, text, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 渲染規則的回覆內容並送出。
    /// Renders a rule's reply and sends it.
    /// </summary>
    /// <param name="webhookEvent">事件。The event.</param>
    /// <param name="rule">命中的規則。The matching rule.</param>
    /// <param name="text">使用者原訊息;加好友事件為空字串。The user's message, or an empty string on an add-friend event.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>處理結果。The outcome.</returns>
    private async Task<Result<LineAutoReplyOutcome>> ReplyAsync(
        LineWebhookEvent webhookEvent,
        LineAutoReplyRule rule,
        string text,
        CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [TextVariable] = text,
            [DisplayNameVariable] = await ResolveDisplayNameAsync(webhookEvent, rule, cancellationToken).ConfigureAwait(false),
        };

        var rendered = LineTemplateRenderer.Render(rule.MessagesJson, values);
        if (rendered.IsFailure)
        {
            return rendered.ToFailure<LineAutoReplyOutcome>();
        }

        var replied = await _messaging
            .ReplyRawJsonAsync(webhookEvent.ReplyToken!, rendered.GetValueOrThrow(), cancellationToken)
            .ConfigureAwait(false);

        if (replied.IsFailure)
        {
            return replied.ToFailure<LineAutoReplyOutcome>();
        }

        if (_logger is not null)
        {
            Log.Replied(_logger, rule.Id, rule.Name);
        }

        return Result.Success(LineAutoReplyOutcome.Replied(rule.Id));
    }

    /// <summary>
    /// 取得使用者的顯示名稱,只有規則真的用到 <c>{{displayName}}</c> 時才去查。
    /// Reads the user's display name, and only when the rule actually uses <c>{{displayName}}</c>.
    /// </summary>
    /// <param name="webhookEvent">事件。The event.</param>
    /// <param name="rule">規則。The rule.</param>
    /// <param name="cancellationToken">取消權杖。The cancellation token.</param>
    /// <returns>顯示名稱;查不到時為空字串。The display name, or an empty string when it cannot be read.</returns>
    /// <remarks>
    /// <para>
    /// 先看規則裡有沒有這個佔位符再決定要不要打 API:絕大多數規則用不到它,而無條件查一次
    /// 等於每則自動回覆都多一次往返,還多一個會失敗的地方。
    /// Whether to call the API is decided by looking for the placeholder first: the great majority of rules never
    /// use it, and an unconditional lookup means one more round trip per auto reply, and one more thing that can
    /// fail.
    /// </para>
    /// <para>
    /// 查不到就用空字串,<b>不讓整個回覆失敗</b>。使用者封鎖過又解封、或這是群組事件時,個人檔案本來就讀不到,
    /// 而為了一個名字讓歡迎訊息整個發不出去,是把小事變成大事。
    /// A failed lookup yields an empty string and <b>does not fail the reply</b>. A profile is simply unreadable
    /// for a user who blocked and unblocked the account, or for a group event, and losing the whole welcome
    /// message over a name turns a small problem into a large one.
    /// </para>
    /// </remarks>
    private async Task<string> ResolveDisplayNameAsync(
        LineWebhookEvent webhookEvent,
        LineAutoReplyRule rule,
        CancellationToken cancellationToken)
    {
        if (!rule.MessagesJson.Contains(DisplayNameVariable, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(webhookEvent.Source.UserId))
        {
            return string.Empty;
        }

        var profile = await _messaging
            .GetProfileAsync(webhookEvent.Source.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (profile.IsFailure)
        {
            if (_logger is not null)
            {
                Log.DisplayNameUnavailable(_logger, rule.Id, profile.Error.Code);
            }

            return string.Empty;
        }

        return profile.GetValueOrThrow().DisplayName;
    }

    /// <summary>
    /// 記錄訊息的定義。
    /// The log message definitions.
    /// </summary>
    private static partial class Log
    {
        /// <summary>
        /// 已依規則回覆時的記錄。
        /// Logged when a rule answered.
        /// </summary>
        /// <param name="logger">記錄器。The logger.</param>
        /// <param name="ruleId">規則識別碼。The rule's identifier.</param>
        /// <param name="ruleName">規則名稱。The rule's name.</param>
        [LoggerMessage(
            EventId = 2300,
            Level = LogLevel.Debug,
            Message = "自動回覆命中規則 {RuleId}({RuleName})。An auto reply matched rule {RuleId} ({RuleName}).")]
        internal static partial void Replied(ILogger logger, string ruleId, string ruleName);

        /// <summary>
        /// 查不到顯示名稱時的記錄。
        /// Logged when the display name could not be read.
        /// </summary>
        /// <param name="logger">記錄器。The logger.</param>
        /// <param name="ruleId">規則識別碼。The rule's identifier.</param>
        /// <param name="errorCode">查詢失敗的代碼。The lookup's failure code.</param>
        [LoggerMessage(
            EventId = 2301,
            Level = LogLevel.Debug,
            Message = "規則 {RuleId} 用到 displayName 但個人檔案查不到({ErrorCode}),以空字串代入。Rule {RuleId} uses displayName but the profile could not be read ({ErrorCode}); an empty string is substituted.")]
        internal static partial void DisplayNameUnavailable(ILogger logger, string ruleId, string errorCode);
    }
}
