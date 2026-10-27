namespace Ozakboy.Line.Webhook;

/// <summary>
/// <c>beacon</c> 事件的 <see cref="LineWebhookBeacon.Type"/> 字串。
/// The <see cref="LineWebhookBeacon.Type"/> strings of a <c>beacon</c> event.
/// </summary>
public static class LineWebhookBeaconTypes
{
    /// <summary>進入 beacon 範圍。Entered the beacon's range.</summary>
    public const string Enter = "enter";

    /// <summary>離開 beacon 範圍。Left the beacon's range.</summary>
    public const string Leave = "leave";

    /// <summary>點了 beacon 橫幅。Tapped the beacon banner.</summary>
    public const string Banner = "banner";

    /// <summary>停留在 beacon 範圍內。Stayed within the beacon's range.</summary>
    public const string Stay = "stay";
}
