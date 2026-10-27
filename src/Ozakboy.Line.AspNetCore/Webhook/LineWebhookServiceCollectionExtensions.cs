using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ozakboy.Line.Webhook.Handling;

namespace Ozakboy.Line.AspNetCore.Webhook;

/// <summary>
/// 註冊 webhook 事件處理常式。
/// Registers a webhook event handler.
/// </summary>
public static class LineWebhookServiceCollectionExtensions
{
    /// <summary>
    /// 把 <typeparamref name="THandler"/> 註冊為 scoped,供 <c>MapLineWebhook&lt;THandler&gt;</c> 使用。
    /// Registers <typeparamref name="THandler"/> as scoped for <c>MapLineWebhook&lt;THandler&gt;</c>.
    /// </summary>
    /// <typeparam name="THandler">處理常式型別。The handler type.</typeparam>
    /// <param name="services">服務集合。The service collection.</param>
    /// <returns>同一個服務集合,方便串接。The same collection, for chaining.</returns>
    /// <remarks>
    /// 用 <c>TryAdd</c>:宿主先自己註冊過(例如改成 singleton)就尊重它。
    /// It uses <c>TryAdd</c>: if the host has already registered the type itself — as a singleton, say — that
    /// registration stands.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> 為 <see langword="null"/> 時擲出。Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddLineWebhookHandler<THandler>(this IServiceCollection services)
        where THandler : LineWebhookHandler
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<THandler>();
        return services;
    }
}
