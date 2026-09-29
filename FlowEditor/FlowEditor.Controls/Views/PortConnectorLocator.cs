using System.Windows;
using System.Windows.Media;

namespace FlowEditor.Views;

/// <summary>集中管理端口控件的视觉树查找与坐标换算。</summary>
internal static class PortConnectorLocator
{
    /// <summary>从指定视觉元素向上查找最近的端口连接器。</summary>
    public static PortConnectorView? FindAncestor(DependencyObject? element)
    {
        for (var current = element; current != null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is PortConnectorView connector)
                return connector;
        }
        return null;
    }

    /// <summary>在视觉子树中查找绑定到指定端口标识的连接器。</summary>
    public static PortConnectorView? FindByPortId(DependencyObject root, string portId)
        => Enumerate(root).FirstOrDefault(c => c.Port?.Id == portId);

    /// <summary>深度优先枚举视觉子树中的全部端口连接器。</summary>
    public static IEnumerable<PortConnectorView> Enumerate(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is PortConnectorView connector)
                yield return connector;
            foreach (var descendant in Enumerate(child))
                yield return descendant;
        }
    }

    /// <summary>计算端口连接器中心相对于指定视觉元素的坐标。</summary>
    public static Point GetCenter(PortConnectorView connector, Visual relativeTo)
    {
        var center = new Point(connector.ActualWidth / 2, connector.ActualHeight / 2);
        return connector.TransformToVisual(relativeTo).Transform(center);
    }
}
