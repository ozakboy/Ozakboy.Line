using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        services.Configure(configure);

        // 與 AddLineMessaging 同樣的理由:管線的設定委派在註冊當下就跑完,拿不到 IOptions,
        // 要把權杖登記進遮罩器只能先自己跑一次 configure。
        // The same reason as in AddLineMessaging: the pipeline's delegate runs to completion at registration and
        // cannot see IOptions, so getting the token to the masker means running configure once here first.
        var probe = new LineLiffOptions();
        configure(probe);

        services.AddHttpClient(LineHttpClientNames.Liff)
            .AddOzakboyHttpPipeline(options =>
            {
                LineServiceCollectionExtensions.ConfigureLinePipeline(options);
                LineServiceCollectionExtensions.AddKnownSecret(options, probe.ChannelAccessToken);
                configurePipeline?.Invoke(options);
            });

        services.AddSingleton<ILineLiffClient>(provider => new LineLiffClient(
            provider.CreateOzakboyHttpPipelineClient(LineHttpClientNames.Liff),
            provider.GetRequiredService<IOptions<LineLiffOptions>>(),
            provider.GetService<ILogger<LineLiffClient>>()));

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
}
