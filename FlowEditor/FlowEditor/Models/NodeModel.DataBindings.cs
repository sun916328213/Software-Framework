using System.Windows.Input;
using FlowEditor.Interfaces;
using FlowEditor.Models;
using WPFCustomControls.PropertyGrid;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowEditor.Nodes;

/// <summary>包含节点画布生命周期、连线订阅及属性面板数据绑定行为。</summary>
public abstract partial class NodeModel
{
    /// <summary>
    /// 当前流程图服务实例。由 FlowChartService 在添加节点时设置。
    /// 通过接口依赖，用于在属性面板中查询上游节点和创建连线。
    /// </summary>
    public IFlowChartService? Service { get; set; }

    /// <summary>节点被挂载到画布时回调。由 FlowChartService 在 AddNode 时调用。</summary>
    public virtual void OnAttached(IFlowChartService service) { }

    /// <summary>节点从画布移除时回调。由 FlowChartService 在移除节点时调用。</summary>
    public virtual void OnDetached() { }

    /// <summary>将节点附加到流程，并恢复其对流程级数据变化的订阅。</summary>
    internal void AttachTo(IFlowChartService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        if (ReferenceEquals(Service, service)) return;
        if (Service != null)
            DetachFromFlow();

        Service = service;
        try
        {
            OnAttached(service);
            if (_dataBindingsChanged != null)
                EnsureConnectionSubscription();
        }
        catch
        {
            DisconnectConnectionSubscription();
            Service = null;
            throw;
        }
    }

    /// <summary>从当前流程卸载节点，保证退订流程事件并释放流程引用。</summary>
    internal void DetachFromFlow()
    {
        if (Service == null) return;
        try
        {
            OnDetached();
        }
        finally
        {
            DisconnectConnectionSubscription();
            Service = null;
        }
    }

    /// <summary>数据绑定记录列表。</summary>
    private readonly List<DataBindingInfo> _dataBindings = new();

    private event Action? _dataBindingsChanged;
    private bool _connectionSubscribed;

    event Action? IDataBindingProvider.DataBindingsChanged
    {
        add
        {
            _dataBindingsChanged += value;
            EnsureConnectionSubscription();
        }
        remove => _dataBindingsChanged -= value;
    }

    /// <summary>首次需要数据绑定通知时订阅所属流程的连线变化。</summary>
    private void EnsureConnectionSubscription()
    {
        if (_connectionSubscribed) return;
        var service = Service;
        if (service == null)
        {
            System.Diagnostics.Debug.WriteLine($"[NodeModel] EnsureConnectionSubscription: Service is null for {Name}");
            return;
        }
        service.Connections.CollectionChanged += OnConnectionsChanged;
        _connectionSubscribed = true;
        System.Diagnostics.Debug.WriteLine($"[NodeModel] EnsureConnectionSubscription: subscribed to Connections.CollectionChanged for {Name}");
    }

    /// <summary>把流程连线集合变化转换为节点数据绑定变化通知。</summary>
    private void OnConnectionsChanged(
        object? sender,
        System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[NodeModel] OnConnectionsChanged: {e.Action}, {e.NewItems?.Count ?? 0} new items, node={Name}");
        _dataBindingsChanged?.Invoke();
    }

    /// <summary>退订当前流程的连线变化事件，并允许后续重新附加。</summary>
    private void DisconnectConnectionSubscription()
    {
        if (!_connectionSubscribed) return;
        if (Service != null)
            Service.Connections.CollectionChanged -= OnConnectionsChanged;
        _connectionSubscribed = false;
    }

    /// <summary>获取通过流程连线可反向到达的直接或间接上游节点。</summary>
    IReadOnlyList<UpstreamNodeInfo> IDataBindingProvider.GetUpstreamFlowNodes()
    {
        var service = Service;
        if (service == null) return Array.Empty<UpstreamNodeInfo>();

        var upstreamIds = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>();
        pending.Enqueue(Id);
        while (pending.TryDequeue(out var targetId))
        {
            foreach (var connection in service.Connections.Where(connection => connection.TargetNodeId == targetId))
            {
                if (connection.SourceNodeId == Id || !upstreamIds.Add(connection.SourceNodeId))
                    continue;
                pending.Enqueue(connection.SourceNodeId);
            }
        }

        // 保持画布节点顺序稳定，避免每次打开绑定窗口时条目跳动。
        return service.Nodes
            .Where(node => upstreamIds.Contains(node.Id))
            .Select(node => new UpstreamNodeInfo { NodeId = node.Id, NodeName = node.Name })
            .ToList();
    }

    /// <summary>获取指定上游节点的可绑定属性列表。</summary>
    IReadOnlyList<string> IDataBindingProvider.GetUpstreamOutputPorts(string upstreamNodeId)
    {
        var service = Service;
        if (service == null) return Array.Empty<string>();

        var node = service.FindNode(upstreamNodeId);
        if (node == null) return Array.Empty<string>();
        return node.GetBindableProperties();
    }

    /// <summary>创建数据绑定：将上游属性绑定到本节点输入端口。</summary>
    /// <param name="upstreamNodeId">上游节点 ID。</param>
    /// <param name="upstreamPropertyName">上游属性名，而不是端口名。</param>
    /// <param name="targetPortName">本节点输入端口名。</param>
    void IDataBindingProvider.CreateDataBinding(
        string upstreamNodeId,
        string upstreamPropertyName,
        string targetPortName)
    {
        var service = Service;
        if (service == null) return;

        var upstreamNode = service.FindNode(upstreamNodeId);
        if (upstreamNode == null) return;

        var outPort = upstreamNode.OutputPorts.FirstOrDefault(port =>
            port.DataType != PortType.Flow && port.PropertyName == upstreamPropertyName);
        if (outPort == null)
        {
            outPort = upstreamNode.AddCustomOutputPort(upstreamPropertyName, upstreamPropertyName);
            System.Diagnostics.Debug.WriteLine(
                $"[NodeModel] Auto-created output port '{upstreamPropertyName}' on node '{upstreamNode.Name}'");
        }

        var inPort = CustomInputPorts.FirstOrDefault(port => port.Name == targetPortName)
            ?? AddCustomInputPort(targetPortName);
        var exists = service.Connections.Any(connection =>
            connection.SourcePortId == outPort.Id && connection.TargetPortId == inPort.Id);
        if (exists) return;

        service.Connections.Add(new ConnectionModel
        {
            SourceNodeId = upstreamNodeId,
            SourcePortId = outPort.Id,
            TargetNodeId = Id,
            TargetPortId = inPort.Id
        });

        _dataBindings.Add(new DataBindingInfo
        {
            BindingId = $"{Id}_Binding_{upstreamNodeId}_{inPort.Id}",
            UpstreamNodeId = upstreamNodeId,
            UpstreamNodeName = upstreamNode.Name,
            PropertyName = upstreamPropertyName,
            SourcePortName = outPort.Name,
            TargetPortId = inPort.Id
        });

        service.Model.IsModified = true;
    }

    /// <summary>获取当前所有数据绑定，包括显式创建和从实际连线检测得到的绑定。</summary>
    IReadOnlyList<DataBindingInfo> IDataBindingProvider.GetDataBindings()
    {
        var result = new List<DataBindingInfo>(_dataBindings);
        var service = Service;
        if (service == null)
        {
            System.Diagnostics.Debug.WriteLine($"[NodeModel] GetDataBindings: Service is null for {Name}");
            return result.AsReadOnly();
        }

        var inputPortIds = InputPorts
            .Where(port => port.DataType != PortType.Flow)
            .Select(port => port.Id)
            .ToHashSet();
        var connections = service.Connections
            .Where(connection => inputPortIds.Contains(connection.TargetPortId))
            .ToList();

        foreach (var connection in connections)
        {
            var upstreamNode = service.FindNode(connection.SourceNodeId);
            if (upstreamNode == null || upstreamNode.Id == Id)
                continue;

            var sourcePort = upstreamNode.FindPortById(connection.SourcePortId);
            var targetPort = InputPorts.FirstOrDefault(port => port.Id == connection.TargetPortId);
            if (sourcePort == null || targetPort == null ||
                result.Any(binding => binding.TargetPortId == connection.TargetPortId))
            {
                continue;
            }

            var propertyName = sourcePort.PropertyName ?? sourcePort.Name;
            result.Add(new DataBindingInfo
            {
                BindingId = $"auto_{Id}_{connection.SourceNodeId}_{connection.TargetPortId}",
                UpstreamNodeId = connection.SourceNodeId,
                UpstreamNodeName = upstreamNode.Name,
                PropertyName = propertyName,
                SourcePortName = sourcePort.Name,
                TargetPortId = connection.TargetPortId
            });
        }

        return result.AsReadOnly();
    }

    /// <summary>删除数据绑定及其关联连线，保留目标输入端口。</summary>
    void IDataBindingProvider.RemoveDataBinding(string bindingId)
    {
        var binding = _dataBindings.FirstOrDefault(candidate => candidate.BindingId == bindingId)
            ?? ((IDataBindingProvider)this).GetDataBindings()
                .FirstOrDefault(candidate => candidate.BindingId == bindingId);
        if (binding == null) return;

        var service = Service;
        if (service == null) return;

        var inputPortIds = InputPorts.Select(port => port.Id).ToHashSet();
        var connections = service.Connections
            .Where(connection =>
                connection.SourceNodeId == binding.UpstreamNodeId &&
                connection.TargetNodeId == Id &&
                (string.IsNullOrEmpty(binding.TargetPortId)
                    ? inputPortIds.Contains(connection.TargetPortId)
                    : connection.TargetPortId == binding.TargetPortId))
            .ToList();
        foreach (var connection in connections)
            service.Connections.Remove(connection);

        _dataBindings.RemoveAll(candidate => candidate.BindingId == bindingId);
        service.Model.IsModified = true;
    }

    /// <summary>打开脚本编辑器命令。子类重写后可被 PropertyGridHost 自动检测为扩展按钮。</summary>
    [PropertyGridAction("打开脚本编辑器")]
    public virtual ICommand? OpenScriptEditorCommand => null;
}
