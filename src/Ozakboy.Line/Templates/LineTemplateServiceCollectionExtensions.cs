using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ozakboy.Line.Templates;

/// <summary>
/// 把訊息範本儲存體註冊進相依注入容器。
/// Registers the message template store with the dependency injection container.
/// </summary>
public static class LineTemplateServiceCollectionExtensions
{
    /// <summary>
    /// 註冊訊息範本儲存體。
    /// Registers the message template store.
    /// </summary>
    /// <param name="services">服務集合。The service collection.</param>
    /// <param name="configure">
    /// 選擇儲存體;不給時用記憶體儲存體。
    /// Chooses the store; the in-memory one is used when this is not supplied.
    /// </param>
    /// <returns>服務集合本身。The service collection.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="services"/> is <see langword="null"/>.
    /// </exception>
    /// <example>
    /// <code>
    /// services.AddLineTemplates();                                  // 記憶體。In memory.
    /// services.AddLineTemplates(o => o.UseJsonFile("data/line-templates.json")); // JSON 檔。A JSON file.
    /// </code>
    /// </example>
    /// <remarks>
    /// 儲存體註冊為 singleton。檔案型儲存體的鎖是<b>實例層級</b>的,每次請求都 new 一個的話那把鎖就形同虛設,
    /// 兩個同時進來的寫入會互相覆蓋。
    /// The store is a singleton. The file-backed store's lock is <b>per instance</b>, so a new one per request
    /// makes the lock meaningless and two concurrent writes overwrite each other.
    /// </remarks>
    public static IServiceCollection AddLineTemplates(
        this IServiceCollection services,
        Action<LineTemplateStoreOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new LineTemplateStoreOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(TimeProvider.System);

        if (string.IsNullOrWhiteSpace(options.JsonFilePath))
        {
            services.TryAddSingleton<ILineTemplateStore>(provider =>
                new InMemoryLineTemplateStore(provider.GetRequiredService<TimeProvider>()));
        }
        else
        {
            var filePath = options.JsonFilePath;
            services.TryAddSingleton<ILineTemplateStore>(provider =>
                new JsonFileLineTemplateStore(filePath, provider.GetRequiredService<TimeProvider>()));
        }

        return services;
    }
}
