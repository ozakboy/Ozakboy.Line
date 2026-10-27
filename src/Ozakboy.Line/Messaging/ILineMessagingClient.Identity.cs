namespace Ozakboy.Line.Messaging;

/// <summary>
/// LINE Messaging API 的用戶端(用戶端身分,0.4.0 起)。
/// The LINE Messaging API client — client identity, since 0.4.0.
/// </summary>
public partial interface ILineMessagingClient
{
    /// <summary>
    /// 這個用戶端綁定的 channel 名稱;未具名註冊的預設 channel 是空字串。
    /// The name of the channel this client is bound to; the default channel of an unnamed registration is the
    /// empty string.
    /// </summary>
    /// <remarks>
    /// 以預設介面實作回傳空字串,讓 0.4.0 之前就自行實作(或模擬)此介面的程式碼不必修改就能編譯。
    /// Implemented as a default interface member returning the empty string, so code that implemented (or mocked)
    /// this interface before 0.4.0 keeps compiling unchanged.
    /// </remarks>
    string ChannelName => string.Empty;
}
