namespace Ozakboy.Line.Login;

/// <summary>
/// 登入流程中要不要引導使用者加官方帳號好友。
/// Whether the sign-in flow should invite the user to add the official account as a friend.
/// </summary>
/// <remarks>
/// 這個選項只有在 Login channel 設定了 Linked OA(連結的官方帳號)時才有作用。沒設定時 LINE 直接忽略
/// <c>bot_prompt</c>,登入照走,畫面上就是不會出現加好友那一步 —— 沒有任何錯誤訊息可看,
/// 這也是「設定好了卻沒效果」最常見的原因。
/// This has an effect only when the Login channel has a linked official account. Without one LINE simply ignores
/// <c>bot_prompt</c>: sign-in proceeds, the add-friend step never appears, and no error says why — which is the
/// usual explanation for "it is configured but nothing happens".
/// </remarks>
public enum LineBotPrompt
{
    /// <summary>
    /// 不引導加好友。
    /// Do not invite the user to add the account.
    /// </summary>
    None = 0,

    /// <summary>
    /// 在同意畫面上附加好友選項,使用者可以略過。
    /// Offers the option on the consent screen, which the user may skip.
    /// </summary>
    Normal = 1,

    /// <summary>
    /// 同意之後另開一個畫面詢問是否加好友。
    /// Shows a separate screen asking to add the account after consent.
    /// </summary>
    Aggressive = 2,
}
