// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：FlowCanvasViewModelAdapter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、System.Collections.ObjectModel、FlowEditor.Controls.Models、FlowEditor.Core.Models、FlowEditor.Controls.Nodes、FlowEditor.Core.Nodes。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using System.Collections.ObjectModel;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.Services.Editing;

namespace FlowEditor.Controls.ViewModels;

/// <summary>
/// 在模型级画布会话与节点 ViewModel 集合之间转换。
/// 仅负责表现层包装和集合同步，容器归属、几何及生命周期规则由画布会话拥有。
/// </summary>
internal sealed class FlowCanvasViewModelAdapter
{
    private readonly FlowCanvasSession _session;
    private readonly ObservableCollection<NodeViewModel> _nodes;

    /// <summary>创建绑定到指定节点投影集合的画布适配器。</summary>
    public FlowCanvasViewModelAdapter(
        FlowCanvasSession session,
        ObservableCollection<NodeViewModel> nodes)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
    }

    /// <summary>转发模型会话的视口自适应请求。</summary>
    public event EventHandler<(double contentWidth, double contentHeight, double offsetX, double offsetY)>?
        ZoomToFitRequested
    {
        add => _session.ZoomToFitRequested += value;
        remove => _session.ZoomToFitRequested -= value;
    }

    /// <summary>放大一个标准步长。</summary>
    public void ZoomIn() => _session.ZoomIn();

    /// <summary>缩小一个标准步长。</summary>
    public void ZoomOut() => _session.ZoomOut();

    /// <summary>恢复 100% 缩放。</summary>
    public void ZoomReset() => _session.ZoomReset();

    /// <summary>请求宿主显示全部画布内容。</summary>
    public void ZoomToFit() => _session.ZoomToFit();

    /// <summary>查找当前画布中的节点投影。</summary>
    public NodeViewModel? FindNode(string nodeId) =>
        _nodes.FirstOrDefault(node => node.Id == nodeId);

    /// <summary>附加节点模型并创建对应的表现层投影。</summary>
    public NodeViewModel AddNode(NodeModel model)
    {
        var attached = _session.AddNode(model);
        var viewModel = new NodeViewModel(attached);
        _nodes.Add(viewModel);
        return viewModel;
    }

    /// <summary>附加已有节点投影及其模型。</summary>
    public void AddNode(NodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        _session.AddNode(node.Model);
        _nodes.Add(node);
    }

    /// <summary>移除节点投影并卸载对应模型。</summary>
    public void RemoveNode(NodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!_nodes.Contains(node)) return;
        _session.RemoveNode(node.Model);
        _nodes.Remove(node);
    }

    /// <summary>批量卸载并清空全部节点投影。</summary>
    public void ClearNodes()
    {
        _session.ClearNodes();
        _nodes.Clear();
    }

    /// <summary>清空当前画布的模型状态与节点投影。</summary>
    public void ClearAll()
    {
        _session.ClearAll();
        _nodes.Clear();
    }

    /// <summary>解析拖动参与模型并映射回现有节点投影。</summary>
    internal IReadOnlyList<NodeViewModel> ResolveDragParticipants(
        NodeViewModel anchor,
        IEnumerable<NodeViewModel> selection)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        ArgumentNullException.ThrowIfNull(selection);
        var byModel = _nodes.ToDictionary(node => node.Model);
        return _session.ResolveDragParticipants(anchor.Model, selection.Select(node => node.Model))
            .Select(model => byModel[model])
            .ToList();
    }

    /// <summary>刷新一个节点模型的容器和泳道归属。</summary>
    internal void RefreshExecutionMembership(NodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        _session.RefreshExecutionMembership(node.Model);
    }

    /// <summary>刷新全部节点模型的容器和泳道归属。</summary>
    internal void RefreshAllExecutionMembership() =>
        _session.RefreshAllExecutionMembership();
}
