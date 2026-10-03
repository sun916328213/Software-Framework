// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：IExecutionScopeContainer。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Core、FlowEditor.Runtime。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Core;
using FlowEditor.Runtime;
namespace FlowEditor.Controls.Core;

/// <summary>
/// 主画布结构化执行容器的编辑器接口；Runtime 调度属性继承自最小结构契约。
/// </summary>
public interface IExecutionScopeContainer : IExecutionScopeDescriptor
{
    /// <summary>编辑器中的容器宽度。</summary>
    double ContainerWidth { get; set; }

    /// <summary>编辑器中的容器高度。</summary>
    double ContainerHeight { get; set; }

    /// <summary>是否在容器边缘显示自身的流程端口。</summary>
    bool ShowBoundaryPorts { get; }

}
