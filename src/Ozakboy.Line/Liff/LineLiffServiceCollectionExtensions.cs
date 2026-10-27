using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ozakboy.Http;

namespace Ozakboy.Line.Liff;

/// <summary>
/// 把 LIFF server API 用戶端註冊進相依注入容器。
/// Registers the LIFF server API client with the dependency injection container.
/// </summary>
public static class LineLiffServiceCollectionExtensions
{
    /// <summary>
    /// 註冊 LIFF server API 用戶端。
    /// Registers the LIFF server API client.
    /// </summary>
    /// <param name="services">服務集合。The service collection.</param>
    /// <param name="configure">設定 LIFF(LINE Login channel 的權杖)。Configures LIFF with the LINE Login channel's token.</param>
    /// <param name="configurePipeline">
    /// 額外調整 HTTP 管線(逾時、重試、日誌);為 <see langword="null"/> 時採用本套件的預設。
    /// Further adjusts the HTTP pipeline — timeouts, retries, logging — or <see langword="null"/> for this
    /// package's defaults.
    /// </param>
    /// <returns>服務集合本身。The service collection.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> 或 <paramref name="configure"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddLineLiff(
        this IServiceCollection services,
        Action<LineLiffOptions> configure,
        Action<HttpPipelineOptions>? configurePipeline = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        // 未具名註冊 = 預設 channel(空字串名稱)+ 一個 unkeyed 的 ILineLiffClient,後者就是 keyed("") 的同一個實例。
        // An unnamed registration is the default channel (the empty-string name) plus an unkeyed ILineLiffClient,
        // which resolves to the very same instance as keyed("").
        services.AddLineLiff(Options.DefaultName, configure, configurePipeline);
        services.AddSingleton<ILineLiffClient>(provider =>
            provider.GetRequiredKeyedService<ILineLiffClient>(Options.DefaultName));

        return services;
    }

    /// <summary>
    /// 從設定區段註冊 LIFF server API 用戶端。
    /// Registers the LIFF server API client from a configuration section.
    /// </summary>
    /// <param name="services">服務集合。The service collection.</param>
    /// <param name="section">設定區段。The configuration section.</param>
    /// <param name="configurePipeline">額外調整 HTTP 管線。Further adjusts the HTTP pipeline.</param>
    /// <returns>服務集合本身。The service collection.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> 或 <paramref name="section"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="services"/> or <paramref name="section"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddLineLiff(
        this IServiceCollection services,
        IConfiguration section,
        Action<HttpPipelineOptions>? configurePipeline = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(section);

        return services.AddLineLiff(section.Bind, configurePipeline);
    }

    /// <summary>
    /// 註冊一個具名的 LIFF server API 用戶端(多 channel)。
    /// Registers a named LIFF server API client, for multi-channel setups.
    /// </summary>
    /// <param name="services">服務集合。The service collection.</param>
    /// <param name="name">channel 名稱;空字串是預設 channel。The channel name; the empty string is the default channel.</param>
    /// <param name="configure">設定 LIFF(LINE Login channel 的權杖)。Configures LIFF with the LINE Login channel's token.</param>
    /// <param name="configurePipeline">額外調整這個 channel 的 HTTP 管線。Further adjusts this channel's HTTP pipeline.</param>
    /// <returns>服務集合本身。The service collection.</returns>
    /// <remarks>
    /// 做法與 <c>AddLineMessaging(name, …)</c> 相同:只註冊成 keyed 服務並可經 <see cref="ILineLiffClientFactory"/>
    /// 取得;一個名字一個用戶端,重複註冊以後者整個取代;每個 channel 有自己的具名 HttpClient
    /// (<see cref="LineHttpClientNames.ForLiff(string)"/>)。
    /// The same as <c>AddLineMessaging(name, …)</c>: registered only as a keyed service and reachable through
    /// <see cref="ILineLiffClientFactory"/>; one name, one client, with a repeated registration replacing the
    /// earlier one wholesale; each channel has its own named HttpClient
    /// (<see cref="LineHttpClientNames.ForLiff(string)"/>).
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/>、<paramref name="name"/> 或 <paramref name="configure"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="services"/>, <paramref name="name"/> or <paramref name="configure"/> is
    /// <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddLineLiff(
        this IServiceCollection services,
        string name,
        Action<LineLiffOptions> configure,
        Action<HttpPipelineOptions>? configurePipeline = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(configure);

        LineChannelRegistrations.ReplaceConfigure(services, name, configure);

        // 與 AddLineMessaging 同樣的理由:管線的設定委派在註冊當下就跑完,拿不到 IOptions,
        // 要把權杖登記進遮罩器只能先自己跑一次 configure。
        // The same reason as in AddLineMessaging: the pipeline's delegate runs to completion at registration and
        // cannot see IOptions, so getting the token to the masker means running configure once here first.
        var probe = new LineLiffOptions();
        configure(probe);

        var httpClientName = LineHttpClientNames.ForLiff(name);
        services.AddHttpClient(httpClientName)
            .AddOzakboyHttpPipeline(options =>
            {
                LineServiceCollectionExtensions.ConfigureLinePipeline(options);
                LineServiceCollectionExtensions.AddKnownSecret(options, probe.ChannelAccessToken);
                configurePipeline?.Invoke(options);
            });

        services.TryAddSingleton<ILineLiffClientFactory, LineLiffClientFactory>();
        LineChannelRegistrations.ReplaceKeyed<ILineLiffClient>(
            services,
            name,
            (provider, _) => new LineLiffClient(
                provider.CreateOzakboyHttpPipelineClient(httpClientName),
                provider.GetRequiredService<IOptionsMonitor<LineLiffOptions>>(),
                name,
                provider.GetService<ILogger<LineLiffClient>>()));

        return services;
    }

    /// <summary>
    /// 從設定區段註冊一個具名的 LIFF server API 用戶端(多 channel)。
    /// Registers a named LIFF server API client from a configuration section, for multi-channel setups.
    /// </summary>
    /// <param name="services">服務集合。The service collection.</param>
    /// <param name="name">channel 名稱;空字串是預設 channel。The channel name; the empty string is the default channel.</param>
    /// <param name="section">設定區段。The configuration section.</param>
    /// <param name="configurePipeline">額外調整這個 channel 的 HTTP 管線。Further adjusts this channel's HTTP pipeline.</param>
    /// <returns>服務集合本身。The service collection.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/>、<paramref name="name"/> 或 <paramref name="section"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="services"/>, <paramref name="name"/> or <paramref name="section"/> is
    /// <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddLineLiff(
        this IServiceCollection services,
        string name,
        IConfiguration section,
        Action<HttpPipelineOptions>? configurePipeline = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(section);

        return services.AddLineLiff(name, section.Bind, configurePipeline);
    }
}
