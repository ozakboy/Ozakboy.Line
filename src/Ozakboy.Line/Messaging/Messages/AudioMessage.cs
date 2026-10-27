using System.Text.Json;
using Ozakboy.Core.Abstractions;

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

    /// <summary>
    /// 送出前的本地檢查:位址必須是 https:// 而且不超過 2000 字(長度必須大於 0 則已由建構子保證)。
    /// The local check before sending: the address must be https:// and at most 2000 characters (a duration above 0 is already guaranteed by the constructor).
    /// </summary>
    /// <returns>
    /// 通過時為成功,否則為 <see cref="LineErrorCodes.InvalidMessage"/> 失敗(快速回覆的問題照該處的代碼)。
    /// Success when it passes, otherwise an <see cref="LineErrorCodes.InvalidMessage"/> failure (quick reply problems keep their own codes).
    /// </returns>
    public override Result Validate()
    {
        var baseResult = base.Validate();
        if (baseResult.IsFailure)
        {
            return baseResult;
        }

        return LineMessageValidation.CheckMediaUrl("語音訊息的 originalContentUrl", OriginalContentUrl) is { } original
            ? LineMessageValidation.InvalidMessage(original)
            : Result.Success();
    }

    /// <inheritdoc />
    internal override void WriteBody(Utf8JsonWriter writer)
    {
        writer.WriteString("originalContentUrl", OriginalContentUrl);
        writer.WriteNumber("duration", DurationMilliseconds);
    }
}
