using FlowEditor.Interfaces;

namespace FlowEditor.Services;

/// <summary>在当前线程立即执行动作的无头 UI 调度 adapter。</summary>
internal sealed class ImmediateUiDispatcher : IUiDispatcher
{
    /// <summary>共享的无状态实例。</summary>
    public static ImmediateUiDispatcher Instance { get; } = new();

    /// <summary>防止外部创建多余实例。</summary>
    private ImmediateUiDispatcher()
    {
    }

    /// <inheritdoc />
    public void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
    }
}
