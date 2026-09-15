namespace Ozakboy.Line.Login;

/// <summary>
/// 一次完整登入的結果。
/// The result of one complete sign-in.
/// </summary>
/// <remarks>
/// 由 <see cref="ILineLoginClient.CompleteLoginAsync"/> 產生:換權杖、查個人檔案、查好友狀態、
/// 驗 id_token 四個步驟的成果放在一起,呼叫端不必自己串。
/// Produced by <see cref="ILineLoginClient.CompleteLoginAsync"/>: the token exchange, the profile lookup, the
/// friendship lookup, and the id_token validation, gathered so the caller does not have to chain them.
/// </remarks>
public sealed class LineLoginResult
{
    /// <summary>
    /// 權杖端點回的內容。
    /// What the token endpoint returned.
    /// </summary>
    public required LineTokenResponse Tokens { get; init; }

    /// <summary>
    /// 使用者的個人檔案。
    /// The user's profile.
    /// </summary>
    public required LineUserProfile Profile { get; init; }

    /// <summary>
    /// 使用者是否已加官方帳號好友;查不到時為 <see langword="null"/>。
    /// Whether the user has added the official account, or <see langword="null"/> when it could not be determined.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> 最常見的原因是 Login channel 沒有設定 Linked OA —— 這種情況下 LINE 回 4xx,
    /// 但登入本身完全正常,不該因此失敗。要區分「沒加好友」與「查不到」,就得靠這個可為 null 的布林。
    /// The usual reason for <see langword="null"/> is a Login channel with no linked official account: LINE
    /// answers 4xx, while the sign-in itself is perfectly fine and should not fail over it. Telling "not a
    /// friend" apart from "could not tell" is exactly what the nullable boolean is for.
    /// </remarks>
    public bool? IsFriend { get; init; }

    /// <summary>
    /// id_token 的內容;沒有 id_token 時為 <see langword="null"/>。
    /// The id_token's contents, or <see langword="null"/> when there was no id_token.
    /// </summary>
    /// <remarks>
    /// 有 id_token 而本地驗證不過時,<see cref="ILineLoginClient.CompleteLoginAsync"/> 整個回失敗而不是
    /// 把這裡留成 <see langword="null"/>:驗不過代表 token 被動過手腳或設定錯了,兩種都不該放行。
    /// When an id_token is present but does not verify locally,
    /// <see cref="ILineLoginClient.CompleteLoginAsync"/> fails outright rather than leaving this
    /// <see langword="null"/>: a token that does not verify has either been tampered with or was issued under
    /// different settings, and neither should be let through.
    /// </remarks>
    public LineIdTokenPayload? IdToken { get; init; }
}
