// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：PrismFlowTabFactory。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Controls.Interfaces、FlowEditor.Controls.ViewModels、Prism.Ioc。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Controls.Interfaces;
using FlowEditor.Controls.ViewModels;
using Prism.Ioc;

namespace FlowEditor.Controls.Presentation.Factories;

/// <summary>通过 Prism 组合容器创建流程标签页的 adapter。</summary>
public sealed class PrismFlowTabFactory : IFlowTabFactory
{
    private readonly IContainerProvider _containerProvider;

    /// <summary>创建 Prism 标签页 factory。</summary>
    /// <param name="containerProvider">应用组合容器。</param>
    public PrismFlowTabFactory(IContainerProvider containerProvider)
    {
        _containerProvider = containerProvider ?? throw new ArgumentNullException(nameof(containerProvider));
    }

    /// <inheritdoc />
    /// <remarks>
    /// 创建当前实现提供的对象或结果；对应入口为 Create。
    /// 正文中的关键协作调用包括 Resolve；具体条件和先后次序见方法体。
    /// </remarks>
    public FlowChartViewModel Create()
        => _containerProvider.Resolve<FlowChartViewModel>();
}
