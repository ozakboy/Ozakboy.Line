using System.Text.Json;

namespace Ozakboy.Line.Messaging.Messages;

/// <summary>
/// 語音訊息。
/// An audio message.
/// </summary>
public sealed class AudioMessage : LineMessage
{
    /// <summary>
    /// 建立語音訊息。
    /// Creates an audio message.
    /// </summary>
    /// <param name="originalContentUrl">音檔位址。The audio address.</param>
    /// <param name="durationMilliseconds">
    /// 音檔長度(毫秒)。LINE 用它畫進度條,填錯不會有錯誤訊息,只會讓進度條對不上。
    /// The length in milliseconds. LINE draws its progress bar from this; a wrong value raises no error and
    /// simply makes the bar disagree with the audio.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="originalContentUrl"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="originalContentUrl"/> is <see langword="null"/> or blank.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="durationMilliseconds"/> 不為正數時擲出。
    /// Thrown when <paramref name="durationMilliseconds"/> is not positive.
    /// </exception>
    public AudioMessage(string originalContentUrl, int durationMilliseconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalContentUrl);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(durationMilliseconds);

        OriginalContentUrl = originalContentUrl;
        DurationMilliseconds = durationMilliseconds;
    }

    /// <inheritdoc />
    public override string Type => "audio";

    /// <summary>
    /// 音檔位址。
    /// The audio address.
    /// </summary>
    public string OriginalContentUrl { get; }

    /// <summary>
    /// 音檔長度(毫秒)。
    /// The length in milliseconds.
    /// </summary>
    public int DurationMilliseconds { get; }

    /// <inheritdoc />
    internal override void WriteBody(Utf8JsonWriter writer)
    {
        writer.WriteString("originalContentUrl", OriginalContentUrl);
        writer.WriteNumber("duration", DurationMilliseconds);
    }
}
