using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.Nodes;
using Prism.Ioc;

namespace FlowEditor.Presentation.Factories;

/// <summary>使用实例级 Prism 回退解析器创建节点的 adapter。</summary>
public sealed class PrismNodeFactory : INodeFactory
{
    private readonly ReflectionNodeFactory _inner;

    /// <summary>创建与当前 Prism 宿主绑定的节点 factory。</summary>
    /// <param name="containerProvider">应用组合容器。</param>
    public PrismNodeFactory(IContainerProvider containerProvider)
    {
        ArgumentNullException.ThrowIfNull(containerProvider);
        _inner = new ReflectionNodeFactory(type => TryResolve(containerProvider, type));
    }

    /// <inheritdoc />
    public NodeModel Create(Type nodeType) => _inner.Create(nodeType);

    /// <inheritdoc />
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
