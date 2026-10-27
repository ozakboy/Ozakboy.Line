using System.Text.Json.Serialization;

namespace Ozakboy.Line.Liff;

/// <summary>
/// 一個 LIFF 應用程式的設定。
/// The settings of one LIFF app.
/// </summary>
/// <remarks>
/// 同一個型別用於新增、更新與讀回:欄位為 <see langword="null"/> 就是「沒有值」 —— 新增時不送、更新時不改。
/// The same type serves adding, updating and reading back: a <see langword="null"/> field means "no value", which
/// is neither sent on add nor changed on update.
/// </remarks>
public sealed class LineLiffApp
{
    /// <summary>
    /// 應用程式的 LIFF ID;只有讀回時才有值,新增與更新時不會送出。
    /// The app's LIFF ID; set only when read back, and never sent on add or update.
    /// </summary>
    [JsonPropertyName("liffId")]
    public string? LiffId { get; init; }

    /// <summary>
    /// 檢視設定。
    /// The view settings.
    /// </summary>
    [JsonPropertyName("view")]
    public LineLiffView? View { get; init; }

    /// <summary>
    /// 應用程式的說明,最多 100 字元。
    /// The app's description, at most 100 characters.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// 要開啟的裝置功能。
    /// The device features to turn on.
    /// </summary>
    [JsonPropertyName("features")]
    public LineLiffFeatures? Features { get; init; }

    /// <summary>
    /// 永久連結的組成方式;目前只有 <c>concat</c>。
    /// How the permanent link is built; currently only <c>concat</c>.
    /// </summary>
    [JsonPropertyName("permanentLinkPattern")]
    public string? PermanentLinkPattern { get; init; }

    /// <summary>
    /// 授權範圍:<c>openid</c>、<c>email</c>、<c>profile</c>、<c>chat_message.write</c>。
    /// The authorisation scopes: <c>openid</c>, <c>email</c>, <c>profile</c> and <c>chat_message.write</c>.
    /// </summary>
#pragma warning disable CA2227 // 模型屬性需要能被物件初始設定式與反序列化指定。The model property must be assignable by initialisers and deserialisation.
    [JsonPropertyName("scope")]
    public IList<string>? Scope { get; init; }
#pragma warning restore CA2227

    /// <summary>
    /// 加好友提示的強度:<c>normal</c>、<c>aggressive</c> 或 <c>none</c>。
    /// How the add-friend prompt behaves: <c>normal</c>, <c>aggressive</c> or <c>none</c>.
    /// </summary>
    [JsonPropertyName("botPrompt")]
    public string? BotPrompt { get; init; }
}
