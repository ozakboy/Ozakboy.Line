namespace Ozakboy.Line.Webhook;

/// <summary>
/// 事件的來源。
/// Where an event came from.
/// </summary>
/// <remarks>
/// 三個識別碼欄位只會有其中一或兩個有值:一對一聊天只有 <see cref="UserId"/>,群組有
/// <see cref="GroupId"/> 與(通常)<see cref="UserId"/>。要回訊息時看 <see cref="Type"/> 決定要回給誰 ——
/// 在群組裡把訊息推給 <see cref="UserId"/>,是把群組對話變成私訊。
/// At most one or two of the three identifiers are ever set: a one-to-one chat carries only
/// <see cref="UserId"/>, while a group carries <see cref="GroupId"/> and usually <see cref="UserId"/> as well.
/// Read <see cref="Type"/> to decide where a reply goes — pushing to <see cref="UserId"/> from a group turns a
/// group conversation into a private message.
/// </remarks>
public sealed class LineWebhookSource
{
    /// <summary>
    /// 來源型別:<c>user</c>、<c>group</c> 或 <c>room</c>。
    /// The source type: <c>user</c>, <c>group</c>, or <c>room</c>.
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// 使用者識別碼。
    /// The user identifier.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// 群組識別碼。
    /// The group identifier.
    /// </summary>
    public string? GroupId { get; init; }

    /// <summary>
    /// 聊天室識別碼。
    /// The room identifier.
    /// </summary>
    public string? RoomId { get; init; }
}
