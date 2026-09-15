using System.Text.Json;

namespace Ozakboy.Line.Messaging.Actions;

/// <summary>
/// 開啟網址的動作。
/// An action that opens a URL.
/// </summary>
public sealed class UriAction : LineAction
{
    /// <summary>
    /// 建立開啟網址的動作。
    /// Creates a URL action.
    /// </summary>
    /// <param name="uri">要開啟的位址。The address to open.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="uri"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="uri"/> is <see langword="null"/> or blank.
    /// </exception>
    public UriAction(string uri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        Uri = uri;
    }

    /// <inheritdoc />
    public override string Type => "uri";

    /// <summary>
    /// 要開啟的位址。
    /// The address to open.
    /// </summary>
    public string Uri { get; }

    /// <summary>
    /// 桌機版 LINE 專用的位址;不設定時桌機也開 <see cref="Uri"/>。
    /// The address for LINE's desktop clients; without one the desktop opens <see cref="Uri"/> too.
    /// </summary>
    /// <remarks>
    /// 需要分開設定的典型情況是 <see cref="Uri"/> 指向 LIFF 或 <c>line://</c> 這類只有手機認得的位址 ——
    /// 桌機開起來是一片空白或一個錯誤頁,而使用者只會覺得「你們的連結壞了」。
    /// The case for setting it is a <see cref="Uri"/> pointing at a LIFF app or a <c>line://</c> address only a
    /// phone understands: on the desktop that opens a blank page or an error, and what the user sees is a broken
    /// link.
    /// </remarks>
    public string? AltUriDesktop { get; set; }

    /// <inheritdoc />
    internal override void WriteBody(Utf8JsonWriter writer)
    {
        writer.WriteString("uri", Uri);

        if (!string.IsNullOrWhiteSpace(AltUriDesktop))
        {
            // LINE 的欄位是巢狀的 altUri.desktop,不是平的 altUriDesktop。
            // LINE's field is the nested altUri.desktop, not a flat altUriDesktop.
            writer.WriteStartObject("altUri");
            writer.WriteString("desktop", AltUriDesktop);
            writer.WriteEndObject();
        }
    }
}
