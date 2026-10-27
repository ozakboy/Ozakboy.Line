using System.Text.Json.Serialization;

namespace Ozakboy.Line.Liff;

/// <summary>
/// LIFF 應用程式要開啟的裝置功能。
/// The device features a LIFF app turns on.
/// </summary>
public sealed class LineLiffFeatures
{
    /// <summary>
    /// 是否啟用藍牙(LINE Beacon / BLE)。
    /// Whether Bluetooth LE is enabled.
    /// </summary>
    [JsonPropertyName("ble")]
    public bool? Ble { get; init; }

    /// <summary>
    /// 是否在 LIFF 瀏覽器啟用 QR code 掃描。
    /// Whether the QR code reader is enabled in the LIFF browser.
    /// </summary>
    [JsonPropertyName("qrCode")]
    public bool? QrCode { get; init; }
}
