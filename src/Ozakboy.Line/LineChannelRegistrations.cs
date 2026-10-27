using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ozakboy.Line;

/// <summary>
/// 具名 channel 的設定委派:帶著名稱,讓後來的註冊能找到並取代先前同名的那一個。
/// A named channel's configure delegate, carrying its name so a later registration can find and replace the
/// earlier one of the same name.
/// </summary>
/// <typeparam name="TOptions">選項型別。The options type.</typeparam>
/// <param name="name">channel 名稱。The channel name.</param>
/// <param name="configure">設定委派。The configure delegate.</param>
internal sealed class LineChannelConfigureOptions<TOptions>(string name, Action<TOptions> configure)
    : ConfigureNamedOptions<TOptions>(name, configure)
    where TOptions : class
{
}

/// <summary>
/// 一個已註冊的具名 channel;工廠靠它列舉名稱。
/// A registered named channel, from which the factories enumerate names.
/// </summary>
/// <param name="ClientType">用戶端的服務型別。The client's service type.</param>
/// <param name="Name">channel 名稱。The channel name.</param>
internal sealed record LineChannelRegistration(Type ClientType, string Name);

/// <summary>
/// 具名 channel 註冊的共用工具。
/// Shared helpers for registering named channels.
/// </summary>
/// <remarks>
/// <b>「一個名字一個用戶端」的做法是取代而不是疊加。</b>選項走 <c>Configure</c> 疊加時,兩個委派會依序都跑,
/// 後者沒設的欄位會留著前者的值 —— 換掉 channel 時舊的 secret 殘留是最糟的結果;所以同名的舊設定委派、
/// keyed 服務與登記項目都先移除再加。
/// <b>One name, one client — by replacement, not by stacking.</b> With stacked <c>Configure</c> calls both
/// delegates run in order and a field the later one leaves unset keeps the earlier value — a stale secret
/// surviving a channel swap is the worst outcome. So the earlier configure delegate, keyed service and
/// registration entry of the same name are removed before the new ones are added.
/// </remarks>
internal static class LineChannelRegistrations
{
    /// <summary>
    /// 以取代的方式登記某個名稱的選項設定。
    /// Registers the options configuration for a name, replacing any earlier one.
    /// </summary>
    /// <typeparam name="TOptions">選項型別。The options type.</typeparam>
    /// <param name="services">服務集合。The service collection.</param>
    /// <param name="name">channel 名稱。The channel name.</param>
    /// <param name="configure">設定委派。The configure delegate.</param>
    internal static void ReplaceConfigure<TOptions>(IServiceCollection services, string name, Action<TOptions> configure)
        where TOptions : class
    {
        services.AddOptions();
        RemoveWhere(
            services,
            descriptor => descriptor.ServiceType == typeof(IConfigureOptions<TOptions>)
                && descriptor.ImplementationInstance is LineChannelConfigureOptions<TOptions> existing
                && existing.Name == name);
        services.AddSingleton<IConfigureOptions<TOptions>>(new LineChannelConfigureOptions<TOptions>(name, configure));
    }

    /// <summary>
    /// 以取代的方式登記某個名稱的 keyed 用戶端,並更新登記項目。
    /// Registers the keyed client for a name, replacing any earlier one, and updates the registration entry.
    /// </summary>
    /// <typeparam name="TClient">用戶端的服務型別。The client's service type.</typeparam>
    /// <param name="services">服務集合。The service collection.</param>
    /// <param name="name">channel 名稱。The channel name.</param>
    /// <param name="factory">建立用戶端的工廠。The factory that creates the client.</param>
    internal static void ReplaceKeyed<TClient>(
        IServiceCollection services,
        string name,
        Func<IServiceProvider, object?, TClient> factory)
        where TClient : class
    {
        RemoveWhere(
            services,
            descriptor => descriptor.IsKeyedService
                && descriptor.ServiceType == typeof(TClient)
                && Equals(descriptor.ServiceKey, name));
        services.AddKeyedSingleton<TClient>(name, factory);

        RemoveWhere(
            services,
            descriptor => descriptor.ImplementationInstance is LineChannelRegistration existing
                && existing.ClientType == typeof(TClient)
                && existing.Name == name);
        services.AddSingleton(new LineChannelRegistration(typeof(TClient), name));
    }

    private static void RemoveWhere(IServiceCollection services, Func<ServiceDescriptor, bool> predicate)
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            if (predicate(services[i]))
            {
                services.RemoveAt(i);
            }
        }
    }
}
