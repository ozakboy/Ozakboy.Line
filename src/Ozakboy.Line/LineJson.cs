using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ozakboy.Line;

/// <summary>
/// 本套件共用的 JSON 設定。
/// The JSON settings shared across this package.
/// </summary>
/// <remarks>
/// 只有一份設定物件、而且是靜態的,因為 <see cref="JsonSerializerOptions"/> 第一次使用時會建立並快取
/// 它的中繼資料 —— 每次呼叫都 new 一個,等於每次請求都重新做一次反射。
/// There is exactly one options instance, and it is static, because <see cref="JsonSerializerOptions"/> builds
/// and caches its metadata on first use: a fresh instance per call means redoing the reflection on every request.
/// </remarks>
internal static class LineJson
{
    /// <summary>
    /// 序列化與反序列化共用的設定。
    /// The options used for both serialising and deserialising.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>PropertyNamingPolicy</c> 設為 camelCase 只是預設值:LINE 的 OAuth 端點用 snake_case
    /// (<c>access_token</c>),Messaging API 用 camelCase(<c>userId</c>),兩者並存,
    /// 因此每個模型的欄位都各自標了 <see cref="JsonPropertyNameAttribute"/>,不靠命名原則猜。
    /// The camelCase naming policy is only a default. LINE's OAuth endpoints use snake_case
    /// (<c>access_token</c>) while the Messaging API uses camelCase (<c>userId</c>), so every model field
    /// carries its own <see cref="JsonPropertyNameAttribute"/> rather than relying on a policy to guess.
    /// </para>
    /// <para>
    /// <c>NumberHandling</c> 允許從字串讀數字:LINE 有些欄位(例如 <c>expires_in</c>)在不同端點上
    /// 有時是數字、有時是字串,關掉這個設定會在少數端點上以反序列化例外收場。
    /// Reading numbers from strings is allowed because some LINE fields — <c>expires_in</c>, for one — arrive as
    /// a number at one endpoint and as a string at another, and without it those endpoints end in a
    /// deserialisation exception.
    /// </para>
    /// </remarks>
    internal static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };
}
