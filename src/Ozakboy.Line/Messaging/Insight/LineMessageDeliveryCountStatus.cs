using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Insight;

/// <summary>
/// 投遞數統計的狀態。
/// The status of a delivery count.
/// </summary>
[JsonConverter(typeof(LineMessageDeliveryCountStatusConverter))]
public enum LineMessageDeliveryCountStatus
{
    /// <summary>LINE 回了本套件不認得的狀態。LINE answered with a status this package does not know.</summary>
    Unknown = 0,

    /// <summary>統計已就緒(<c>ready</c>)。The figure is ready.</summary>
    Ready = 1,

    /// <summary>還沒統計完(<c>unready</c>)。Not counted yet.</summary>
    Unready = 2,

    /// <summary>查不到(<c>out_of_service</c>)。Not available.</summary>
    OutOfService = 3,
}

/// <summary>
/// <see cref="LineMessageDeliveryCountStatus"/> 的轉換器:認不得的字串回 <see cref="LineMessageDeliveryCountStatus.Unknown"/> 而不擲例外。
/// The converter of <see cref="LineMessageDeliveryCountStatus"/>: a string it does not know becomes
/// <see cref="LineMessageDeliveryCountStatus.Unknown"/> instead of throwing.
/// </summary>
/// <remarks>
/// LINE 日後新增狀態時,擲例外會讓整個統計回應讀不出來;退到 Unknown 至少還拿得到數字。
/// Should LINE add a status later, throwing would make the whole statistics response unreadable; falling back to
/// Unknown still lets the figure through.
/// </remarks>
internal sealed class LineMessageDeliveryCountStatusConverter : JsonConverter<LineMessageDeliveryCountStatus>
{
    /// <inheritdoc />
    public override LineMessageDeliveryCountStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() switch
            {
                "ready" => LineMessageDeliveryCountStatus.Ready,
                "unready" => LineMessageDeliveryCountStatus.Unready,
                "out_of_service" => LineMessageDeliveryCountStatus.OutOfService,
                _ => LineMessageDeliveryCountStatus.Unknown,
            }
            : LineMessageDeliveryCountStatus.Unknown;

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, LineMessageDeliveryCountStatus value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStringValue(value switch
        {
            LineMessageDeliveryCountStatus.Ready => "ready",
            LineMessageDeliveryCountStatus.Unready => "unready",
            LineMessageDeliveryCountStatus.OutOfService => "out_of_service",
            _ => "unknown",
        });
    }
}
