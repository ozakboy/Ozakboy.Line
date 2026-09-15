using Microsoft.Extensions.DependencyInjection;
using Ozakboy.Line.Mcp.Tools;

namespace Ozakboy.Line.Mcp;

/// <summary>
/// 把 LINE 的工具類別加進既有的 MCP server。
/// Adds the LINE tool classes to an MCP server that already exists.
/// </summary>
public static class LineMcpServerBuilderExtensions
{
    /// <summary>
    /// 把本套件的六組工具全部加進去。
    /// Adds all six of this package's tool sets.
    /// </summary>
    /// <param name="builder">MCP server 建構器。The MCP server builder.</param>
    /// <returns>同一個建構器,方便串接。The same builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="builder"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// 給「已經有自己的 MCP server、只是想多掛 LINE 這一組」的宿主用。仍然要先呼叫
    /// <see cref="LineMcpServiceCollectionExtensions.AddLineMcp"/> —— 工具本身沒有狀態,
    /// 待發佇列與設定才是它們真正依賴的東西。
    /// For a host that already runs its own MCP server and wants the LINE set on it as well.
    /// <see cref="LineMcpServiceCollectionExtensions.AddLineMcp"/> still has to be called first: the tools hold no
    /// state of their own, and the outbox and the settings are what they actually depend on.
    /// </para>
    /// <para>
    /// 自動回覆與訊息範本的工具即使宿主沒註冊那兩個功能也會被掛上,呼叫時回一句「未啟用」。
    /// 這比「工具根本不出現」清楚:AI 讀得到「有這個功能,只是這個站沒開」,而不是自己猜為什麼做不到。
    /// The auto reply and template tools are mounted even when the host has not registered those features, and
    /// answer that the feature is off. That says more than their absence would: the AI reads that the capability
    /// exists and this site has not enabled it, rather than guessing why it cannot do something.
    /// </para>
    /// </remarks>
    public static IMcpServerBuilder WithLineTools(this IMcpServerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .WithTools<LineInfoTools>()
            .WithTools<LineSendTools>()
            .WithTools<LineOutboxTools>()
            .WithTools<LineRichMenuTools>()
            .WithTools<LineAutoReplyTools>()
            .WithTools<LineTemplateTools>();
    }
}
