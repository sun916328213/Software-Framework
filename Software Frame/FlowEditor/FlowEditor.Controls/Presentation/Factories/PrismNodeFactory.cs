// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：PrismNodeFactory。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime、FlowEditor.Controls.Models、FlowEditor.Core.Models。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using Prism.Ioc;
using FlowEditor.Controls.Discovery;

namespace FlowEditor.Controls.Presentation.Factories;

/// <summary>使用实例级 Prism 回退解析器创建节点的 adapter。</summary>
public sealed class PrismNodeFactory : INodeFactory
{
    private readonly ReflectionNodeFactory _inner;

    /// <summary>创建与当前 Prism 宿主绑定的节点 factory。</summary>
    /// <param name="containerProvider">应用组合容器。</param>
    public PrismNodeFactory(IContainerProvider containerProvider)
        : this(containerProvider, null) { }

    /// <summary>使用宿主共享目录和实例级依赖解析器创建节点。</summary>
    /// <param name="containerProvider">应用组合容器。</param>
    /// <param name="catalog">统一节点目录，旧式直接构造允许为空。</param>
    public PrismNodeFactory(IContainerProvider containerProvider, IFlowNodeCatalog? catalog)
    {
        ArgumentNullException.ThrowIfNull(containerProvider);
        _inner = new ReflectionNodeFactory(type => TryResolve(containerProvider, type), catalog);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 创建当前实现提供的对象或结果；对应入口为 Create。
    /// 正文中的关键协作调用包括 Create；具体条件和先后次序见方法体。
    /// </remarks>
    public NodeModel Create(Type nodeType) => _inner.Create(nodeType);

    /// <inheritdoc />
    /// <remarks>
    /// 创建当前实现提供的对象或结果；对应入口为 Create。
    /// 正文中的关键协作调用包括 Create；具体条件和先后次序见方法体。
    /// </remarks>
    public NodeModel Create(NodeDocumentModel descriptor) => _inner.Create(descriptor);

    /// <summary>把宿主解析失败转换成反射 factory 可理解的未解析结果。</summary>
    /// <param name="containerProvider">应用组合容器。</param>
    /// <param name="type">待解析依赖类型。</param>
    /// <returns>解析结果；宿主无法提供时返回 null。</returns>
    private static object? TryResolve(IContainerProvider containerProvider, Type type)
    {
        try
        {
            return containerProvider.Resolve(type);
        }
        catch
        {
            return null;
        }
    }
}
