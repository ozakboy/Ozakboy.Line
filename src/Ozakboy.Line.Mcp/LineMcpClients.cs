using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Liff;
using Ozakboy.Line.Messaging;

namespace Ozakboy.Line.Mcp;

/// <summary>
/// 依 <see cref="LineMcpOptions.ChannelName"/> 取得 MCP 工具要操作的 Messaging 與 LIFF 用戶端。
/// Hands the MCP tools the Messaging and LIFF clients of the channel named by
/// <see cref="LineMcpOptions.ChannelName"/>.
/// </summary>
/// <remarks>
/// <para>
/// 找不到指定的 channel 時,<see cref="Messaging"/> 給的是一個<b>每個方法都回
/// <see cref="LineErrorCodes.NotConfigured"/> 失敗</b>的替身,而不是擲例外或悄悄退回別的 channel ——
/// 送出類工具拿錯帳號比送不出去嚴重得多。
/// When the named channel does not exist, <see cref="Messaging"/> is a stand-in whose every method answers a
/// <see cref="LineErrorCodes.NotConfigured"/> failure, rather than throwing or quietly falling back to another
/// channel: a sending tool using the wrong account is far worse than one that cannot send.
/// </para>
/// <para>
/// 預設 channel(空字串)優先用直接註冊的 <see cref="ILineMessagingClient"/> / <see cref="ILineLiffClient"/>,
/// 因此 0.4.0 之前、沒有工廠的宿主(或替換了用戶端的宿主)照舊可用;具名 channel 一律走工廠。
/// The default channel (the empty string) prefers a directly registered <see cref="ILineMessagingClient"/> /
/// <see cref="ILineLiffClient"/>, so hosts from before 0.4.0 — with no factory, or with a replaced client —
/// carry on working; a named channel always goes through the factory.
/// </para>
/// </remarks>
public sealed class LineMcpClients
{
    private readonly IServiceProvider _provider;
    private readonly string _channelName;

    /// <summary>
    /// 建立。
    /// Creates the resolver.
    /// </summary>
    /// <param name="provider">服務提供者。The service provider.</param>
    /// <param name="options">MCP 設定。The MCP settings.</param>
    /// <exception cref="ArgumentNullException">
    /// 任一參數為 <see langword="null"/> 時擲出。Thrown when any argument is <see langword="null"/>.
    /// </exception>
    public LineMcpClients(IServiceProvider provider, IOptions<LineMcpOptions> options)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(options);

        _provider = provider;
        _channelName = options.Value.ChannelName ?? string.Empty;
    }

    /// <summary>
    /// 工具操作的 channel 名稱;空字串是預設 channel。
    /// The channel the tools operate on; the empty string is the default channel.
    /// </summary>
    public string ChannelName => _channelName;

    /// <summary>
    /// 這個 channel 是否有可用的 Messaging 用戶端。
    /// Whether the channel has a usable Messaging client.
    /// </summary>
    public bool IsMessagingAvailable => FindMessaging() is not null;

    /// <summary>
    /// Messaging 用戶端;channel 不存在時為每個方法都回 <see cref="LineErrorCodes.NotConfigured"/> 失敗的替身。
    /// The Messaging client; a stand-in failing every method with <see cref="LineErrorCodes.NotConfigured"/> when the
    /// channel does not exist.
    /// </summary>
    public ILineMessagingClient Messaging =>
        FindMessaging() ?? LineMcpUnavailableMessagingClient.Create(_channelName);

    /// <summary>
    /// LIFF 用戶端;宿主沒有註冊 LIFF,或沒有這個名稱的 LIFF channel 時為 <see langword="null"/>。
    /// The LIFF client, or <see langword="null"/> when the host registered no LIFF, or none under this name.
    /// </summary>
    public ILineLiffClient? Liff
    {
        get
        {
            if (_channelName.Length == 0 && _provider.GetService<ILineLiffClient>() is { } direct)
            {
                return direct;
            }

            var factory = _provider.GetService<ILineLiffClientFactory>();
            return factory is not null && factory.Contains(_channelName) ? factory.Get(_channelName) : null;
        }
    }

    /// <summary>
    /// 找不到 channel 時的失敗;channel 可用時為 <see langword="null"/>。
    /// The failure for a channel that cannot be found, or <see langword="null"/> when the channel is usable.
    /// </summary>
    public Error? MessagingUnavailableError =>
        IsMessagingAvailable ? null : LineMcpUnavailableMessagingClient.CreateError(_channelName);

    private ILineMessagingClient? FindMessaging()
    {
        if (_channelName.Length == 0 && _provider.GetService<ILineMessagingClient>() is { } direct)
        {
            return direct;
        }

        var factory = _provider.GetService<ILineMessagingClientFactory>();
        return factory is not null && factory.Contains(_channelName) ? factory.Get(_channelName) : null;
    }
}
