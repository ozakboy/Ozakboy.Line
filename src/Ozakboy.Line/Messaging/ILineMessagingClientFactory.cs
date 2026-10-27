namespace Ozakboy.Line.Messaging;

/// <summary>
/// 依 channel 名稱取得 <see cref="ILineMessagingClient"/> 的工廠(多 channel 情境用)。
/// A factory that returns the <see cref="ILineMessagingClient"/> for a channel name, for multi-channel setups.
/// </summary>
/// <remarks>
/// 名稱由 <c>AddLineMessaging(name, …)</c> 註冊;未具名的 <c>AddLineMessaging(…)</c> 登記的是空字串名稱。
/// 同一個名稱永遠拿到同一個(singleton)用戶端。
/// Names are registered by <c>AddLineMessaging(name, …)</c>; the unnamed <c>AddLineMessaging(…)</c> registers the
/// empty-string name. The same name always yields the same (singleton) client.
/// </remarks>
public interface ILineMessagingClientFactory
{
    /// <summary>
    /// 已註冊的 channel 名稱(依註冊順序)。
    /// The registered channel names, in registration order.
    /// </summary>
    IReadOnlyCollection<string> ChannelNames { get; }

    /// <summary>
    /// 指定名稱是否已註冊。
    /// Whether the given name is registered.
    /// </summary>
    /// <param name="channelName">channel 名稱。The channel name.</param>
    /// <returns>已註冊為 <see langword="true"/>。<see langword="true"/> when registered.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="channelName"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="channelName"/> is <see langword="null"/>.
    /// </exception>
    bool Contains(string channelName);

    /// <summary>
    /// 取得指定名稱的用戶端。
    /// Gets the client of the given name.
    /// </summary>
    /// <param name="channelName">channel 名稱;空字串是預設 channel。The channel name; the empty string is the default channel.</param>
    /// <returns>用戶端。The client.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="channelName"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="channelName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// 名稱未註冊時擲出,訊息列出已註冊的名稱。
    /// Thrown when the name is not registered; the message lists the registered names.
    /// </exception>
    ILineMessagingClient Get(string channelName);
}
