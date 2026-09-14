namespace FlowEditor.Core;

/// <summary>
/// 子流程递归深度防护（P5）。
/// 以 AsyncLocal 追踪异步调用链上的嵌套深度，防止子流程自引用/循环引用导致栈溢出。
/// </summary>
public static class FlowRecursionGuard
{
    /// <summary>允许的最大嵌套深度。</summary>
    public const int MaxDepth = 8;

    private static readonly AsyncLocal<int> Depth = new();

    /// <summary>当前异步调用链的嵌套深度（0 = 顶层流程）。</summary>
    public static int CurrentDepth => Depth.Value;

    /// <summary>
    /// 进入一层子流程：深度 +1；超过 MaxDepth 抛出异常。
    /// 返回的作用域 Dispose 时深度 -1。
    /// </summary>
    public static IDisposable Enter()
    {
        if (Depth.Value >= MaxDepth)
            throw new InvalidOperationException($"子流程嵌套深度超过限制（最大 {MaxDepth} 层），请检查是否存在循环引用");
        Depth.Value++;
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        public void Dispose() => Depth.Value--;
    }
}
