using System.Text.RegularExpressions;
using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Templates;

namespace Ozakboy.Line.AutoReply;

/// <summary>
/// 在一組規則裡挑出該用哪一條。
/// Picks which rule out of a set applies.
/// </summary>
/// <remarks>
/// 挑選順序是<b>先看 <see cref="LineAutoReplyRule.Priority"/>,同分再看比對方式的明確程度</b>
/// (Exact &gt; StartsWith &gt; Contains &gt; Regex)。第二層排序很重要:規則多起來之後,
/// 一條寫得寬鬆的 <c>Contains</c> 很容易把一堆精準的 <c>Exact</c> 全部吃掉,
/// 而那種問題從規則列表上看不出來 —— 每一條單獨看都是對的。
/// Rules are ordered by <see cref="LineAutoReplyRule.Priority"/> first and, within a tie, by how specific the
/// match is: Exact &gt; StartsWith &gt; Contains &gt; Regex. That second level matters. Once there are many
/// rules, one loose <c>Contains</c> easily swallows a pile of precise <c>Exact</c> ones, and nothing in the rule
/// list shows it — every rule looks right on its own.
/// </remarks>
public static class LineAutoReplyMatcher
{
    /// <summary>
    /// 正規表示式比對的時間上限。
    /// The time cap on a regular expression match.
    /// </summary>
    /// <remarks>
    /// 有上限是因為規則由人編輯,而一個不小心寫出來的回溯爆炸樣式(例如 <c>(a+)+b</c>)
    /// 可以讓一次比對跑上好幾秒。webhook 有回應時限,超時的代價是 LINE 重送整批事件。
    /// The cap exists because rules are edited by people, and a catastrophic-backtracking pattern written by
    /// accident — <c>(a+)+b</c>, say — can take seconds for one match. Webhooks have a response deadline, and
    /// overrunning it costs a redelivery of the whole batch.
    /// </remarks>
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// 依訊息文字挑出該回覆的規則。
    /// Picks the rule that answers a message's text.
    /// </summary>
    /// <param name="rules">全部規則。Every rule.</param>
    /// <param name="text">使用者傳來的文字。The text the user sent.</param>
    /// <returns>命中的規則;沒有命中時為 <see langword="null"/>。The matching rule, or <see langword="null"/>.</returns>
    /// <remarks>
    /// 只比對看文字的四種模式;<see cref="LineAutoReplyMatchMode.Follow"/> 與
    /// <see cref="LineAutoReplyMatchMode.Fallback"/> 由 <see cref="FindFirst"/> 取。
    /// Only the four text modes are considered here; <see cref="LineAutoReplyMatchMode.Follow"/> and
    /// <see cref="LineAutoReplyMatchMode.Fallback"/> come from <see cref="FindFirst"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="rules"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="rules"/> is <see langword="null"/>.
    /// </exception>
    public static LineAutoReplyRule? Match(IReadOnlyList<LineAutoReplyRule> rules, string text)
    {
        ArgumentNullException.ThrowIfNull(rules);

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        // 比對前去除頭尾空白。手機鍵盤的自動空格、複製貼上帶進來的換行,都會讓一個原本該命中的
        // Exact 規則悄悄失效,而使用者只覺得「打了沒反應」。
        // Trimmed before matching: a phone keyboard's trailing space or a newline picked up by copy-and-paste is
        // enough to make an Exact rule quietly stop working, and all the user sees is a message that got no reply.
        var trimmed = text.Trim();

        LineAutoReplyRule? best = null;
        var bestRank = (Priority: 0, Specificity: 0);

        for (var index = 0; index < rules.Count; index++)
        {
            var rule = rules[index];

            if (!rule.Enabled || !IsTextMode(rule.Match) || !Matches(rule, trimmed))
            {
                continue;
            }

            var rank = (Priority: rule.Priority, Specificity: Specificity(rule.Match));
            if (best is null || rank.Priority < bestRank.Priority
                || (rank.Priority == bestRank.Priority && rank.Specificity < bestRank.Specificity))
            {
                best = rule;
                bestRank = rank;
            }
        }

        return best;
    }

    /// <summary>
    /// 取出指定比對方式中 <see cref="LineAutoReplyRule.Priority"/> 最小的啟用規則。
    /// Takes the enabled rule with the smallest <see cref="LineAutoReplyRule.Priority"/> for a given match mode.
    /// </summary>
    /// <param name="rules">全部規則。Every rule.</param>
    /// <param name="mode">比對方式,通常是 <see cref="LineAutoReplyMatchMode.Follow"/> 或 <see cref="LineAutoReplyMatchMode.Fallback"/>。The match mode, normally <see cref="LineAutoReplyMatchMode.Follow"/> or <see cref="LineAutoReplyMatchMode.Fallback"/>.</param>
    /// <returns>規則;沒有時為 <see langword="null"/>。The rule, or <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="rules"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="rules"/> is <see langword="null"/>.
    /// </exception>
    public static LineAutoReplyRule? FindFirst(IReadOnlyList<LineAutoReplyRule> rules, LineAutoReplyMatchMode mode)
    {
        ArgumentNullException.ThrowIfNull(rules);

        LineAutoReplyRule? best = null;

        for (var index = 0; index < rules.Count; index++)
        {
            var rule = rules[index];

            if (rule.Enabled && rule.Match == mode && (best is null || rule.Priority < best.Priority))
            {
                best = rule;
            }
        }

        return best;
    }

    /// <summary>
    /// 檢查一條規則本身是否成立。
    /// Checks whether a rule holds together on its own.
    /// </summary>
    /// <param name="rule">規則。The rule.</param>
    /// <returns>
    /// 成立時為成功,否則為 <see cref="LineErrorCodes.InvalidAutoReplyRule"/> 或
    /// <see cref="LineErrorCodes.TooManyMessages"/> / <see cref="LineErrorCodes.InvalidJson"/> 的失敗。
    /// Success when it holds, otherwise a <see cref="LineErrorCodes.InvalidAutoReplyRule"/>,
    /// <see cref="LineErrorCodes.TooManyMessages"/>, or <see cref="LineErrorCodes.InvalidJson"/> failure.
    /// </returns>
    /// <remarks>
    /// 在<b>存進 store 之前</b>驗證,而不是等到有人觸發規則才發現。一條 JSON 壞掉的規則存進去之後,
    /// 症狀是某些使用者傳訊息完全沒有回應,而 log 裡只有一行渲染失敗 —— 那時已經不知道是誰什麼時候改壞的。
    /// Validate <b>before</b> storing rather than discovering the problem when someone triggers the rule. A rule
    /// with broken JSON in the store shows up as certain users getting no reply at all, with one rendering
    /// failure in the log — and by then nobody knows who broke it or when.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="rule"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="rule"/> is <see langword="null"/>.
    /// </exception>
    public static Result Validate(LineAutoReplyRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (IsTextMode(rule.Match))
        {
            if (string.IsNullOrWhiteSpace(rule.Pattern))
            {
                return Error.Validation(
                    LineErrorCodes.InvalidAutoReplyRule,
                    "比對樣式不可為空白。The match pattern cannot be blank.");
            }

            if (rule.Match == LineAutoReplyMatchMode.Regex && !CanCompile(rule))
            {
                return Error.Validation(
                    LineErrorCodes.InvalidAutoReplyRule,
                    "正規表示式無法編譯。The regular expression will not compile.");
            }
        }

        // Follow 與 Fallback 不看 Pattern:它們的觸發條件是事件型別,不是文字。
        // 在這裡硬要求填一個用不到的樣式,只會讓編輯的人填一個假值進去。
        // Follow and Fallback ignore the pattern: what triggers them is the event, not any text. Insisting on a
        // pattern that is never read would only have whoever edits them type in a placeholder.
        return LineTemplateRenderer.Validate(rule.MessagesJson).ToResult();
    }

    /// <summary>
    /// 這個比對方式是否看訊息文字。
    /// Whether this match mode reads the message's text.
    /// </summary>
    /// <param name="mode">比對方式。The match mode.</param>
    /// <returns>看文字時為 <see langword="true"/>。<see langword="true"/> when it reads the text.</returns>
    private static bool IsTextMode(LineAutoReplyMatchMode mode) =>
        mode is LineAutoReplyMatchMode.Exact
            or LineAutoReplyMatchMode.Contains
            or LineAutoReplyMatchMode.StartsWith
            or LineAutoReplyMatchMode.Regex;

    /// <summary>
    /// 比對方式的明確程度,數字小的比較明確。
    /// How specific a match mode is; a smaller number is more specific.
    /// </summary>
    /// <param name="mode">比對方式。The match mode.</param>
    /// <returns>排序用的名次。The rank used for ordering.</returns>
    private static int Specificity(LineAutoReplyMatchMode mode) => mode switch
    {
        LineAutoReplyMatchMode.Exact => 0,
        LineAutoReplyMatchMode.StartsWith => 1,
        LineAutoReplyMatchMode.Contains => 2,
        _ => 3,
    };

    /// <summary>
    /// 單一規則是否符合這段文字。
    /// Whether one rule matches this text.
    /// </summary>
    /// <param name="rule">規則。The rule.</param>
    /// <param name="text">已去除頭尾空白的文字。The trimmed text.</param>
    /// <returns>符合時為 <see langword="true"/>。<see langword="true"/> when it matches.</returns>
    private static bool Matches(LineAutoReplyRule rule, string text)
    {
        if (string.IsNullOrEmpty(rule.Pattern))
        {
            return false;
        }

        var comparison = rule.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        return rule.Match switch
        {
            LineAutoReplyMatchMode.Exact => string.Equals(text, rule.Pattern, comparison),
            LineAutoReplyMatchMode.StartsWith => text.StartsWith(rule.Pattern, comparison),
            LineAutoReplyMatchMode.Contains => text.Contains(rule.Pattern, comparison),
            LineAutoReplyMatchMode.Regex => RegexMatches(rule, text),
            _ => false,
        };
    }

    /// <summary>
    /// 以正規表示式比對,逾時或表示式無效時視為不符。
    /// Matches with a regular expression, treating a timeout or an invalid expression as no match.
    /// </summary>
    /// <param name="rule">規則。The rule.</param>
    /// <param name="text">文字。The text.</param>
    /// <returns>符合時為 <see langword="true"/>。<see langword="true"/> when it matches.</returns>
    /// <remarks>
    /// 每次比對都重新建一個 <see cref="Regex"/>,沒有快取。規則是可編輯的資料,快取就要處理失效;
    /// 而自動回覆一次只跑幾條規則、每條只比一句話,這裡不是值得先優化的地方。
    /// A fresh <see cref="Regex"/> per match, with no cache. Rules are editable data, and a cache would need
    /// invalidating; an auto reply runs a handful of rules against one sentence, which is not where the time
    /// goes.
    /// </remarks>
    private static bool RegexMatches(LineAutoReplyRule rule, string text)
    {
        try
        {
            var options = RegexOptions.CultureInvariant | (rule.IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
            return Regex.IsMatch(text, rule.Pattern, options, RegexTimeout);
        }
        catch (RegexMatchTimeoutException)
        {
            // 逾時視為不符而不是往外擲。壞掉的那條規則不會回覆,但其他規則與整個 webhook 照常運作。
            // A timeout counts as no match rather than propagating: the broken rule does not reply, while the
            // other rules and the webhook itself carry on.
            return false;
        }
        catch (ArgumentException)
        {
            // 表示式本身無效(存進 store 之前應該被 Validate 擋下,但 store 裡可能有更早存入的舊規則)。
            // The expression itself is invalid. Validate should have stopped it before the store, but a store can
            // hold rules written before that check existed.
            return false;
        }
    }

    /// <summary>
    /// 規則的正規表示式編不編得起來。
    /// Whether the rule's regular expression compiles.
    /// </summary>
    /// <param name="rule">規則。The rule.</param>
    /// <returns>編得起來時為 <see langword="true"/>。<see langword="true"/> when it compiles.</returns>
    private static bool CanCompile(LineAutoReplyRule rule)
    {
        try
        {
            _ = new Regex(rule.Pattern, RegexOptions.CultureInvariant, RegexTimeout);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
