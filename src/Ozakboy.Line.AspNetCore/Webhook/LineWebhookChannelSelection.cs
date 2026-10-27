using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace Ozakboy.Line.AspNetCore.Webhook;

/// <summary>
/// 端點挑選 channel 的方式快照:不指定、固定名稱,或從路由值取名稱。
/// A snapshot of how an endpoint selects its channel: none, a fixed name, or a name taken from a route value.
/// </summary>
internal sealed class LineWebhookChannelSelection
{
    private readonly string? _fixedName;
    private readonly string? _routeParameter;

    private LineWebhookChannelSelection(string? fixedName, string? routeParameter)
    {
        _fixedName = fixedName;
        _routeParameter = routeParameter;
    }

    /// <summary>
    /// 沒有指定 channel:沿用單一 channel 的行為(預設 channel,密鑰來自 <c>IOptions</c>)。
    /// No channel specified: the single-channel behaviour, with the default channel and a secret from
    /// <c>IOptions</c>.
    /// </summary>
    internal static LineWebhookChannelSelection None { get; } = new(null, null);

    /// <summary>
    /// 是否指定了 channel(固定名稱或路由參數)。
    /// Whether a channel was specified, by fixed name or route parameter.
    /// </summary>
    internal bool IsSelected => _fixedName is not null || _routeParameter is not null;

    /// <summary>
    /// 這個端點是否可能落在具名 channel(路由參數,或非空的固定名稱)。
    /// Whether this endpoint may land on a named channel: a route parameter, or a non-empty fixed name.
    /// </summary>
    internal bool MayBeNamed => _routeParameter is not null || _fixedName is { Length: > 0 };

    /// <summary>
    /// 驗證並複製設定。
    /// Validates and copies the settings.
    /// </summary>
    /// <param name="options">端點設定。The endpoint settings.</param>
    /// <returns>快照。The snapshot.</returns>
    /// <exception cref="InvalidOperationException">
    /// <see cref="LineWebhookEndpointOptions.ChannelName"/> 與 <see cref="LineWebhookEndpointOptions.ChannelRouteParameter"/>
    /// 同時設定,或路由參數名稱是空白時擲出。
    /// Thrown when both <see cref="LineWebhookEndpointOptions.ChannelName"/> and
    /// <see cref="LineWebhookEndpointOptions.ChannelRouteParameter"/> are set, or the route parameter name is blank.
    /// </exception>
    internal static LineWebhookChannelSelection Create(LineWebhookEndpointOptions options)
    {
        var fixedName = options.ChannelName;
        var routeParameter = options.ChannelRouteParameter;

        if (fixedName is not null && routeParameter is not null)
        {
            throw new InvalidOperationException(
                "LineWebhookEndpointOptions.ChannelName 與 ChannelRouteParameter 只能擇一設定。ChannelName and ChannelRouteParameter are mutually exclusive; set only one.");
        }

        if (routeParameter is not null && string.IsNullOrWhiteSpace(routeParameter))
        {
            throw new InvalidOperationException(
                "LineWebhookEndpointOptions.ChannelRouteParameter 不可是空白。ChannelRouteParameter cannot be blank.");
        }

        return fixedName is null && routeParameter is null ? None : new LineWebhookChannelSelection(fixedName, routeParameter);
    }

    /// <summary>
    /// 取得這個請求要用的 channel 名稱;路由值缺漏時是空字串(預設 channel)。
    /// Gets the channel name this request targets; an empty string (the default channel) when the route value is
    /// absent.
    /// </summary>
    /// <param name="context">請求內容。The HTTP context.</param>
    /// <returns>channel 名稱。The channel name.</returns>
    internal string Resolve(HttpContext context)
    {
        if (_fixedName is not null)
        {
            return _fixedName;
        }

        if (_routeParameter is not null
            && context.Request.RouteValues.TryGetValue(_routeParameter, out var value)
            && value is not null)
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        return string.Empty;
    }
}
