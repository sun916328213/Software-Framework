// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：IUiDispatcher。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
namespace FlowEditor.Controls.Interfaces;

/// <summary>
/// 将需要更新宿主界面的动作封送到 UI 线程的 seam。
/// 核心应用服务只依赖该契约，不感知 WPF 的 Application 或 Dispatcher。
/// </summary>
public interface IUiDispatcher
{
    /// <summary>在宿主 UI 线程执行指定动作。</summary>
    /// <param name="action">需要在 UI 线程执行的动作。</param>
    void Invoke(Action action);
}
