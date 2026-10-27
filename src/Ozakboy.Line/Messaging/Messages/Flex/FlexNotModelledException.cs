namespace Ozakboy.Line.Messaging.Messages.Flex;

/// <summary>
/// 讀回 Flex JSON 時,節點含有本套件沒建模的欄位、或欄位值的型別不符:呼叫端據此把該節點退回原樣保存。
/// Raised while reading Flex JSON when a node carries a field this package does not model, or a value of the wrong kind;
/// the caller then keeps the node verbatim.
/// </summary>
/// <remarks>
/// 只在內部流動、不會到達呼叫端:<see cref="FlexReader"/> 在元件與容器的分派點接住它。
/// It stays internal and never reaches the caller: <see cref="FlexReader"/> catches it at the component and container dispatch points.
/// </remarks>
#pragma warning disable CA1032, CA1064 // 僅內部使用的控制流程例外,不需要標準建構子,也不該公開。An internal control-flow exception: it needs no standard constructors and must not be public.
internal sealed class FlexNotModelledException : Exception
#pragma warning restore CA1032, CA1064
{
    /// <summary>
    /// 建立例外。
    /// Creates the exception.
    /// </summary>
    /// <param name="field">造成問題的欄位名。The offending field name.</param>
    internal FlexNotModelledException(string field)
        : base($"Flex 欄位 '{field}' 沒有被建模或型別不符。The Flex field '{field}' is not modelled or has the wrong kind.")
    {
    }
}
