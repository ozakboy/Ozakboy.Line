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

    /// <inheritdoc />
    internal override void WriteBody(Utf8JsonWriter writer) => writer.WriteString("uri", Uri);
}
