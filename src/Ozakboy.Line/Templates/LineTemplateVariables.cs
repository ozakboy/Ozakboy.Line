using System.Text.RegularExpressions;

namespace Ozakboy.Line.Templates;

/// <summary>
/// 從範本內容裡找出 <c>{{變數}}</c> 佔位符。
/// Finds the <c>{{variable}}</c> placeholders in a template body.
/// </summary>
/// <remarks>
/// 佔位符的名字限定為「英文字母或底線開頭,後接英數字或底線」。限制得緊一點是刻意的:
/// 名字若能含空白或標點,同一個變數就會有好幾種寫法(<c>{{ name }}</c>、<c>{{name }}</c>),
/// 而漏掉其中一種的結果是把 <c>{{name}}</c> 原樣送到使用者眼前。
/// A placeholder name must start with a letter or an underscore and continue with letters, digits or
/// underscores. The narrow rule is deliberate: names allowing spaces or punctuation give one variable several
/// spellings — <c>{{ name }}</c>, <c>{{name }}</c> — and missing one of them means sending <c>{{name}}</c>
/// verbatim to a user.
/// </remarks>
public static partial class LineTemplateVariables
{
    /// <summary>
    /// 抽出範本裡用到的所有變數名,去重且保留出現順序。
    /// Extracts every variable name used in the template, deduplicated and in order of appearance.
    /// </summary>
    /// <param name="messagesJson">範本內容。The template body.</param>
    /// <returns>變數名;沒有佔位符時為空清單。The names, or an empty list when there are no placeholders.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="messagesJson"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="messagesJson"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// 保留順序而不是排序,是為了讓「缺變數」的錯誤訊息照著範本的閱讀順序列出來 ——
    /// 編輯範本的人比對時不必在文件裡跳來跳去。
    /// The order of appearance is kept rather than sorted so that a missing-variable message lists them in the
    /// order the template reads, and whoever is editing it does not have to jump around the document.
    /// </remarks>
    public static IReadOnlyList<string> Extract(string messagesJson)
    {
        ArgumentNullException.ThrowIfNull(messagesJson);

        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var match in PlaceholderRegex().EnumerateMatches(messagesJson))
        {
            var placeholder = messagesJson.AsSpan(match.Index, match.Length);
            var name = ReadName(placeholder);

            if (seen.Add(name))
            {
                names.Add(name);
            }
        }

        return names;
    }

    /// <summary>
    /// 把佔位符裡的變數名取出來(去掉兩側的大括號與空白)。
    /// Takes the variable name out of a placeholder, dropping the braces and the surrounding whitespace.
    /// </summary>
    /// <param name="placeholder">整個佔位符,含大括號。The whole placeholder, braces included.</param>
    /// <returns>變數名。The variable name.</returns>
    internal static string ReadName(ReadOnlySpan<char> placeholder) =>
        placeholder[2..^2].Trim().ToString();

    /// <summary>
    /// 佔位符的樣式。抽出與替換走同一個表示式,兩邊認得的佔位符才不會不一樣。
    /// The placeholder pattern. Extraction and replacement share one expression so the two cannot disagree about
    /// what counts as a placeholder.
    /// </summary>
    /// <returns>編譯好的正規表示式。The compiled regular expression.</returns>
    [GeneratedRegex(@"\{\{\s*[A-Za-z_][A-Za-z0-9_]*\s*\}\}", RegexOptions.CultureInvariant)]
    internal static partial Regex PlaceholderRegex();
}
