using System.Reflection;
using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Messaging;

namespace Ozakboy.Line.Mcp;

/// <summary>
/// 指定的 channel 不存在時用的 <see cref="ILineMessagingClient"/> 替身:每個方法都回
/// <see cref="LineErrorCodes.NotConfigured"/> 失敗,一個請求都不送。
/// The <see cref="ILineMessagingClient"/> stand-in for a channel that does not exist: every method answers a
/// <see cref="LineErrorCodes.NotConfigured"/> failure and sends no request at all.
/// </summary>
/// <remarks>
/// 用 <see cref="DispatchProxy"/> 做,介面日後再長新方法也不必回頭補這裡。
/// Built on <see cref="DispatchProxy"/>, so a method added to the interface later needs nothing here.
/// </remarks>
#pragma warning disable CA1852 // DispatchProxy 的衍生型別不可 sealed:執行期要再從它產生代理類別。The proxy is generated at run time as a subclass, so this cannot be sealed.
internal class LineMcpUnavailableMessagingClient : DispatchProxy
#pragma warning restore CA1852
{
    private static readonly MethodInfo GenericFailure = typeof(Result)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(method => method.Name == nameof(Result.Failure) && method.IsGenericMethodDefinition && method.GetParameters().Length == 1);

    private static readonly MethodInfo FromResult = typeof(Task)
        .GetMethod(nameof(Task.FromResult))!;

    private string _channelName = string.Empty;

    /// <summary>
    /// 建立替身。
    /// Creates the stand-in.
    /// </summary>
    /// <param name="channelName">不存在的 channel 名稱。The missing channel's name.</param>
    /// <returns>替身。The stand-in.</returns>
    internal static ILineMessagingClient Create(string channelName)
    {
        var client = Create<ILineMessagingClient, LineMcpUnavailableMessagingClient>();
        ((LineMcpUnavailableMessagingClient)(object)client)._channelName = channelName;
        return client;
    }

    /// <summary>
    /// 建立「找不到 channel」的失敗。
    /// Creates the "no such channel" failure.
    /// </summary>
    /// <param name="channelName">channel 名稱。The channel name.</param>
    /// <returns>失敗。The failure.</returns>
    internal static Error CreateError(string channelName) => Error.Validation(
        LineErrorCodes.NotConfigured,
        $"MCP 設定的 LINE channel「{channelName}」沒有註冊(LineMcpOptions.ChannelName),無法操作。The LINE channel \"{channelName}\" named by LineMcpOptions.ChannelName is not registered, so nothing can be done with it.");

    /// <inheritdoc />
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);

        if (targetMethod.Name == "get_" + nameof(ILineMessagingClient.ChannelName))
        {
            return _channelName;
        }

        var returnType = targetMethod.ReturnType;
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var resultType = returnType.GetGenericArguments()[0];
            var error = CreateError(_channelName);

            object failure;
            if (resultType == typeof(Result))
            {
                failure = Result.Failure(error);
            }
            else if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Result<>))
            {
                failure = GenericFailure.MakeGenericMethod(resultType.GetGenericArguments()[0]).Invoke(null, [error])!;
            }
            else
            {
                throw new NotSupportedException($"替身不知道如何回應 {targetMethod.Name} 的回傳型別 {returnType}。");
            }

            return FromResult.MakeGenericMethod(resultType).Invoke(null, [failure]);
        }

        throw new NotSupportedException($"替身不知道如何回應 {targetMethod.Name}。");
    }
}
