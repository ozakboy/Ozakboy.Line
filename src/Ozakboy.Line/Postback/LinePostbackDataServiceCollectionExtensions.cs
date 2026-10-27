using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ozakboy.Line.Postback;

/// <summary>
/// 把 postback 資料暫存註冊進相依注入容器。
/// Registers postback data staging with the dependency injection container.
/// </summary>
public static class LinePostbackDataServiceCollectionExtensions
{
    /// <summary>
    /// 註冊 <see cref="ILinePostbackDataStore"/>。
    /// Registers <see cref="ILinePostbackDataStore"/>.
    /// </summary>
    /// <param name="services">服務集合。The service collection.</param>
    /// <param name="configure">
    /// 調整存活時間、長度上限與儲存體;不給時用記憶體儲存體與預設值。
    /// Adjusts the time to live, the length limit and the store; the in-memory store and the defaults are used
    /// when this is not supplied.
    /// </param>
    /// <returns>服務集合本身。The service collection.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="services"/> is <see langword="null"/>.
    /// </exception>
    /// <example>
    /// <code>
    /// services.AddLinePostbackData();                                                 // 記憶體。In memory.
    /// services.AddLinePostbackData(o => o.UseJsonFile("data/line-postbacks.json"));  // JSON 檔。A JSON file.
    /// </code>
    /// </example>
    /// <remarks>
    /// 儲存體註冊為 singleton:檔案型儲存體的鎖是<b>實例層級</b>的,記憶體儲存體的資料也在實例裡,
    /// 每次請求各拿一個等於沒有鎖、也等於存了之後再也查不到。
    /// The store is a singleton: the file-backed store's lock is <b>per instance</b> and the in-memory store's data
    /// lives in the instance, so one per request means no lock, and data stored where it can never be found.
    /// </remarks>
    public static IServiceCollection AddLinePostbackData(
        this IServiceCollection services,
        Action<LinePostbackDataOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new LinePostbackDataOptions();
        configure?.Invoke(options);
        options.EnsureValid();

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(options);

        if (string.IsNullOrWhiteSpace(options.JsonFilePath))
        {
            services.TryAddSingleton<ILinePostbackDataStore>(provider =>
                new InMemoryLinePostbackDataStore(options, provider.GetRequiredService<TimeProvider>()));
        }
        else
        {
            var filePath = options.JsonFilePath;
            services.TryAddSingleton<ILinePostbackDataStore>(provider =>
                new JsonFileLinePostbackDataStore(filePath, options, provider.GetRequiredService<TimeProvider>()));
        }

        return services;
    }
}
