using System.Text.Json.Serialization;

namespace Ozakboy.Line.Login;

/// <summary>
/// 好友狀態端點的回應形狀,只用於反序列化。
/// The friendship endpoint's response shape, used only for deserialisation.
/// </summary>
/// <remarks>
/// 對外不暴露這個型別:回應裡只有一個布林,包成一個公開型別只是讓呼叫端多寫一次 <c>.FriendFlag</c>。
/// This type is not exposed: the response carries a single boolean, and wrapping it in a public type would only
/// make callers write <c>.FriendFlag</c> once more.
/// </remarks>
internal sealed class LineFriendshipStatus
{
    /// <summary>
    /// 使用者是否已加官方帳號好友。
    /// Whether the user has added the official account as a friend.
    /// </summary>
    [JsonPropertyName("friendFlag")]
    public bool FriendFlag { get; init; }
}
