// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：WpfUiDispatcher。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、System.Windows、FlowEditor.Controls.Interfaces。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using System.Windows;
using FlowEditor.Controls.Interfaces;

namespace FlowEditor.Controls.Presentation.Threading;

/// <summary>使用 WPF Dispatcher 封送界面更新的生产环境 adapter。</summary>
public sealed class WpfUiDispatcher : IUiDispatcher
{
    /// <inheritdoc />
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 Invoke。
    /// 正文中的关键协作调用包括 ThrowIfNull、CheckAccess、action、Invoke；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null ||
            dispatcher.HasShutdownStarted ||
            dispatcher.HasShutdownFinished ||
            dispatcher.CheckAccess())
        {
            action();
            return;
        }

        try
        {
            dispatcher.Invoke(action);
        }
        catch (TaskCanceledException)
        {
            action();
        }
        catch (InvalidOperationException) when (
            dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            action();
        }
    }
}
