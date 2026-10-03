// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：FlowCanvasSession。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Controls.Services、FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime、FlowEditor.Controls.Interfaces。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Controls.Services;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Interfaces;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.Nodes.FlowControl;

namespace FlowEditor.Controls.Services.Editing;

/// <summary>
/// 管理一个流程画布的节点生命周期、批量清理和视口操作。
/// 所有节点在此模块中统一附加到所属流程会话，避免调用方遗漏生命周期回调。
/// </summary>
internal sealed class FlowCanvasSession
{
    private readonly IFlowChartService _owner;
    private readonly FlowChartModel _model;
    private readonly Func<IReadOnlyList<NodeModel>> _nodeSnapshot;
    private readonly ICollection<ConnectionModel> _connections;
    private readonly IUndoRedoService _undoRedo;
    private readonly FlowOutputBuffer _output;

    /// <summary>创建绑定到一个流程会话的画布模块。</summary>
    public FlowCanvasSession(
        IFlowChartService owner,
        FlowChartModel model,
        Func<IReadOnlyList<NodeModel>> nodeSnapshot,
        ICollection<ConnectionModel> connections,
        IUndoRedoService undoRedo,
        FlowOutputBuffer output)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _nodeSnapshot = nodeSnapshot ?? throw new ArgumentNullException(nameof(nodeSnapshot));
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _undoRedo = undoRedo ?? throw new ArgumentNullException(nameof(undoRedo));
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

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
        var nodes = GetNodes();
        if (nodes.Count == 0)
        {
            ZoomReset();
            return;
        }

        var minX = nodes.Min(node => node.X);
        var minY = nodes.Min(node => node.Y);
        var maxX = nodes.Max(node => node.X + GetVisualWidth(node));
        var maxY = nodes.Max(node => node.Y + GetVisualHeight(node));
        ZoomToFitRequested?.Invoke(this,
            (maxX - minX + 200, maxY - minY + 200, minX - 100, minY - 100));
    }

    /// <summary>查找当前画布中的节点。</summary>
    public NodeModel? FindNode(string nodeId) =>
        GetNodes().FirstOrDefault(node => node.Id == nodeId);

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

    /// <summary>把节点模型附加到当前流程，并返回该模型。</summary>
    public NodeModel AddNode(NodeModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        model.AttachTo(_owner);
        return model;
    }

    /// <summary>移除节点并触发卸载生命周期。</summary>
    public void RemoveNode(NodeModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!GetNodes().Contains(node)) return;
        node.DetachFromFlow();
    }

    /// <summary>批量卸载并清空全部节点。</summary>
    public void ClearNodes()
    {
        foreach (var node in GetNodes())
            node.DetachFromFlow(isBatchOperation: true);
    }

    /// <summary>解析一次拖动需要共同移动的选中节点、容器及其后代节点。</summary>
    internal IReadOnlyList<NodeModel> ResolveDragParticipants(
        NodeModel anchor,
        IEnumerable<NodeModel> selection)
    {
        var nodes = GetNodes();
        var participants = selection;
        var movingContainer = IsCanvasContainer(anchor);
        if (anchor is InactiveNodeContainerNode)
        {
            participants = participants.Concat(nodes.Where(candidate =>
                candidate != anchor && IsInsideContainer(anchor, candidate)));
        }
        else if (anchor is IExecutionScopeContainer)
        {
            participants = participants.Concat(nodes.Where(candidate =>
                candidate != anchor && IsExecutionDescendantOf(candidate, anchor.Id)));
        }

        return participants
            .Where(node => movingContainer || node.IsDraggable)
            .Distinct()
            .ToList();
    }

    /// <summary>根据节点中心位置刷新“不执行”状态以及执行容器和泳道归属。</summary>
    internal void RefreshExecutionMembership(NodeModel node)
    {
        var nodes = GetNodes();
        if (node is InactiveNodeContainerNode)
        {
            node.IsExcludedFromExecution = true;
            foreach (var candidate in nodes.Where(item =>
                         item != node && item is not InactiveNodeContainerNode).ToList())
            {
                RefreshExecutionMembership(candidate);
            }
            return;
        }

        var inactiveContainer = nodes.FirstOrDefault(container =>
            container is InactiveNodeContainerNode &&
            IsInsideContainer(container, node));
        if (inactiveContainer != null)
        {
            var changed = !node.IsExcludedFromExecution || node.ParentExecutionContainerId != null;
            node.IsExcludedFromExecution = true;
            node.ParentExecutionContainerId = null;
            node.ExecutionLaneId = null;
            if (changed)
            {
                RemoveConnectionsOutsideCurrentScope(node);
                _model.StatusText = $"节点 [{node.Name}] 已放入不执行容器";
            }
            return;
        }

        node.IsExcludedFromExecution = false;
        var executionContainer = nodes
            .Where(container =>
                container != node &&
                container is IExecutionScopeContainer &&
                !IsExecutionDescendantOf(container, node.Id) &&
                IsInsideContainer(container, node))
            .OrderBy(container => GetVisualWidth(container) * GetVisualHeight(container))
            .FirstOrDefault();

        var oldParentId = node.ParentExecutionContainerId;
        var newParentId = executionContainer?.Id;
        var newLaneId = executionContainer == null ? null : ResolveLaneId(executionContainer, node);
        var membershipChanged = oldParentId != newParentId || node.ExecutionLaneId != newLaneId;
        node.ParentExecutionContainerId = newParentId;
        node.ExecutionLaneId = newLaneId;

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
        foreach (var candidate in GetNodes().Where(node =>
                     node is not InactiveNodeContainerNode).ToList())
        {
            RefreshExecutionMembership(candidate);
        }
    }

    /// <summary>判断节点迁移时是否应保留循环容器边界连线。</summary>
    private bool ShouldPreserveLoopBoundaryConnections(string? oldParentId, string? newParentId)
    {
        if (oldParentId == newParentId) return true;
        var nodes = GetNodes();
        var oldIsLoop = oldParentId != null && nodes.Any(candidate =>
            candidate.Id == oldParentId && candidate is LoopContainerNode);
        var newIsLoop = newParentId != null && nodes.Any(candidate =>
            candidate.Id == newParentId && candidate is LoopContainerNode);
        return (oldIsLoop && newParentId == null) || (newIsLoop && oldParentId == null);
    }

    /// <summary>判断节点中心是否位于指定画布容器范围内。</summary>
    private static bool IsInsideContainer(NodeModel container, NodeModel node)
    {
        var centerX = node.X + GetVisualWidth(node) / 2;
        var centerY = node.Y + GetVisualHeight(node) / 2;
        var right = container.X + GetVisualWidth(container);
        var bottom = container.Y + GetVisualHeight(container);
        return centerX >= container.X && centerX <= right &&
               centerY >= container.Y && centerY <= bottom;
    }

    /// <summary>判断节点是否为可在画布上承载子节点的容器。</summary>
    private static bool IsCanvasContainer(NodeModel node) =>
        node is InactiveNodeContainerNode or IExecutionScopeContainer;

    /// <summary>判断节点是否属于指定执行容器的任意层级后代。</summary>
    private bool IsExecutionDescendantOf(NodeModel candidate, string containerId)
    {
        var parentId = candidate.ParentExecutionContainerId;
        var visited = new HashSet<string>();
        while (parentId != null && visited.Add(parentId))
        {
            if (parentId == containerId) return true;
            parentId = GetNodes().FirstOrDefault(node => node.Id == parentId)
                ?.ParentExecutionContainerId;
        }
        return false;
    }

    /// <summary>解析节点在指定执行容器中的泳道标识。</summary>
    private static string? ResolveLaneId(NodeModel container, NodeModel node)
    {
        if (container is not IExecutionScopeContainer scope || scope.ExecutionLanes.Count == 0)
            return null;
        const double headerHeight = 64;
        const double bottomMargin = 16;
        var contentHeight = Math.Max(1, scope.ContainerHeight - headerHeight - bottomMargin);
        var centerY = node.Y + GetVisualHeight(node) / 2;
        var relativeY = Math.Clamp(centerY - container.Y - headerHeight, 0, contentHeight - 0.001);
        var index = Math.Clamp(
            (int)(relativeY / contentHeight * scope.ExecutionLanes.Count),
            0,
            scope.ExecutionLanes.Count - 1);
        return scope.ExecutionLanes[index].Id;
    }

    /// <summary>获取执行泳道的显示名称。</summary>
    private static string GetLaneName(NodeModel container, string? laneId) =>
        container is IExecutionScopeContainer scope
            ? scope.ExecutionLanes.FirstOrDefault(lane => lane.Id == laneId)?.Name ?? "未指定泳道"
            : "未指定泳道";

    /// <summary>移除节点迁移后跨越当前执行作用域的非法连线。</summary>
    private void RemoveConnectionsOutsideCurrentScope(NodeModel node)
    {
        foreach (var connection in _connections.Where(connection =>
                     connection.SourceNodeId == node.Id || connection.TargetNodeId == node.Id).ToList())
        {
            var otherId = connection.SourceNodeId == node.Id
                ? connection.TargetNodeId
                : connection.SourceNodeId;
            var other = GetNodes().FirstOrDefault(candidate => candidate.Id == otherId);
            if (other == null ||
                node.IsExcludedFromExecution ||
                other.ParentExecutionContainerId != node.ParentExecutionContainerId ||
                other.ExecutionLaneId != node.ExecutionLaneId)
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

    /// <summary>获取稳定的当前节点快照，供一次画布规则计算使用。</summary>
    private IReadOnlyList<NodeModel> GetNodes() => _nodeSnapshot();
}
