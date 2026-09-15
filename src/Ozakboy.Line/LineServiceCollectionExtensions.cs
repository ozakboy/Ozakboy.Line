using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ozakboy.Http;
using Ozakboy.Line.Login;
using Ozakboy.Line.Messaging;
using Ozakboy.Security.Masking;

namespace Ozakboy.Line;

/// <summary>
/// 把 LINE 用戶端註冊進相依注入容器。
/// Registers the LINE clients with the dependency injection container.
/// </summary>
public static class LineServiceCollectionExtensions
{
    /// <summary>
    /// 註冊 LINE Login 用戶端。
    /// Registers the LINE Login client.
    /// </summary>
    /// <param name="services">服務集合。The service collection.</param>
    /// <param name="configure">設定 Login channel。Configures the Login channel.</param>
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
    public static IServiceCollection AddLineLogin(
        this IServiceCollection services,
        Action<LineLoginOptions> configure,
        Action<HttpPipelineOptions>? configurePipeline = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);

        // 這裡先自己跑一次 configure 把 channel secret 拿出來。原因是時機:AddOzakboyHttpPipeline 的設定
        // 委派在「註冊當下」就執行完畢,而 IOptions 要等服務提供者建好才讀得到 —— 想在管線註冊時拿到
        // 祕密去登記遮罩,就只能在這裡先問一次。
        // The configure delegate is run once here to get the channel secret out. The reason is timing:
        // AddOzakboyHttpPipeline's own delegate runs to completion at registration, whereas IOptions cannot be
        // read until the provider is built — so registering the secret with the masker at pipeline registration
        // means asking for it here first.
        var probe = new LineLoginOptions();
        configure(probe);

        services.AddHttpClient(LineHttpClientNames.Login)
            .AddOzakboyHttpPipeline(options =>
            {
                ConfigureLinePipeline(options);
                AddKnownSecret(options, probe.ChannelSecret);
                configurePipeline?.Invoke(options);
            });

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ILineLoginClient>(provider => new LineLoginClient(
            provider.CreateOzakboyHttpPipelineClient(LineHttpClientNames.Login),
            provider.GetRequiredService<IOptions<LineLoginOptions>>(),
            provider.GetService<ILogger<LineLoginClient>>()));

        return services;
    }

    /// <summary>
    /// 從設定區段註冊 LINE Login 用戶端。
    /// Registers the LINE Login client from a configuration section.
    /// </summary>
    /// <param name="services">服務集合。The service collection.</param>
    /// <param name="section">設定區段。The configuration section.</param>
    /// <param name="configurePipeline">額外調整 HTTP 管線。Further adjusts the HTTP pipeline.</param>
    /// <returns>服務集合本身。The service collection.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> 或 <paramref name="section"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="services"/> or <paramref name="section"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddLineLogin(
        this IServiceCollection services,
        IConfiguration section,
        Action<HttpPipelineOptions>? configurePipeline = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(section);

        return services.AddLineLogin(section.Bind, configurePipeline);
    }

    /// <summary>
    /// 註冊 LINE Messaging API 用戶端。
    /// Registers the LINE Messaging API client.
    /// </summary>
    /// <param name="services">服務集合。The service collection.</param>
    /// <param name="configure">設定 Messaging channel。Configures the Messaging channel.</param>
    /// <param name="configurePipeline">額外調整 HTTP 管線。Further adjusts the HTTP pipeline.</param>
    /// <returns>服務集合本身。The service collection.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> 或 <paramref name="configure"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddLineMessaging(
        this IServiceCollection services,
        Action<LineMessagingOptions> configure,
        Action<HttpPipelineOptions>? configurePipeline = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);

        var probe = new LineMessagingOptions();
        configure(probe);

        services.AddHttpClient(LineHttpClientNames.Messaging)
            .AddOzakboyHttpPipeline(options =>
            {
                ConfigureLinePipeline(options);
                AddKnownSecret(options, probe.ChannelAccessToken);
                AddKnownSecret(options, probe.ChannelSecret);
                configurePipeline?.Invoke(options);
            });

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ILineMessagingClient>(provider => new LineMessagingClient(
            provider.CreateOzakboyHttpPipelineClient(LineHttpClientNames.Messaging),
            provider.GetRequiredService<IOptions<LineMessagingOptions>>(),
            provider.GetService<ILogger<LineMessagingClient>>()));

        return services;
    }

    /// <summary>
    /// 從設定區段註冊 LINE Messaging API 用戶端。
    /// Registers the LINE Messaging API client from a configuration section.
    /// </summary>
    /// <param name="services">服務集合。The service collection.</param>
    /// <param name="section">設定區段。The configuration section.</param>
    /// <param name="configurePipeline">額外調整 HTTP 管線。Further adjusts the HTTP pipeline.</param>
    /// <returns>服務集合本身。The service collection.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> 或 <paramref name="section"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="services"/> or <paramref name="section"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddLineMessaging(
        this IServiceCollection services,
        IConfiguration section,
        Action<HttpPipelineOptions>? configurePipeline = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(section);

        return services.AddLineMessaging(section.Bind, configurePipeline);
    }

    /// <summary>
    /// 兩個用戶端共用的管線設定。
    /// The pipeline settings shared by both clients.
    /// </summary>
    /// <param name="options">管線設定。The pipeline options.</param>
    /// <remarks>
    /// <para>
    /// <b>不簽章</b>:LINE 用 Bearer 權杖,沒有請求簽章這回事,掛上簽章處理器只會多一段什麼都不做的處理。
    /// <b>No signing</b>: LINE authenticates with a bearer token and has no request signature, so a signing
    /// handler would be one more stage doing nothing.
    /// </para>
    /// <para>
    /// <b>不本地限流</b>:LINE 的限流是伺服端的事,而且各端點的額度規則不同、官方也沒有公布可據以建桶的數字。
    /// 本地猜一個數字出來,結果是「猜太寬等於沒設、猜太緊自己卡住自己」;正確的做法是尊重 LINE 回的 429 與
    /// <c>Retry-After</c>,那條路徑由重試處理器負責。
    /// <b>No local rate limiting</b>: LINE's limits are enforced at their end, differ per endpoint, and are not
    /// published as numbers one could build buckets from. A locally guessed number is either too loose to matter
    /// or tight enough to throttle oneself; the right answer is to respect LINE's 429 and its <c>Retry-After</c>,
    /// which is the retry handler's job.
    /// </para>
    /// </remarks>
    private static void ConfigureLinePipeline(HttpPipelineOptions options)
    {
        options.EnableSigning = false;
        options.EnableRateLimiting = false;
    }

    /// <summary>
    /// 把一個祕密登記進遮罩清單。
    /// Registers one secret with the mask list.
    /// </summary>
    /// <param name="options">管線設定。The pipeline options.</param>
    /// <param name="secret">祕密的值。The secret's value.</param>
    /// <remarks>
    /// 太短的值不登記。遮罩器以字面替換運作,登記一個五個字元的值,等於讓日誌裡所有含有那五個字元的文字
    /// 都被打上馬賽克 —— 那不是保護,是把日誌毀掉。<c>Ozakboy.Http</c> 也會在驗證時拒絕過短的值。
    /// A value that is too short is not registered. The masker works by literal replacement, and registering a
    /// five-character value masks every piece of log text containing those five characters — not protection, but
    /// a ruined log. <c>Ozakboy.Http</c> also rejects values that are too short during validation.
    /// </remarks>
    private static void AddKnownSecret(HttpPipelineOptions options, string secret)
    {
        if (!string.IsNullOrWhiteSpace(secret) && secret.Length >= SecretMasker.MinimumKnownSecretLength)
        {
            options.KnownSecrets.Add(secret);
        }
    }
}
