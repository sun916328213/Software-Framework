// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：IFlowTabFactory。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
namespace FlowEditor.Controls.ViewModels;

/// <summary>
/// 创建独立流程标签页 ViewModel 的同层 seam。
/// 由 Presentation adapter 实现，避免通用 Interfaces 模块反向依赖 ViewModel。
/// </summary>
public interface IFlowTabFactory
{
    /// <summary>创建一个具有独立流程会话的标签页。</summary>
    /// <returns>新的流程标签页 ViewModel。</returns>
    FlowChartViewModel Create();
}
