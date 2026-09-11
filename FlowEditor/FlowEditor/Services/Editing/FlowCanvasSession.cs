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
        node.Model.Service = _owner;
        _nodes.Add(node);
        node.Model.OnAttached(_owner);
    }

    /// <summary>添加节点模型并返回其画布 ViewModel。</summary>
    public NodeViewModel AddNode(NodeModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        model.Service = _owner;
        var viewModel = new NodeViewModel(model);
        _nodes.Add(viewModel);
        model.OnAttached(_owner);
        return viewModel;
    }

    /// <summary>移除节点并触发卸载生命周期。</summary>
    public void RemoveNode(NodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        node.Model.OnDetached();
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

    /// <summary>返回节点对应的视觉宽度。</summary>
    private static double GetVisualWidth(NodeModel node) => node switch
    {
        InactiveNodeContainerNode inactive => inactive.ContainerWidth,
        IExecutionScopeContainer execution => execution.ContainerWidth,
        _ => FlowChartModel.NodeWidth
    };

    /// <summary>返回节点对应的视觉高度。</summary>
    private static double GetVisualHeight(NodeModel node) => node switch
    {
        InactiveNodeContainerNode inactive => inactive.ContainerHeight,
        IExecutionScopeContainer execution => execution.ContainerHeight,
        _ => FlowChartModel.NodeHeight
    };
}
