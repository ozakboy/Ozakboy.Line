using System.Text.Json.Serialization;
using System.Globalization;
using System.Text.Json;
using Ozakboy.Core.Abstractions;

namespace Ozakboy.Line.Messaging.Messages;

/// <summary>
/// 第二代文字訊息(<c>textV2</c>):在文字裡以 <c>{key}</c> 引用 @ 提及與 LINE emoji。
/// The second-generation text message (<c>textV2</c>): the text refers to mentions and LINE emojis as
/// <c>{key}</c>.
/// </summary>
/// <remarks>
/// <para>
/// 與 <see cref="TextMessage"/> 的 <c>$</c> 位置計算不同,這裡不用數字位置 —— 文字裡的 <c>{key}</c> 對應
/// <see cref="Substitution"/> 的鍵,LINE 自己找位置,不會因為前面多了一個代理對就全部偏掉。
/// Unlike the <c>$</c> positions of <see cref="TextMessage"/>, no numeric positions are involved: each
/// <c>{key}</c> in the text matches a key in <see cref="Substitution"/> and LINE finds the place itself, so one
/// extra surrogate pair earlier in the text cannot throw everything off.
/// </para>
/// <para>
/// 本地檢查(<see cref="Validate"/>)要求引用與替換項一一對應:文字裡每個 <c>{key}</c> 都要有替換項,
/// 每個替換項也都要被引用至少一次 —— 多出來的替換項在 LINE 那頭是整則訊息退回。
/// The local check (<see cref="Validate"/>) wants references and substitutions to match one to one: every
/// <c>{key}</c> in the text needs a substitution and every substitution must be referenced at least once —
/// a stray substitution has LINE reject the whole message.
/// </para>
/// </remarks>
[JsonConverter(typeof(LineMessageJsonConverterFactory))]
public sealed class TextV2Message : LineMessage
{
    /// <summary>
    /// 建立第二代文字訊息。
    /// Creates a second-generation text message.
    /// </summary>
    /// <param name="text">訊息內容,以 <c>{key}</c> 引用替換項。The message text, referring to substitutions as <c>{key}</c>.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="text"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="text"/> is <see langword="null"/> or blank.
    /// </exception>
    public TextV2Message(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Text = text;
    }

    /// <inheritdoc />
    public override string Type => "textV2";

    /// <summary>
    /// 訊息內容。
    /// The message text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// 具名替換項;鍵只能是英數字與底線。
    /// The named substitutions; keys may hold only letters, digits and underscores.
    /// </summary>
    public IDictionary<string, LineTextSubstitution> Substitution { get; } =
        new Dictionary<string, LineTextSubstitution>(StringComparer.Ordinal);

    /// <summary>
    /// 引用的訊息權杖;要引用某則訊息回覆時設定。
    /// The quote token, set when this message quotes another.
    /// </summary>
    public string? QuoteToken { get; set; }

    /// <inheritdoc />
    public override Result Validate()
    {
        var baseResult = base.Validate();
        if (baseResult.IsFailure)
        {
            return baseResult;
        }

        if (LineMessageValidation.IsTooLong(Text, LineMessagingLimits.MaxTextLength))
        {
            return LineMessageValidation.InvalidMessage(
                LineMessageValidation.TooLong("textV2 訊息的 text", Text, LineMessagingLimits.MaxTextLength));
        }

        foreach (var key in Substitution.Keys)
        {
            if (!IsValidKey(key))
            {
                return LineMessageValidation.InvalidMessage(
                    $"substitution 的鍵 '{LineMessageValidation.Quote(key)}' 不合規格:只能是英數字與底線,且不可為空。The substitution key '{LineMessageValidation.Quote(key)}' is invalid: only letters, digits and underscores are allowed, and it cannot be empty.");
            }
        }

        var referenced = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in ReadReferences(Text))
        {
            if (!Substitution.ContainsKey(key))
            {
                return LineMessageValidation.InvalidMessage(
                    $"text 引用了 {{{key}}},但 substitution 沒有這個鍵。The text refers to {{{key}}}, but substitution has no such key.");
            }

            referenced.Add(key);
        }

        foreach (var key in Substitution.Keys)
        {
            if (!referenced.Contains(key))
            {
                return LineMessageValidation.InvalidMessage(
                    $"substitution 的鍵 '{key}' 沒有在 text 裡以 {{{key}}} 引用。The substitution key '{key}' is never referenced in the text as {{{key}}}.");
            }
        }

        var mentions = 0;
        var emojis = 0;
        foreach (var substitution in Substitution.Values)
        {
            if (substitution.Type == LineTextSubstitution.TypeMention)
            {
                mentions++;
            }
            else
            {
                emojis++;
            }
        }

        if (mentions > LineMessagingLimits.MaxTextV2Mentions)
        {
            return LineMessageValidation.InvalidMessage(string.Create(
                CultureInfo.InvariantCulture,
                $"textV2 訊息最多帶 {LineMessagingLimits.MaxTextV2Mentions} 個 mention,這次是 {mentions} 個。A textV2 message takes at most {LineMessagingLimits.MaxTextV2Mentions} mentions; {mentions} were supplied."));
        }

        return emojis > LineMessagingLimits.MaxTextEmojis
            ? LineMessageValidation.InvalidMessage(string.Create(
                CultureInfo.InvariantCulture,
                $"textV2 訊息最多帶 {LineMessagingLimits.MaxTextEmojis} 個 emoji,這次是 {emojis} 個。A textV2 message takes at most {LineMessagingLimits.MaxTextEmojis} emojis; {emojis} were supplied."))
            : Result.Success();
    }

    /// <inheritdoc />
    internal override void WriteBody(Utf8JsonWriter writer)
    {
        writer.WriteString("text", Text);

        if (Substitution.Count > 0)
        {
            writer.WriteStartObject("substitution");
            foreach (var (key, substitution) in Substitution)
            {
                writer.WritePropertyName(key);
                substitution.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        if (!string.IsNullOrWhiteSpace(QuoteToken))
        {
            writer.WriteString("quoteToken", QuoteToken);
        }
    }

    /// <summary>
    /// 判斷替換項的鍵是否合規格。
    /// Tells whether a substitution key is well-formed.
    /// </summary>
    /// <param name="key">鍵。The key.</param>
    /// <returns>非空且全是 ASCII 英數字或底線時為 <see langword="true"/>。<see langword="true"/> when non-empty and made of ASCII letters, digits and underscores only.</returns>
    private static bool IsValidKey(string key)
    {
        if (key.Length == 0)
        {
            return false;
        }

        foreach (var character in key)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character != '_')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 依出現順序讀出文字裡所有 <c>{key}</c> 的 key。
    /// Reads, in order of appearance, the key of every <c>{key}</c> in the text.
    /// </summary>
    /// <param name="text">文字。The text.</param>
    /// <returns>
    /// 引用的鍵。只認 <c>{</c> 後面緊接英數字與底線、再接 <c>}</c> 的片段,其餘的大括號當一般字元。
    /// The referenced keys. Only a <c>{</c> followed by letters, digits and underscores and then a <c>}</c> counts;
    /// any other brace is an ordinary character.
    /// </returns>
    private static IEnumerable<string> ReadReferences(string text)
    {
        var start = 0;
        while ((start = text.IndexOf('{', start)) >= 0)
        {
            var end = start + 1;
            while (end < text.Length && (char.IsAsciiLetterOrDigit(text[end]) || text[end] == '_'))
            {
                end++;
            }

            if (end < text.Length && text[end] == '}' && end > start + 1)
            {
                yield return text.Substring(start + 1, end - start - 1);
                start = end + 1;
            }
            else
            {
                start++;
            }
        }
    }
}
