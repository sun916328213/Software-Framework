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
        var nodeMap = nodes.ToDictionary(n => n.Id, n => n);
        var portPositions = CollectPortPositions(nodeCanvas);
        var visiblePaths = new Dictionary<string, Path>();
        var hitPaths = new Dictionary<string, Path>();
        foreach (var path in connectionCanvas.Children.OfType<Path>())
        {
            if (path.Tag is not ConnectionModel existing) continue;
            var target = path.IsHitTestVisible ? hitPaths : visiblePaths;
            target.TryAdd(existing.Id, path);
        }
        var retainedPaths = new HashSet<Path>();

        foreach (var conn in connections)
        {
            if (!nodeMap.TryGetValue(conn.SourceNodeId, out var sourceNode) ||
                !nodeMap.TryGetValue(conn.TargetNodeId, out var targetNode))
                continue;

            var sourcePort = sourceNode.FindPort(conn.SourcePortId);
            var targetPort = targetNode.FindPort(conn.TargetPortId);
            if (sourcePort == null || targetPort == null) continue;

            var sourcePoint = FindPortPositionOnCanvas(portPositions, sourceNode, sourcePort.Id);
            var targetPoint = FindPortPositionOnCanvas(portPositions, targetNode, targetPort.Id);

            bool isSelected = conn.Id == selectedConnectionId;
            bool isActive = sourceNode.Status == Nodes.NodeStatus.Running ||
                           targetNode.Status == Nodes.NodeStatus.Running;
            bool isFlow = sourcePort.DataType == PortType.Flow;

            var geometry = BuildBezierGeometry(sourcePoint, targetPoint);
            var styleKey = GetStyleKey(isSelected, isActive, isFlow);

            // 1) 可见连线 — 用 DynamicResource 引用 XAML 样式，支持热重载
            if (!visiblePaths.TryGetValue(conn.Id, out var path))
            {
                path = new Path { IsHitTestVisible = false };
                connectionCanvas.Children.Add(path);
            }
            path.Data = geometry;
            path.Tag = conn;
            path.SetResourceReference(Path.StyleProperty, styleKey);
            path.ClearValue(Shape.StrokeProperty);
            // 普通数据线沿用端口类型色；选中、运行中和执行流继续使用状态样式。
            if (!isSelected && !isActive && !isFlow)
                path.Stroke = PortColorConverter.ResolveBrush(sourcePort.DataType);
            retainedPaths.Add(path);

            // 2) 透明粗命中区域（12px 宽，便于点击选中）
            if (!hitPaths.TryGetValue(conn.Id, out var hitArea))
            {
                hitArea = new Path
                {
                    Stroke = Brushes.Transparent,
                    StrokeThickness = 12,
                    IsHitTestVisible = true,
                    Cursor = Cursors.Hand
                };
                hitArea.MouseLeftButtonDown += OnConnectionMouseLeftButtonDown;
                hitArea.MouseRightButtonDown += OnConnectionMouseRightButtonDown;
                connectionCanvas.Children.Add(hitArea);
            }
            hitArea.Data = geometry;
            hitArea.Tag = conn;
            retainedPaths.Add(hitArea);
        }

        foreach (var stalePath in connectionCanvas.Children.OfType<Path>()
                     .Where(path => !retainedPaths.Contains(path)).ToArray())
            connectionCanvas.Children.Remove(stalePath);
    }

    private static Dictionary<string, Point> CollectPortPositions(Canvas nodeCanvas)
    {
        var positions = new Dictionary<string, Point>();
        foreach (var connector in PortConnectorLocator.Enumerate(nodeCanvas))
        {
            if (connector.Port is not { } port) continue;
            try { positions[port.Id] = PortConnectorLocator.GetCenter(connector, nodeCanvas); }
            catch (InvalidOperationException) { }
        }
        return positions;
    }

    private static Point FindPortPositionOnCanvas(
        IReadOnlyDictionary<string, Point> portPositions, NodeViewModel nodeVm, string portId)
        => portPositions.TryGetValue(portId, out var point)
            ? point
            : new Point(nodeVm.X + 100, nodeVm.Y + 50);

    private static void OnConnectionMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Path { Tag: ConnectionModel connection } path) return;
        var vm = FindFlowChartViewModel(path);
        if (vm == null) return;
        vm.SelectedConnection = vm.SelectedConnection?.Id == connection.Id ? null : connection;
        e.Handled = true;
    }

    private static void OnConnectionMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Path { Tag: ConnectionModel connection } path) return;
        var vm = FindFlowChartViewModel(path);
        if (vm == null) return;
        vm.DeleteConnectionCommand.Execute(connection);
        e.Handled = true;
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
