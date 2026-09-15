using System.Text.Json;

namespace Ozakboy.Line.Messaging.Messages;

/// <summary>
/// 位置訊息。
/// A location message.
/// </summary>
public sealed class LocationMessage : LineMessage
{
    /// <summary>
    /// 建立位置訊息。
    /// Creates a location message.
    /// </summary>
    /// <param name="title">地點名稱。The place's title.</param>
    /// <param name="address">地址。The address.</param>
    /// <param name="latitude">緯度。The latitude.</param>
    /// <param name="longitude">經度。The longitude.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="title"/> 或 <paramref name="address"/> 為 <see langword="null"/> 或空白時擲出。
    /// Thrown when <paramref name="title"/> or <paramref name="address"/> is <see langword="null"/> or blank.
    /// </exception>
    public LocationMessage(string title, string address, double latitude, double longitude)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        Title = title;
        Address = address;
        Latitude = latitude;
        Longitude = longitude;
    }

    /// <inheritdoc />
    public override string Type => "location";

    /// <summary>
    /// 地點名稱。
    /// The place's title.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// 地址。
    /// The address.
    /// </summary>
    public string Address { get; }

    /// <summary>
    /// 緯度。
    /// The latitude.
    /// </summary>
    public double Latitude { get; }

    /// <summary>
    /// 經度。
    /// The longitude.
    /// </summary>
    public double Longitude { get; }

    /// <inheritdoc />
    internal override void WriteBody(Utf8JsonWriter writer)
    {
        writer.WriteString("title", Title);
        writer.WriteString("address", Address);
        writer.WriteNumber("latitude", Latitude);
        writer.WriteNumber("longitude", Longitude);
    }
}
