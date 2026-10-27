using Microsoft.Extensions.DependencyInjection;

namespace Ozakboy.Line.Messaging;

/// <summary>
/// <see cref="ILineMessagingClientFactory"/> 的實作:以 keyed 服務解析各 channel 的用戶端。
/// The implementation of <see cref="ILineMessagingClientFactory"/>, resolving each channel's client as a keyed
/// service.
/// </summary>
internal sealed class LineMessagingClientFactory : ILineMessagingClientFactory
{
    private readonly IServiceProvider _provider;
    private readonly string[] _names;

    /// <summary>
    /// 建立工廠。
    /// Creates the factory.
    /// </summary>
    /// <param name="provider">服務提供者。The service provider.</param>
    /// <param name="registrations">所有已登記的 channel。Every registered channel.</param>
    public LineMessagingClientFactory(IServiceProvider provider, IEnumerable<LineChannelRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(registrations);

        _provider = provider;
        _names = [.. registrations
            .Where(registration => registration.ClientType == typeof(ILineMessagingClient))
            .Select(registration => registration.Name)];
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> ChannelNames => _names;

    /// <inheritdoc />
    public bool Contains(string channelName)
    {
        ArgumentNullException.ThrowIfNull(channelName);
        return Array.IndexOf(_names, channelName) >= 0;
    }

    /// <inheritdoc />
    public ILineMessagingClient Get(string channelName)
    {
        ArgumentNullException.ThrowIfNull(channelName);

        return _provider.GetKeyedService<ILineMessagingClient>(channelName)
            ?? throw new InvalidOperationException(
                $"沒有名為「{channelName}」的 LINE Messaging channel。已註冊:{LineChannelNames.Describe(_names)}。"
                + $" No LINE Messaging channel is registered under the name \"{channelName}\". Registered: {LineChannelNames.Describe(_names)}.");
    }
}
