using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace Ozakboy.Line.AspNetCore;

/// <summary>
/// 把 LINE Login 認證方案掛進 ASP.NET Core。
/// Adds the LINE Login authentication scheme to ASP.NET Core.
/// </summary>
/// <example>
/// <code>
/// builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
///     .AddCookie()
///     .AddLineLogin(options =>
///     {
///         options.ClientId = builder.Configuration["Line:ChannelId"]!;
///         options.ClientSecret = builder.Configuration["Line:ChannelSecret"]!;
///         options.BotPrompt = LineBotPrompt.Aggressive;
///     });
/// </code>
/// </example>
public static class LineLoginAuthenticationExtensions
{
    /// <summary>
    /// 以預設方案名稱與設定掛上 LINE Login。
    /// Adds LINE Login with the default scheme name and settings.
    /// </summary>
    /// <param name="builder">認證建構器。The authentication builder.</param>
    /// <returns>建構器本身。The builder.</returns>
    public static AuthenticationBuilder AddLineLogin(this AuthenticationBuilder builder) =>
        builder.AddLineLogin(
            LineLoginAuthenticationDefaults.AuthenticationScheme,
            LineLoginAuthenticationDefaults.DisplayName,
            _ => { });

    /// <summary>
    /// 以預設方案名稱掛上 LINE Login。
    /// Adds LINE Login with the default scheme name.
    /// </summary>
    /// <param name="builder">認證建構器。The authentication builder.</param>
    /// <param name="configure">設定委派。The configuration delegate.</param>
    /// <returns>建構器本身。The builder.</returns>
    public static AuthenticationBuilder AddLineLogin(
        this AuthenticationBuilder builder,
        Action<LineLoginAuthenticationOptions> configure) =>
        builder.AddLineLogin(
            LineLoginAuthenticationDefaults.AuthenticationScheme,
            LineLoginAuthenticationDefaults.DisplayName,
            configure);

    /// <summary>
    /// 以指定的方案名稱掛上 LINE Login。
    /// Adds LINE Login under a given scheme name.
    /// </summary>
    /// <param name="builder">認證建構器。The authentication builder.</param>
    /// <param name="authenticationScheme">方案名稱。The scheme name.</param>
    /// <param name="configure">設定委派。The configuration delegate.</param>
    /// <returns>建構器本身。The builder.</returns>
    public static AuthenticationBuilder AddLineLogin(
        this AuthenticationBuilder builder,
        string authenticationScheme,
        Action<LineLoginAuthenticationOptions> configure) =>
        builder.AddLineLogin(authenticationScheme, LineLoginAuthenticationDefaults.DisplayName, configure);

    /// <summary>
    /// 以指定的方案名稱與顯示名稱掛上 LINE Login。
    /// Adds LINE Login under a given scheme name and display name.
    /// </summary>
    /// <param name="builder">認證建構器。The authentication builder.</param>
    /// <param name="authenticationScheme">方案名稱。The scheme name.</param>
    /// <param name="displayName">顯示名稱。The display name.</param>
    /// <param name="configure">設定委派。The configuration delegate.</param>
    /// <returns>建構器本身。The builder.</returns>
    /// <remarks>
    /// 需要同時支援兩個 LINE Login channel(例如正式站與測試站、或兩個品牌)時,
    /// 用不同的方案名稱各掛一次即可,兩者的設定完全獨立。
    /// Supporting two LINE Login channels at once — a production and a staging one, or two brands — is a matter
    /// of calling this twice with different scheme names; their settings are entirely independent.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/> 或 <paramref name="configure"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    public static AuthenticationBuilder AddLineLogin(
        this AuthenticationBuilder builder,
        string authenticationScheme,
        string displayName,
        Action<LineLoginAuthenticationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        return builder.AddOAuth<LineLoginAuthenticationOptions, LineLoginAuthenticationHandler>(
            authenticationScheme,
            displayName,
            configure);
    }
}
