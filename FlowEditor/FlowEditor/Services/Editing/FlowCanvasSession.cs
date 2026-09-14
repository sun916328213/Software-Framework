using System.Collections.ObjectModel;
using FlowEditor.Core;
using FlowEditor.Interfaces;
using FlowEditor.Models;
using FlowEditor.Nodes;
using FlowEditor.Nodes.FlowControl;
using FlowEditor.ViewModels;

namespace FlowEditor.Services.Editing;

/// <summary>
/// 管理一个流程画布的节点生命周期、批量清理和视口操作。
/// 所有节点在此模块中统一附加到所属流程会话，避免调用方遗漏生命周期回调。
/// </summary>
internal sealed class FlowCanvasSession
{
    private readonly IFlowChartService _owner;
    private readonly FlowChartModel _model;
    private readonly ObservableCollection<NodeViewModel> _nodes;
    private readonly ObservableCollection<ConnectionModel> _connections;
    private readonly IUndoRedoService _undoRedo;
    private readonly FlowOutputBuffer _output;

    /// <summary>创建绑定到一个流程会话的画布模块。</summary>
    public FlowCanvasSession(
        IFlowChartService owner,
        FlowChartModel model,
        ObservableCollection<NodeViewModel> nodes,
        ObservableCollection<ConnectionModel> connections,
        IUndoRedoService undoRedo,
        FlowOutputBuffer output)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _undoRedo = undoRedo ?? throw new ArgumentNullException(nameof(undoRedo));
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    /// <summary>批量节点操作是否正在进行。</summary>
    public bool IsBatchOperation { get; set; }

    /// <summary>请求宿主将给定内容范围缩放到视口。</summary>
    public event EventHandler<(double contentWidth, double contentHeight, double offsetX, double offsetY)>?
        ZoomToFitRequested;

    /// <summary>放大一个标准步长。</summary>
    public void ZoomIn() =>
        _model.ZoomLevel = Math.Round(_model.ZoomLevel + FlowChartModel.ZoomStep, 2);

    /// <summary>缩小一个标准步长。</summary>
    public void ZoomOut() =>
        _model.ZoomLevel = Math.Round(_model.ZoomLevel - FlowChartModel.ZoomStep, 2);

    /// <summary>恢复 100% 缩放。</summary>
    public void ZoomReset() => _model.ZoomLevel = 1.0;

    /// <summary>计算全部节点视觉范围并请求宿主自适应显示。</summary>
    public void ZoomToFit()
    {
        if (_nodes.Count == 0)
        {
            ZoomReset();
            return;
        }

        var minX = _nodes.Min(node => node.X);
        var minY = _nodes.Min(node => node.Y);
        var maxX = _nodes.Max(node => node.X + GetVisualWidth(node.Model));
        var maxY = _nodes.Max(node => node.Y + GetVisualHeight(node.Model));
        ZoomToFitRequested?.Invoke(this,
            (maxX - minX + 200, maxY - minY + 200, minX - 100, minY - 100));
    }

    /// <summary>查找当前画布中的节点。</summary>
    public NodeViewModel? FindNode(string nodeId) =>
        _nodes.FirstOrDefault(node => node.Id == nodeId);

    /// <summary>清空节点、连线、撤销栈和输出，并恢复空画布状态。</summary>
    public void ClearAll()
    {
        _connections.Clear();
        ClearNodes();
        _undoRedo.Clear();
        _output.Clear();
        _model.StatusText = "已清空";
        _model.CurrentFilePath = null;
        _model.IsModified = false;
    }

    /// <summary>添加已创建的节点 ViewModel 并完成模型附加。</summary>
    public void AddNode(NodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        node.Model.AttachTo(_owner);
        _nodes.Add(node);
    }

    /// <summary>添加节点模型并返回其画布 ViewModel。</summary>
    public NodeViewModel AddNode(NodeModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        model.AttachTo(_owner);
        var viewModel = new NodeViewModel(model);
        _nodes.Add(viewModel);
        return viewModel;
    }

    /// <summary>移除节点并触发卸载生命周期。</summary>
    public void RemoveNode(NodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!_nodes.Contains(node)) return;
        node.Model.DetachFromFlow();
        _nodes.Remove(node);
    }

    /// <summary>批量卸载并清空全部节点。</summary>
    public void ClearNodes()
    {
        IsBatchOperation = true;
        try
        {
            foreach (var node in _nodes.ToList())
                RemoveNode(node);
        }
        finally
        {
            IsBatchOperation = false;
        }
    }

    /// <summary>解析一次拖动需要共同移动的选中节点、容器及其后代节点。</summary>
    internal IReadOnlyList<NodeViewModel> ResolveDragParticipants(
        NodeViewModel anchor,
        IEnumerable<NodeViewModel> selection)
    {
        var participants = selection;
        var movingContainer = IsCanvasContainer(anchor);
        if (anchor.Model is InactiveNodeContainerNode)
        {
            participants = participants.Concat(_nodes.Where(candidate =>
                candidate != anchor && IsInsideContainer(anchor, candidate)));
        }
        else if (anchor.Model is IExecutionScopeContainer)
        {
            participants = participants.Concat(_nodes.Where(candidate =>
                candidate != anchor && IsExecutionDescendantOf(candidate, anchor.Id)));
        }

        return participants
            .Where(node => movingContainer || node.IsDraggable)
            .Distinct()
            .ToList();
    }

    /// <summary>根据节点中心位置刷新“不执行”状态以及执行容器和泳道归属。</summary>
    internal void RefreshExecutionMembership(NodeViewModel node)
    {
        if (node.Model is InactiveNodeContainerNode)
        {
            node.IsExcludedFromExecution = true;
            foreach (var candidate in _nodes.Where(item =>
                         item != node && item.Model is not InactiveNodeContainerNode).ToList())
            {
                RefreshExecutionMembership(candidate);
            }
            return;
        }

        var inactiveContainer = _nodes.FirstOrDefault(container =>
            container.Model is InactiveNodeContainerNode &&
            IsInsideContainer(container, node));
        if (inactiveContainer != null)
        {
            var changed = !node.IsExcludedFromExecution || node.Model.ParentExecutionContainerId != null;
            node.IsExcludedFromExecution = true;
            node.Model.ParentExecutionContainerId = null;
            node.Model.ExecutionLaneId = null;
            if (changed)
            {
                RemoveConnectionsOutsideCurrentScope(node);
                _model.StatusText = $"节点 [{node.Name}] 已放入不执行容器";
            }
            return;
        }

        node.IsExcludedFromExecution = false;
        var executionContainer = _nodes
            .Where(container =>
                container != node &&
                container.Model is IExecutionScopeContainer &&
                !IsExecutionDescendantOf(container, node.Id) &&
                IsInsideContainer(container, node))
            .OrderBy(container => GetVisualWidth(container.Model) * GetVisualHeight(container.Model))
            .FirstOrDefault();

        var oldParentId = node.Model.ParentExecutionContainerId;
        var newParentId = executionContainer?.Id;
        var newLaneId = executionContainer == null ? null : ResolveLaneId(executionContainer, node);
        var membershipChanged = oldParentId != newParentId || node.Model.ExecutionLaneId != newLaneId;
        node.Model.ParentExecutionContainerId = newParentId;
        node.Model.ExecutionLaneId = newLaneId;

        if (!membershipChanged)
            return;

        if (!ShouldPreserveLoopBoundaryConnections(oldParentId, newParentId))
            RemoveConnectionsOutsideCurrentScope(node);
        _model.StatusText = executionContainer == null
            ? $"节点 [{node.Name}] 已移回主流程"
            : $"节点 [{node.Name}] 已加入 [{executionContainer.Name}] / [{GetLaneName(executionContainer, newLaneId)}]";
    }

    /// <summary>重新计算全部非停用容器节点的执行容器和泳道归属。</summary>
    internal void RefreshAllExecutionMembership()
    {
        foreach (var candidate in _nodes.Where(node =>
                     node.Model is not InactiveNodeContainerNode).ToList())
        {
            RefreshExecutionMembership(candidate);
        }
    }

    /// <summary>判断节点迁移时是否应保留循环容器边界连线。</summary>
    private bool ShouldPreserveLoopBoundaryConnections(string? oldParentId, string? newParentId)
    {
        if (oldParentId == newParentId) return true;
        var oldIsLoop = oldParentId != null && _nodes.Any(candidate =>
            candidate.Id == oldParentId && candidate.Model is LoopContainerNode);
        var newIsLoop = newParentId != null && _nodes.Any(candidate =>
            candidate.Id == newParentId && candidate.Model is LoopContainerNode);
        return (oldIsLoop && newParentId == null) || (newIsLoop && oldParentId == null);
    }

    /// <summary>判断节点中心是否位于指定画布容器范围内。</summary>
    private static bool IsInsideContainer(NodeViewModel container, NodeViewModel node)
    {
        var centerX = node.X + GetVisualWidth(node.Model) / 2;
        var centerY = node.Y + GetVisualHeight(node.Model) / 2;
        var right = container.X + GetVisualWidth(container.Model);
        var bottom = container.Y + GetVisualHeight(container.Model);
        return centerX >= container.X && centerX <= right &&
               centerY >= container.Y && centerY <= bottom;
    }

    /// <summary>判断节点是否为可在画布上承载子节点的容器。</summary>
    private static bool IsCanvasContainer(NodeViewModel node) =>
        node.Model is InactiveNodeContainerNode or IExecutionScopeContainer;

    /// <summary>判断节点是否属于指定执行容器的任意层级后代。</summary>
    private bool IsExecutionDescendantOf(NodeViewModel candidate, string containerId)
    {
        var parentId = candidate.Model.ParentExecutionContainerId;
        var visited = new HashSet<string>();
        while (parentId != null && visited.Add(parentId))
        {
            if (parentId == containerId) return true;
            parentId = _nodes.FirstOrDefault(node => node.Id == parentId)
                ?.Model.ParentExecutionContainerId;
        }
        return false;
    }

    /// <summary>解析节点在指定执行容器中的泳道标识。</summary>
    private static string? ResolveLaneId(NodeViewModel container, NodeViewModel node)
    {
        if (container.Model is not IExecutionScopeContainer scope || scope.ExecutionLanes.Count == 0)
            return null;
        const double headerHeight = 64;
        const double bottomMargin = 16;
        var contentHeight = Math.Max(1, scope.ContainerHeight - headerHeight - bottomMargin);
        var centerY = node.Y + GetVisualHeight(node.Model) / 2;
        var relativeY = Math.Clamp(centerY - container.Y - headerHeight, 0, contentHeight - 0.001);
        var index = Math.Clamp(
            (int)(relativeY / contentHeight * scope.ExecutionLanes.Count),
            0,
            scope.ExecutionLanes.Count - 1);
        return scope.ExecutionLanes[index].Id;
    }

    /// <summary>获取执行泳道的显示名称。</summary>
    private static string GetLaneName(NodeViewModel container, string? laneId) =>
        container.Model is IExecutionScopeContainer scope
            ? scope.ExecutionLanes.FirstOrDefault(lane => lane.Id == laneId)?.Name ?? "未指定泳道"
            : "未指定泳道";

    /// <summary>移除节点迁移后跨越当前执行作用域的非法连线。</summary>
    private void RemoveConnectionsOutsideCurrentScope(NodeViewModel node)
    {
        foreach (var connection in _connections.Where(connection =>
                     connection.SourceNodeId == node.Id || connection.TargetNodeId == node.Id).ToList())
        {
            var otherId = connection.SourceNodeId == node.Id
                ? connection.TargetNodeId
                : connection.SourceNodeId;
            var other = _nodes.FirstOrDefault(candidate => candidate.Id == otherId);
            if (other == null ||
                node.IsExcludedFromExecution ||
                other.Model.ParentExecutionContainerId != node.Model.ParentExecutionContainerId ||
                other.Model.ExecutionLaneId != node.Model.ExecutionLaneId)
            {
                _connections.Remove(connection);
            }
        }
    }

    /// <summary>返回节点对应的视觉宽度。</summary>
    internal static double GetVisualWidth(NodeModel node) => node switch
    {
        InactiveNodeContainerNode inactive => inactive.ContainerWidth,
        IExecutionScopeContainer execution => execution.ContainerWidth,
        _ => FlowChartModel.NodeWidth
    };

    /// <summary>返回节点对应的视觉高度。</summary>
    internal static double GetVisualHeight(NodeModel node) => node switch
    {
        InactiveNodeContainerNode inactive => inactive.ContainerHeight,
        IExecutionScopeContainer execution => execution.ContainerHeight,
        _ => FlowChartModel.NodeHeight
    };
}
