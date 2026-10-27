using System.Text.Json.Serialization;
using System.Globalization;
using System.Text.Json;
using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Messaging.Messages;

namespace Ozakboy.Line.Messaging.Actions;

/// <summary>
/// 開啟日期 / 時間選擇器的動作。
/// An action that opens a date or time picker.
/// </summary>
/// <remarks>
/// 使用者選完之後,結果會以 <c>postback</c> 事件送回 webhook:
/// <see cref="Ozakboy.Line.Webhook.LineWebhookPostback.Data"/> 是這裡給的 <see cref="Data"/>,
/// 選到的值在原始 JSON 的 <c>postback.params</c> 裡。兩者分開的好處是同一個選擇器可以重複用 ——
/// <see cref="Data"/> 說「這是在選什麼」,<c>params</c> 說「選了什麼」。
/// What the user picks comes back as a <c>postback</c> event:
/// <see cref="Ozakboy.Line.Webhook.LineWebhookPostback.Data"/> is the <see cref="Data"/> given here, and the
/// chosen value sits in <c>postback.params</c> in the original JSON. Keeping them apart is what lets one picker
/// be reused: <see cref="Data"/> says what is being chosen and <c>params</c> says what was chosen.
/// </remarks>
[JsonConverter(typeof(LineActionJsonConverterFactory))]
public sealed class DatetimePickerAction : LineAction
{
    /// <summary>只選日期。A date only.</summary>
    public const string ModeDate = "date";

    /// <summary>只選時間。A time only.</summary>
    public const string ModeTime = "time";

    /// <summary>日期與時間一起選。A date and a time together.</summary>
    public const string ModeDatetime = "datetime";

    /// <summary>
    /// 建立日期 / 時間選擇器動作。
    /// Creates a datetime picker action.
    /// </summary>
    /// <param name="data">回傳給 webhook 的資料。The data posted back to the webhook.</param>
    /// <param name="mode">
    /// <see cref="ModeDate"/>、<see cref="ModeTime"/> 或 <see cref="ModeDatetime"/> 其中之一。
    /// One of <see cref="ModeDate"/>, <see cref="ModeTime"/>, or <see cref="ModeDatetime"/>.
    /// </param>
    /// <param name="initial">
    /// 初始值;格式隨 <paramref name="mode"/> 而定(<c>yyyy-MM-dd</c>、<c>HH:mm</c>、<c>yyyy-MM-ddTHH:mm</c>)。
    /// The initial value, in the format that goes with <paramref name="mode"/>: <c>yyyy-MM-dd</c>,
    /// <c>HH:mm</c>, or <c>yyyy-MM-ddTHH:mm</c>.
    /// </param>
    /// <param name="max">可選的最大值。The largest selectable value.</param>
    /// <param name="min">可選的最小值。The smallest selectable value.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="data"/> 為空白,或 <paramref name="mode"/> 不是三種模式之一時擲出。
    /// Thrown when <paramref name="data"/> is blank, or <paramref name="mode"/> is none of the three modes.
    /// </exception>
    public DatetimePickerAction(string data, string mode, string? initial = null, string? max = null, string? min = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);

        if (mode is not (ModeDate or ModeTime or ModeDatetime))
        {
            // 在這裡擋下來,而不是等 LINE 回一個只說「內容有錯」的 400:模式字串打錯
            // (例如 dateTime)在 JSON 上看不出問題,只有 LINE 認得出來。
            // Refused here rather than as a 400 from LINE saying only that the body is wrong: a mistyped mode —
            // dateTime, say — looks perfectly fine in the JSON, and only LINE can tell.
            throw new ArgumentException(
                $"模式必須是 {ModeDate}、{ModeTime} 或 {ModeDatetime}。The mode must be {ModeDate}, {ModeTime}, or {ModeDatetime}.",
                nameof(mode));
        }

        Data = data;
        Mode = mode;
        Initial = initial;
        Max = max;
        Min = min;
    }

    /// <inheritdoc />
    public override string Type => "datetimepicker";

    /// <summary>
    /// 回傳給 webhook 的資料。
    /// The data posted back to the webhook.
    /// </summary>
    public string Data { get; }

    /// <summary>
    /// 選擇器模式。
    /// The picker's mode.
    /// </summary>
    public string Mode { get; }

    /// <summary>
    /// 初始值。
    /// The initial value.
    /// </summary>
    public string? Initial { get; }

    /// <summary>
    /// 可選的最大值。
    /// The largest selectable value.
    /// </summary>
    public string? Max { get; }

    /// <summary>
    /// 可選的最小值。
    /// The smallest selectable value.
    /// </summary>
    public string? Min { get; }

    /// <summary>
    /// 檢查 <c>data</c> 長度、<c>initial</c> / <c>min</c> / <c>max</c> 的格式是否符合 <see cref="Mode"/>,以及 <c>max</c> 不早於 <c>min</c>。
    /// Checks the length of <c>data</c>, that <c>initial</c>, <c>min</c> and <c>max</c> match the format of
    /// <see cref="Mode"/>, and that <c>max</c> is not earlier than <c>min</c>.
    /// </summary>
    /// <param name="location">動作所在位置的描述。A description of where the action sits.</param>
    /// <returns>通過時為成功。Success when it passes.</returns>
    /// <remarks>
    /// 模式本身不在這裡檢查:建構子已經擋掉 date / time / datetime 以外的值。
    /// The mode itself is not checked here: the constructor already refuses anything but date, time, or datetime.
    /// </remarks>
    internal override Result ValidateBody(string location)
    {
        var where = $"{location}(type=datetimepicker)";
        if (LineMessageValidation.IsTooLong(Data, LineMessagingLimits.MaxPostbackDataLength))
        {
            return LineMessageValidation.InvalidAction(
                LineMessageValidation.TooLong($"{where}的 data", Data, LineMessagingLimits.MaxPostbackDataLength));
        }

        var format = FormatOf(Mode);
        DateTime? min = null;
        DateTime? max = null;
        foreach (var (name, value) in new[] { ("initial", Initial), ("min", Min), ("max", Max) })
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (!DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return LineMessageValidation.InvalidAction(
                    $"{where}的 {name} 格式不符:mode={Mode} 時必須是 {format},實際是:{LineMessageValidation.Quote(value)}。The {name} of {where} has the wrong format: with mode={Mode} it must be {format}, but is: {LineMessageValidation.Quote(value)}.");
            }

            if (name == "min")
            {
                min = parsed;
            }
            else if (name == "max")
            {
                max = parsed;
            }
        }

        return min is { } lower && max is { } upper && upper < lower
            ? LineMessageValidation.InvalidAction(
                $"{where}的 max({Max})早於 min({Min})。The max ({Max}) of {where} is earlier than its min ({Min}).")
            : Result.Success();
    }

    /// <summary>
    /// 取得某個模式對應的時間格式。
    /// Gets the time format that goes with a mode.
    /// </summary>
    /// <param name="mode">模式。The mode.</param>
    /// <returns>格式字串。The format string.</returns>
    private static string FormatOf(string mode) => mode switch
    {
        ModeDate => "yyyy-MM-dd",
        ModeTime => "HH:mm",
        _ => "yyyy-MM-ddTHH:mm",
    };

    /// <inheritdoc />
    internal override void WriteBody(Utf8JsonWriter writer)
    {
        writer.WriteString("data", Data);
        writer.WriteString("mode", Mode);

        if (!string.IsNullOrWhiteSpace(Initial))
        {
            writer.WriteString("initial", Initial);
        }

        if (!string.IsNullOrWhiteSpace(Max))
        {
            writer.WriteString("max", Max);
        }

        if (!string.IsNullOrWhiteSpace(Min))
        {
            writer.WriteString("min", Min);
        }
    }
}
