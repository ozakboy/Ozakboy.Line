using System.Reflection;
using Ozakboy.Core.Abstractions;
using Ozakboy.Line.Messaging;
using Ozakboy.Line.Messaging.Messages;

namespace Ozakboy.Line.Tests.TestSupport;

/// <summary>
/// 一次被記錄下來的送出呼叫。
/// One recorded send call.
/// </summary>
/// <param name="Method">方法名稱。The method name.</param>
/// <param name="Target">回覆權杖或收件人。The reply token or the recipient.</param>
/// <param name="Text">文字內容(文字訊息的第一則)。The text of the first text message.</param>
public sealed record RecordedSend(string Method, string Target, string? Text);

/// <summary>
/// 以 <see cref="DispatchProxy"/> 實作的 <see cref="ILineMessagingClient"/>,只認得回覆與推播,並記錄呼叫。
/// An <see cref="ILineMessagingClient"/> built on <see cref="DispatchProxy"/> that understands only replies and
/// pushes, and records them.
/// </summary>
/// <remarks>
/// 與 <c>FakeLineMessagingClient</c> 不同,這個不需要隨介面成長而補成員 —— webhook 處理只用到回覆與推播,
/// 其他方法一律擲 <see cref="NotSupportedException"/>,免得測試因為錯的理由通過。
/// Unlike <c>FakeLineMessagingClient</c> it needs no new member whenever the interface grows: webhook handling
/// uses only replies and pushes, and anything else throws <see cref="NotSupportedException"/> so a test cannot
/// pass for the wrong reason. Also linked into the ASP.NET Core test project.
/// </remarks>
public class RecordingLineMessagingClient : DispatchProxy
{
    private readonly object _gate = new();
    private readonly List<RecordedSend> _sends = [];

    /// <summary>
    /// 到目前為止的送出呼叫快照。
    /// A snapshot of the send calls so far.
    /// </summary>
    public IReadOnlyList<RecordedSend> Sends
    {
        get
        {
            lock (_gate)
            {
                return [.. _sends];
            }
        }
    }

    /// <summary>
    /// 回覆類方法要回的失敗;為 <see langword="null"/> 時成功。
    /// The failure the reply methods answer with, or <see langword="null"/> for success.
    /// </summary>
    public Error? ReplyFailure { get; set; }

    /// <summary>
    /// 依收件人決定推播類方法回的失敗;為 <see langword="null"/> 或回傳 <see langword="null"/> 時成功。
    /// Decides per recipient what failure a push answers with; success when it is <see langword="null"/> or returns <see langword="null"/>.
    /// </summary>
    public Func<string, Error?>? PushFailureFor { get; set; }

    /// <summary>
    /// 依收件人決定推播類方法要擲的例外。
    /// Decides per recipient what exception a push throws.
    /// </summary>
    public Func<string, Exception?>? PushThrowsFor { get; set; }

    /// <summary>
    /// 回覆類方法要擲的例外。
    /// The exception the reply methods throw.
    /// </summary>
    public Exception? ReplyThrows { get; set; }

    /// <summary>
    /// 建立一個記錄用的用戶端。
    /// Creates a recording client.
    /// </summary>
    /// <returns>介面與記錄器本身。The interface and the recorder itself.</returns>
    public static (ILineMessagingClient Client, RecordingLineMessagingClient Recorder) Create()
    {
        var client = Create<ILineMessagingClient, RecordingLineMessagingClient>();
        return (client, (RecordingLineMessagingClient)(object)client);
    }

    /// <inheritdoc />
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        ArgumentNullException.ThrowIfNull(args);

        var name = targetMethod.Name;
        var isReply = name is "ReplyAsync" or "ReplyTextAsync";
        var isPush = name is "PushAsync" or "PushTextAsync";
        if (!isReply && !isPush)
        {
            throw new NotSupportedException($"RecordingLineMessagingClient 沒有安排 {name}。");
        }

        var target = (string)args[0]!;
        var text = args[1] switch
        {
            string value => value,
            IReadOnlyList<LineMessage> messages when messages.Count > 0 && messages[0] is TextMessage first => first.Text,
            _ => null,
        };

        lock (_gate)
        {
            _sends.Add(new RecordedSend(name, target, text));
        }

        var thrown = isReply ? ReplyThrows : PushThrowsFor?.Invoke(target);
        if (thrown is not null)
        {
            throw thrown;
        }

        var failure = isReply ? ReplyFailure : PushFailureFor?.Invoke(target);
        return Task.FromResult(failure is null
            ? Result.Success<IReadOnlyList<LineSentMessage>>([])
            : Result.Failure<IReadOnlyList<LineSentMessage>>(failure));
    }
}
