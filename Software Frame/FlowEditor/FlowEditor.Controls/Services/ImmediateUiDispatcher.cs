// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：ImmediateUiDispatcher。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Controls.Interfaces。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Controls.Interfaces;

namespace FlowEditor.Controls.Services;

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
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 Invoke。
    /// 正文中的关键协作调用包括 ThrowIfNull、action；具体条件和先后次序见方法体。
    /// </remarks>
    public void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
    }
}
