using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using FlowEditor.Models;
using FlowEditor.ViewModels;
using FlowEditor.Converters;

namespace FlowEditor.Views;

/// <summary>
/// 连线渲染器。
/// 负责在画布上绘制所有节点之间的连接线。
/// 所有视觉样式（颜色、粗细、线帽等）均从 XAML 资源样式读取，
/// 在 XAML 设计器中修改 Resources.xaml 即可实时预览并热重载连线外观。
/// </summary>
public static class ConnectionRenderer
{
    /// <summary>
    /// 构建贝塞尔曲线几何体（统一实现，供连线渲染与临时连线共用）。
    /// </summary>
    public static Geometry BuildBezierGeometry(Point start, Point end)
    {
        var dx = Math.Abs(end.X - start.X);
        var offset = Math.Max(50, dx * 0.5);
        var figure = new PathFigure { StartPoint = start, IsClosed = false, IsFilled = false };
        figure.Segments.Add(new BezierSegment(
            new Point(start.X + offset, start.Y),
            new Point(end.X - offset, end.Y),
            end, true));
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    /// <summary>
    /// 根据连线状态获取对应的 XAML 样式键名。
    /// </summary>
    private static string GetStyleKey(bool isSelected, bool isActive, bool isFlow)
    {
        if (isSelected) return "ConnectionPathStyle_Selected";
        if (isActive)   return "ConnectionPathStyle_Active";
        if (isFlow)     return "ConnectionPathStyle_Flow";
        return "ConnectionPathStyle_Normal";
    }

    /// <summary>
    /// 渲染所有连线。视觉表现完全由 XAML 样式控制。
    /// 使用 DynamicResource 引用样式，支持 XAML 热重载。
    /// </summary>
    public static void RenderConnections(
        Canvas connectionCanvas,
        Canvas nodeCanvas,
        IReadOnlyList<ConnectionModel> connections,
        IReadOnlyList<NodeViewModel> nodes,
        string? selectedConnectionId = null)
    {
        connectionCanvas.Children.Clear();
        var nodeMap = nodes.ToDictionary(n => n.Id, n => n);

        foreach (var conn in connections)
        {
            if (!nodeMap.TryGetValue(conn.SourceNodeId, out var sourceNode) ||
                !nodeMap.TryGetValue(conn.TargetNodeId, out var targetNode))
                continue;

            var sourcePort = sourceNode.FindPort(conn.SourcePortId);
            var targetPort = targetNode.FindPort(conn.TargetPortId);
            if (sourcePort == null || targetPort == null) continue;

            var sourcePoint = FindPortPositionOnCanvas(nodeCanvas, sourceNode, sourcePort.Id);
            var targetPoint = FindPortPositionOnCanvas(nodeCanvas, targetNode, targetPort.Id);

            bool isSelected = conn.Id == selectedConnectionId;
            bool isActive = sourceNode.Status == Nodes.NodeStatus.Running ||
                           targetNode.Status == Nodes.NodeStatus.Running;
            bool isFlow = sourcePort.DataType == PortType.Flow;

            var geometry = BuildBezierGeometry(sourcePoint, targetPoint);
            var styleKey = GetStyleKey(isSelected, isActive, isFlow);

            // 1) 可见连线 — 用 DynamicResource 引用 XAML 样式，支持热重载
            var path = new Path
            {
                Data = geometry,
                Tag = conn,
                IsHitTestVisible = false   // 命中测试交给下面的透明粗线
            };
            path.SetResourceReference(Path.StyleProperty, styleKey);
            // 普通数据线沿用端口类型色；选中、运行中和执行流继续使用状态样式。
            if (!isSelected && !isActive && !isFlow)
                path.Stroke = PortColorConverter.ResolveBrush(sourcePort.DataType);
            connectionCanvas.Children.Add(path);

            // 2) 透明粗命中区域（12px 宽，便于点击选中）
            var hitArea = new Path
            {
                Data = geometry,
                Stroke = Brushes.Transparent,
                StrokeThickness = 12,
                Tag = conn,
                IsHitTestVisible = true,
                Cursor = Cursors.Hand
            };

            // 左键选中连线
            hitArea.MouseLeftButtonDown += (_, e) =>
            {
                var vm = FindFlowChartViewModel(connectionCanvas);
                if (vm != null)
                {
                    vm.SelectedConnection = vm.SelectedConnection?.Id == conn.Id ? null : conn;
                    e.Handled = true;
                }
            };

            // 右键删除连线
            hitArea.MouseRightButtonDown += (_, e) =>
            {
                var vm = FindFlowChartViewModel(connectionCanvas);
                if (vm != null)
                {
                    vm.DeleteConnectionCommand.Execute(conn);
                    e.Handled = true;
                }
            };

            connectionCanvas.Children.Add(hitArea);
        }
    }

    /// <summary>在画布上查找端口的圆心坐标。</summary>
    private static Point FindPortPositionOnCanvas(
        Canvas nodeCanvas, NodeViewModel nodeVm, string portId)
    {
        foreach (UIElement child in nodeCanvas.Children)
        {
            if (child is not NodeView nodeView) continue;
            if (nodeView.DataContext != nodeVm) continue;

            var connector = PortConnectorLocator.FindByPortId(nodeView, portId);
            if (connector == null) continue;

            try
            {
                return PortConnectorLocator.GetCenter(connector, nodeCanvas);
            }
            catch { return new Point(nodeVm.X + 100, nodeVm.Y + 50); }
        }

        return new Point(nodeVm.X + 100, nodeVm.Y + 50);
    }

    private static FlowChartViewModel? FindFlowChartViewModel(DependencyObject element)
    {
        var current = element;
        while (current != null)
        {
            if (current is FrameworkElement fe && fe.DataContext is FlowChartViewModel vm)
                return vm;
            current = VisualTreeHelper.GetParent(current) as DependencyObject;
        }
        return null;
    }
}
