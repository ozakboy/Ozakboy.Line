using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ozakboy.Line.Messaging;

namespace Ozakboy.Line.AutoReply;

/// <summary>
/// 把關鍵字自動回覆註冊進相依注入容器。
/// Registers keyword auto reply with the dependency injection container.
/// </summary>
public static class LineAutoReplyServiceCollectionExtensions
{
    /// <summary>
    /// 註冊自動回覆的設定、規則儲存體與服務。
    /// Registers the auto reply settings, its rule store, and the service.
    /// </summary>
    /// <param name="services">服務集合。The service collection.</param>
    /// <param name="configure">調整設定;不給時用預設值。Adjusts the settings; the defaults are used when this is not supplied.</param>
    /// <param name="stores">選擇儲存體;不給時用記憶體儲存體。Chooses the store; the in-memory one is used when this is not supplied.</param>
    /// <returns>服務集合本身。The service collection.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="services"/> is <see langword="null"/>.
    /// </exception>
    /// <example>
    /// <code>
    /// services.AddLineMessaging(o => o.ChannelAccessToken = token);
    /// services.AddLineAutoReply(
    ///     o => o.Enabled = true,
    ///     s => s.UseJsonFile("data/line-auto-replies.json"));
    /// </code>
    /// </example>
    /// <remarks>
    /// 需要先註冊 <see cref="ILineMessagingClient"/>(<c>AddLineMessaging</c>):回覆要靠它送出去。
    /// 全部註冊為 singleton —— 檔案型儲存體的鎖是實例層級的,每次請求各拿一個等於沒有鎖。
    /// This needs <see cref="ILineMessagingClient"/> registered first, through <c>AddLineMessaging</c>: that is
    /// what sends the reply. Everything is a singleton, because the file-backed store's lock is per instance and
    /// one instance per request is no lock at all.
    /// </remarks>
    public static IServiceCollection AddLineAutoReply(
        this IServiceCollection services,
        Action<LineAutoReplyOptions>? configure = null,
        Action<LineAutoReplyStoreOptions>? stores = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        var storeOptions = new LineAutoReplyStoreOptions();
        stores?.Invoke(storeOptions);

        services.TryAddSingleton(TimeProvider.System);

        if (string.IsNullOrWhiteSpace(storeOptions.JsonFilePath))
        {
            services.TryAddSingleton<ILineAutoReplyStore>(provider =>
                new InMemoryLineAutoReplyStore(provider.GetRequiredService<TimeProvider>()));
        }
        else
        {
            var filePath = storeOptions.JsonFilePath;
            services.TryAddSingleton<ILineAutoReplyStore>(provider =>
                new JsonFileLineAutoReplyStore(filePath, provider.GetRequiredService<TimeProvider>()));
        }

        services.TryAddSingleton<ILineAutoReplyService>(provider => new LineAutoReplyService(
            provider.GetRequiredService<ILineAutoReplyStore>(),
            provider.GetRequiredService<ILineMessagingClient>(),
            provider.GetRequiredService<IOptions<LineAutoReplyOptions>>(),
            provider.GetService<ILogger<LineAutoReplyService>>()));

        return services;
    }
}
