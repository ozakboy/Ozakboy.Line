using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.RichMenu;

/// <summary>
/// 圖文選單批次作業的階段。
/// The phase of a rich menu batch.
/// </summary>
[JsonConverter(typeof(LineRichMenuBatchPhaseConverter))]
public enum LineRichMenuBatchPhase
{
    /// <summary>LINE 回了本套件不認得的階段。LINE answered with a phase this package does not know.</summary>
    Unknown = 0,

    /// <summary>進行中(<c>ongoing</c>)。Still running.</summary>
    Ongoing = 1,

    /// <summary>已完成(<c>succeeded</c>)。Finished successfully.</summary>
    Succeeded = 2,

    /// <summary>失敗(<c>failed</c>)。Failed.</summary>
    Failed = 3,
}

/// <summary>
/// <see cref="LineRichMenuBatchPhase"/> 的轉換器:認不得的字串回 <see cref="LineRichMenuBatchPhase.Unknown"/> 而不擲例外。
/// The converter of <see cref="LineRichMenuBatchPhase"/>: a string it does not know becomes
/// <see cref="LineRichMenuBatchPhase.Unknown"/> instead of throwing.
/// </summary>
internal sealed class LineRichMenuBatchPhaseConverter : JsonConverter<LineRichMenuBatchPhase>
{
    /// <inheritdoc />
    public override LineRichMenuBatchPhase Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() switch
            {
                "ongoing" => LineRichMenuBatchPhase.Ongoing,
                "succeeded" => LineRichMenuBatchPhase.Succeeded,
                "failed" => LineRichMenuBatchPhase.Failed,
                _ => LineRichMenuBatchPhase.Unknown,
            }
            : LineRichMenuBatchPhase.Unknown;

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, LineRichMenuBatchPhase value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStringValue(value switch
        {
            LineRichMenuBatchPhase.Ongoing => "ongoing",
            LineRichMenuBatchPhase.Succeeded => "succeeded",
            LineRichMenuBatchPhase.Failed => "failed",
            _ => "unknown",
        });
    }
}

/// <summary>
/// 一次圖文選單批次作業的進度。
/// The progress of one rich menu batch.
/// </summary>
/// <remarks>
/// 對應 LINE 進度端點的 <c>phase</c>、<c>acceptedTime</c> 與 <c>completedTime</c>。批次是<b>非同步</b>的:
/// 送出端點回 202 只代表收下,是否全部完成要看這個型別。
/// Maps to <c>phase</c>, <c>acceptedTime</c> and <c>completedTime</c> of LINE's progress endpoint. A batch is
/// <b>asynchronous</b>: the send endpoint's 202 means only that it was accepted, and whether it all finished is
/// what this type reports.
/// </remarks>
public sealed class LineRichMenuBatchProgress
{
    /// <summary>
    /// 目前階段。
    /// The current phase.
    /// </summary>
    [JsonPropertyName("phase")]
    public LineRichMenuBatchPhase Phase { get; init; }

    /// <summary>
    /// LINE 收下請求的時間(ISO 8601)。
    /// When LINE accepted the request, as ISO 8601.
    /// </summary>
    [JsonPropertyName("acceptedTime")]
    public string AcceptedTime { get; init; } = string.Empty;

    /// <summary>
    /// 完成的時間(ISO 8601);未完成時為 <see langword="null"/>。
    /// When it completed, as ISO 8601, or <see langword="null"/> while unfinished.
    /// </summary>
    [JsonPropertyName("completedTime")]
    public string? CompletedTime { get; init; }
}
