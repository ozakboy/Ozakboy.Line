using Microsoft.Extensions.DependencyInjection;

namespace Ozakboy.Line.Liff;

/// <summary>
/// <see cref="ILineLiffClientFactory"/> 的實作:以 keyed 服務解析各 channel 的用戶端。
/// The implementation of <see cref="ILineLiffClientFactory"/>, resolving each channel's client as a keyed
/// service.
/// </summary>
internal sealed class LineLiffClientFactory : ILineLiffClientFactory
{
    private readonly IServiceProvider _provider;
    private readonly string[] _names;

    /// <summary>
    /// 建立工廠。
    /// Creates the factory.
    /// </summary>
    /// <param name="provider">服務提供者。The service provider.</param>
    /// <param name="registrations">所有已登記的 channel。Every registered channel.</param>
    public LineLiffClientFactory(IServiceProvider provider, IEnumerable<LineChannelRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(registrations);

        _provider = provider;
        _names = [.. registrations
            .Where(registration => registration.ClientType == typeof(ILineLiffClient))
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
    public ILineLiffClient Get(string channelName)
    {
        ArgumentNullException.ThrowIfNull(channelName);

        return _provider.GetKeyedService<ILineLiffClient>(channelName)
            ?? throw new InvalidOperationException(
                $"沒有名為「{channelName}」的 LIFF channel。已註冊:{LineChannelNames.Describe(_names)}。"
                + $" No LIFF channel is registered under the name \"{channelName}\". Registered: {LineChannelNames.Describe(_names)}.");
    }
}
