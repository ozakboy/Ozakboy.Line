namespace Ozakboy.Line.Messaging;

/// <summary>
/// 訊息驗證端點要模擬的送出方式。
/// The kind of send a message validation call simulates.
/// </summary>
/// <remarks>
/// LINE 對每一種送出方式各有一個驗證端點(<c>/v2/bot/message/validate/{push|multicast|broadcast|reply|narrowcast}</c>),
/// 規則略有不同 —— 例如 reply 才接受某些只在回覆時有效的欄位。這個列舉只決定路徑的最後一段。
/// LINE has one validation endpoint per kind of send
/// (<c>/v2/bot/message/validate/{push|multicast|broadcast|reply|narrowcast}</c>) and the rules differ slightly:
/// some fields are valid only in a reply, for one. This enum only decides the last path segment.
/// </remarks>
public enum LineMessageValidationTarget
{
    /// <summary>推播給單一對象。A push to one recipient.</summary>
    Push = 0,

    /// <summary>推播給多個使用者。A multicast.</summary>
    Multicast = 1,

    /// <summary>廣播給所有好友。A broadcast.</summary>
    Broadcast = 2,

    /// <summary>以 reply token 回覆。A reply.</summary>
    Reply = 3,

    /// <summary>分眾推播。A narrowcast.</summary>
    Narrowcast = 4,
}
