using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ozakboy.Line.Mcp.Outbox;
using Ozakboy.Line.Messaging;

namespace Ozakboy.Line.Mcp;

/// <summary>
/// 把 LINE 的 MCP 工具組註冊進相依注入容器。
/// Registers the LINE MCP tool set with the dependency injection container.
/// </summary>
public static class LineMcpServiceCollectionExtensions
{
    /// <summary>
    /// 註冊設定、待發佇列、待發項目服務與抓圖用的 HTTP 用戶端。
    /// Registers the settings, the outbox, the outbox service, and the HTTP client used to fetch images.
    /// </summary>
    /// <param name="services">服務集合。The service collection.</param>
    /// <param name="configure">調整設定。Adjusts the settings.</param>
    /// <param name="stores">選擇儲存體;不給時用記憶體佇列。Chooses the store; the in-memory queue is used when this is not supplied.</param>
    /// <returns>服務集合本身。The service collection.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> 或 <paramref name="configure"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// 這個方法<b>不會</b>註冊 MCP server 本身。宿主已經有自己的 MCP server 時,用
    /// <see cref="LineMcpServerBuilderExtensions.WithLineTools"/> 把工具加進去;
    /// 沒有的話用 <see cref="AddLineMcpServer"/> 一次做完。
    /// This does <b>not</b> register an MCP server. A host that already has one adds the tools with
    /// <see cref="LineMcpServerBuilderExtensions.WithLineTools"/>; a host that does not uses
    /// <see cref="AddLineMcpServer"/> to do both at once.
    /// </remarks>
    public static IServiceCollection AddLineMcp(
        this IServiceCollection services,
        Action<LineMcpOptions> configure,
        Action<LineMcpStoreOptions>? stores = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);

        var storeOptions = new LineMcpStoreOptions();
        stores?.Invoke(storeOptions);

        services.TryAddSingleton(TimeProvider.System);

        if (string.IsNullOrWhiteSpace(storeOptions.OutboxJsonFilePath))
        {
            services.TryAddSingleton<ILineMcpOutboxStore, InMemoryLineMcpOutboxStore>();
        }
        else
        {
            var filePath = storeOptions.OutboxJsonFilePath;
            services.TryAddSingleton<ILineMcpOutboxStore>(_ => new JsonFileLineMcpOutboxStore(filePath));
        }

        // 抓圖用的具名用戶端。逾時壓在 30 秒:它連的是<b>外部 AI 指定的位址</b>,
        // 沒有逾時的話,一個回應很慢的位址就能把宿主的核准操作卡在那裡。
        // The named client used to fetch images, with a thirty-second timeout. It connects to <b>an address the
        // outside AI chose</b>, and without a timeout one slow address is enough to hang the host's approval.
        services.AddHttpClient(LineMcpHttpClientNames.ImageFetch, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Ozakboy.Line.Mcp");
        });

        services.TryAddSingleton<ILineMcpOutboxService>(provider => new LineMcpOutboxService(
            provider.GetRequiredService<ILineMcpOutboxStore>(),
            provider.GetRequiredService<ILineMessagingClient>(),
            provider.GetRequiredService<IHttpClientFactory>(),
            provider.GetRequiredService<TimeProvider>()));

        return services;
    }

    /// <summary>
    /// 註冊一個只掛 LINE 工具的 MCP server(含 <see cref="AddLineMcp"/> 的全部內容)。
    /// Registers an MCP server carrying just the LINE tools, including everything <see cref="AddLineMcp"/> does.
    /// </summary>
    /// <param name="services">服務集合。The service collection.</param>
    /// <param name="configure">調整設定。Adjusts the settings.</param>
    /// <param name="stores">選擇儲存體。Chooses the store.</param>
    /// <returns>MCP server 建構器,可再串接其他工具。The MCP server builder, for chaining more tools.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> 或 <paramref name="configure"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// 傳輸設為 stateless:不追蹤 session、只吃 POST。這是反向代理後面、又不需要 server 主動送訊息的
    /// 標準用法,也讓多個執行個體不必有 session 黏著性。
    /// The transport is stateless: no session tracking, POST only. That is the standard arrangement behind a
    /// reverse proxy where the server never needs to initiate a message, and it frees several instances from
    /// needing session affinity.
    /// </remarks>
    public static IMcpServerBuilder AddLineMcpServer(
        this IServiceCollection services,
        Action<LineMcpOptions> configure,
        Action<LineMcpStoreOptions>? stores = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddLineMcp(configure, stores);

        return services
            .AddMcpServer()
            .WithHttpTransport(options => options.Stateless = true)
            .WithLineTools();
    }
}
