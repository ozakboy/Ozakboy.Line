namespace Ozakboy.Line.Webhook;

/// <summary>
/// webhook 事件的型別字串。
/// The type strings of webhook events.
/// </summary>
/// <remarks>
/// 這裡列的是本套件寫作時 LINE 已公布的型別。LINE 隨時可能新增,因此
/// <see cref="LineWebhookEvent.Type"/> 是<b>字串</b>而不是列舉 —— 收到沒見過的型別時,
/// 事件仍然完整送達處理常式,原始 JSON 也還在 <see cref="LineWebhookEvent.Raw"/> 裡,
/// 而不是在反序列化那一步就被擋掉。
/// These are the types LINE had published when this package was written. LINE may add more at any time, which is
/// why <see cref="LineWebhookEvent.Type"/> is a <b>string</b> rather than an enum: an unfamiliar type still
/// reaches the handler whole, with its original JSON in <see cref="LineWebhookEvent.Raw"/>, instead of being
/// refused at deserialisation.
/// </remarks>
public static class LineWebhookEventTypes
{
    /// <summary>使用者傳來訊息。A message from the user.</summary>
    public const string Message = "message";

    /// <summary>使用者加入好友或解除封鎖。The user added the account or unblocked it.</summary>
    public const string Follow = "follow";

    /// <summary>使用者封鎖了官方帳號。The user blocked the account.</summary>
    public const string Unfollow = "unfollow";

    /// <summary>官方帳號被加入群組或聊天室。The account was added to a group or room.</summary>
    public const string Join = "join";

    /// <summary>官方帳號被移出群組或聊天室。The account was removed from a group or room.</summary>
    public const string Leave = "leave";

    /// <summary>有成員加入群組或聊天室。A member joined a group or room.</summary>
    public const string MemberJoined = "memberJoined";

    /// <summary>有成員離開群組或聊天室。A member left a group or room.</summary>
    public const string MemberLeft = "memberLeft";

    /// <summary>使用者觸發了回傳動作。The user triggered a postback action.</summary>
    public const string Postback = "postback";

    /// <summary>使用者進入或離開 beacon 範圍。The user entered or left a beacon's range.</summary>
    public const string Beacon = "beacon";

    /// <summary>帳號連結完成。An account link completed.</summary>
    public const string AccountLink = "accountLink";

    /// <summary>使用者收回了訊息。The user unsent a message.</summary>
    public const string Unsend = "unsend";

    /// <summary>使用者看完了影片訊息。The user finished watching a video message.</summary>
    public const string VideoPlayComplete = "videoPlayComplete";

    /// <summary>LINE Things 裝置事件。A LINE Things device event.</summary>
    public const string Things = "things";
}
