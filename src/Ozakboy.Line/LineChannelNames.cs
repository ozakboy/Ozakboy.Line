namespace Ozakboy.Line;

/// <summary>
/// 給錯誤訊息用的 channel 名稱描述。
/// Describes channel names for error messages.
/// </summary>
internal static class LineChannelNames
{
    /// <summary>
    /// 把名稱清單寫成可讀的文字;空字串名稱(預設 channel)寫成「(預設 default)」。
    /// Writes a name list as readable text; the empty-string name (the default channel) is written as
    /// "(預設 default)".
    /// </summary>
    /// <param name="names">名稱。The names.</param>
    /// <returns>描述文字。The description.</returns>
    internal static string Describe(IReadOnlyCollection<string> names) =>
        names.Count == 0
            ? "(無 none)"
            : string.Join(", ", names.Select(name => name.Length == 0 ? "(預設 default)" : $"\"{name}\""));
}
