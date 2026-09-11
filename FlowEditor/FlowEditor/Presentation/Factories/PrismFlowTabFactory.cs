using FlowEditor.Interfaces;
using FlowEditor.ViewModels;
using Prism.Ioc;

namespace FlowEditor.Presentation.Factories;

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
    public FlowChartViewModel Create()
        => _containerProvider.Resolve<FlowChartViewModel>();
}
