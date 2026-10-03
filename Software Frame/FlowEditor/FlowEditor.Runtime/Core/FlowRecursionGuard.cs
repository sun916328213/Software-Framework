// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：FlowRecursionGuard、Scope。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Core。导入不代表所有类型都会在运行时加载。
using FlowEditor.Core;
namespace FlowEditor.Runtime;

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

    /// <summary>
    /// Scope 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
    /// 继承或实现的契约：IDisposable。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class Scope : IDisposable
    {
        /// <summary>
        /// 执行资源释放步骤；对应入口为 Dispose。
        /// </summary>
        public void Dispose() => Depth.Value--;
    }
}
