namespace Ozakboy.Line.AspNetCore.Webhook;

/// <summary>
/// 本套件放進 <c>HttpContext.Items</c> 的鍵名。
/// The keys this package writes into <c>HttpContext.Items</c>.
/// </summary>
/// <remarks>
/// 鍵名字串是<b>公開契約</b>:宿主的處理常式靠它取值,發佈之後不改。
/// These key strings are a <b>public contract</b>: a host's handler reads by them, and they do not change once
/// published.
/// </remarks>
public static class LineWebhookItems
{
    /// <summary>
    /// 自動回覆的處理結果,型別為 <see cref="Ozakboy.Line.AutoReply.LineAutoReplyOutcome"/>。
    /// The auto reply outcome, of type <see cref="Ozakboy.Line.AutoReply.LineAutoReplyOutcome"/>.
    /// </summary>
    /// <remarks>
    /// 只有在 <see cref="LineWebhookEndpointOptions.AutoReply"/> 為 <see langword="true"/> 時才會有值。
    /// 自動回覆本身失敗時也不會有值(失敗已記錄,處理常式照常收到事件)。
    /// It is present only when <see cref="LineWebhookEndpointOptions.AutoReply"/> is <see langword="true"/>, and
    /// absent when the auto reply itself failed — that failure is logged, and the handler still receives the
    /// event.
    /// </remarks>
    public const string AutoReplyOutcome = "Ozakboy.Line.AutoReplyOutcome";

    /// <summary>
    /// 驗簽通過的請求所屬的 channel 名稱(型別 <see cref="string"/>);預設 channel 為空字串。
    /// The name of the channel a signature-verified request belongs to (a <see cref="string"/>); an empty string
    /// for the default channel.
    /// </summary>
    /// <remarks>
    /// 只有在驗簽<b>通過</b>之後才放值:驗簽失敗(含未知 channel)的請求不會有這個鍵。
    /// 見 <see cref="LineWebhookEndpointOptions.ChannelName"/> 與 <see cref="LineWebhookEndpointOptions.ChannelRouteParameter"/>。
    /// It is set only <b>after</b> the signature verifies: a request that fails verification, an unknown channel
    /// included, carries no such key. See <see cref="LineWebhookEndpointOptions.ChannelName"/> and
    /// <see cref="LineWebhookEndpointOptions.ChannelRouteParameter"/>.
    /// </remarks>
    public const string ChannelName = "Ozakboy.Line.ChannelName";

    /// <summary>
    /// 本次請求有事件因維護模式被擋下時為 <see langword="true"/>(型別 <see cref="bool"/>);沒有被擋下就沒有這個鍵。
    /// <see langword="true"/> (a <see cref="bool"/>) when maintenance mode held back at least one event in this
    /// request; the key is absent otherwise.
    /// </summary>
    /// <remarks>
    /// 見 <see cref="LineWebhookEndpointOptions.Maintenance"/>。
    /// See <see cref="LineWebhookEndpointOptions.Maintenance"/>.
    /// </remarks>
    public const string MaintenanceSkipped = "Ozakboy.Line.MaintenanceSkipped";
}
