using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using ModelContextProtocol.AspNetCore;

namespace Ozakboy.Line.Mcp;

/// <summary>
/// 掛上 MCP 端點。
/// Maps the MCP endpoint.
/// </summary>
public static class LineMcpEndpointRouteBuilderExtensions
{
    /// <summary>
    /// 在 <paramref name="prefix"/> 掛上 MCP 端點。
    /// Maps the MCP endpoint at <paramref name="prefix"/>.
    /// </summary>
    /// <param name="endpoints">端點路由建構器。The endpoint route builder.</param>
    /// <param name="prefix">
    /// 掛載前綴,必須與 <see cref="LineMcpApplicationBuilderExtensions.UseLineMcpKeyGate"/> 用的<b>同一個</b>。
    /// The mount prefix, which must be the <b>same one</b> given to
    /// <see cref="LineMcpApplicationBuilderExtensions.UseLineMcpKeyGate"/>.
    /// </param>
    /// <returns>端點慣例建構器。The endpoint convention builder.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="endpoints"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="endpoints"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// 端點掛在 <b>不含金鑰</b>的路徑上:對外的 <c>{prefix}/{金鑰}</c> 已經由金鑰閘把金鑰那一段剝掉了。
    /// 兩邊的前綴不一致的話,金鑰閘改寫出來的路徑會找不到端點,結果是金鑰正確也一律 404。
    /// The endpoint sits on the path <b>without</b> the key: the gate has already stripped that segment from the
    /// public <c>{prefix}/{key}</c>. With the two prefixes out of step, the path the gate rewrites matches no
    /// endpoint, and a correct key answers 404 all the same.
    /// </remarks>
    public static IEndpointConventionBuilder MapLineMcp(
        this IEndpointRouteBuilder endpoints,
        string prefix = LineMcpApplicationBuilderExtensions.DefaultPrefix)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        return endpoints.MapMcp(prefix);
    }
}
