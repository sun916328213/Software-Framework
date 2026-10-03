// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：NodeModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls、FlowEditor.Core.Nodes、System.Collections.ObjectModel、System.Reflection、FlowEditor.Controls.Models、FlowEditor.Core.Models。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls;
using FlowEditor.Core.Nodes;
using System.Collections.ObjectModel;
using System.Reflection;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using WPFCustomControls.PropertyGrid.Core.Models;

namespace FlowEditor.Controls.Nodes;

/// <summary>包含标准/动态端口集合、缓存、编辑和属性端口配置行为。</summary>
public abstract partial class NodeModel
{
    /// <summary>自定义输入端口列表（用户手动添加，位于节点左侧）。</summary>
    public ObservableCollection<PortModel> CustomInputPorts { get; } = new();

    /// <summary>自定义输出端口列表（用户手动添加，位于节点右侧，可绑定属性）。</summary>
    public ObservableCollection<PortModel> CustomOutputPorts { get; } = new();

    private List<PortModel>? _cachedInputPorts;
    private List<PortModel>? _cachedOutputPorts;

    /// <summary>输入端口列表（内置标准端口 + 自定义端口）。</summary>
    public List<PortModel> InputPorts => _cachedInputPorts ??= BuildInputPorts();

    /// <summary>输出端口列表（内置标准端口 + 自定义端口）。</summary>
    public List<PortModel> OutputPorts => _cachedOutputPorts ??= BuildOutputPorts();

    /// <summary>组合可选标准输入端口和动态自定义输入端口。</summary>
    private List<PortModel> BuildInputPorts()
    {
        var ports = new List<PortModel>();
        if (IsInEnabled)
        {
            ports.Add(new PortModel
            {
                Id = $"{Id}_Next_IN",
                Name = "Next_IN",
                Direction = PortDirection.Input,
                DataType = PortType.Flow,
                OwnerNodeId = Id,
                Category = PortCategory.Standard
            });
        }
        ports.AddRange(CustomInputPorts);
        return ports;
    }

    /// <summary>组合可选标准输出端口和动态自定义输出端口。</summary>
    private List<PortModel> BuildOutputPorts()
    {
        var ports = new List<PortModel>();
        if (IsOutEnabled)
        {
            ports.Add(new PortModel
            {
                Id = $"{Id}_Next_Out",
                Name = "Next_Out",
                Direction = PortDirection.Output,
                DataType = PortType.Flow,
                OwnerNodeId = Id,
                Category = PortCategory.Standard
            });
        }
        ports.AddRange(CustomOutputPorts);
        return ports;
    }

    /// <summary>使端口缓存失效（端口变更后调用）。</summary>
    public void InvalidatePorts()
    {
        _cachedInputPorts = null;
        _cachedOutputPorts = null;
        OnPropertyChanged(nameof(InputPorts));
        OnPropertyChanged(nameof(OutputPorts));
    }

    /// <summary>添加自定义输入端口。</summary>
    public PortModel AddCustomInputPort(string name, string? boundProperty = null) =>
        AddCustomPort(name, boundProperty, PortDirection.Input);

    /// <summary>添加自定义输出端口（可绑定节点属性）。</summary>
    public PortModel AddCustomOutputPort(string name, string? boundProperty = null) =>
        AddCustomPort(name, boundProperty, PortDirection.Output);

    /// <summary>创建指定方向的自定义端口并使聚合端口缓存失效。</summary>
    private PortModel AddCustomPort(string name, string? boundProperty, PortDirection direction)
    {
        var displayName = NormalizePortDisplayName(name, boundProperty);
        var port = new PortModel
        {
            Id = $"{Id}_{(direction == PortDirection.Input ? "CustomIn" : "CustomOut")}_{Guid.NewGuid():N}",
            Name = displayName,
            PropertyName = boundProperty,
            Direction = direction,
            DataType = PortTypeClassifier.FromClrType(
                GetType().GetProperty(boundProperty ?? name)?.PropertyType),
            OwnerNodeId = Id,
            Category = PortCategory.Custom
        };
        GetCustomPorts(direction).Add(port);
        InvalidatePorts();
        return port;
    }

    /// <summary>删除自定义端口并同步连接和属性端口配置。</summary>
    public bool RemoveCustomPort(string portId)
    {
        var input = CustomInputPorts.FirstOrDefault(port => port.Id == portId);
        if (input != null)
        {
            CustomInputPorts.Remove(input);
            RemovePortConnections(portId);
            SynchronizeRemovedPortConfig(input);
            InvalidatePorts();
            ConfigChanged?.Invoke();
            return true;
        }

        var output = CustomOutputPorts.FirstOrDefault(port => port.Id == portId);
        if (output == null) return false;
        CustomOutputPorts.Remove(output);
        RemovePortConnections(portId);
        SynchronizeRemovedPortConfig(output);
        InvalidatePorts();
        ConfigChanged?.Invoke();
        return true;
    }

    /// <summary>删除端口后根据剩余端口回写属性端口配置，并兼容旧文档端口名。</summary>
    private void SynchronizeRemovedPortConfig(PortModel port)
    {
        var propertyName = port.PropertyName;
        if (string.IsNullOrWhiteSpace(propertyName) && GetType().GetProperty(port.Name) != null)
            propertyName = port.Name;
        if (string.IsNullOrWhiteSpace(propertyName))
            return;

        var hasInput = CustomInputPorts.Any(candidate =>
            candidate.PropertyName == propertyName || candidate.Name == propertyName);
        var hasOutput = CustomOutputPorts.Any(candidate =>
            candidate.PropertyName == propertyName || candidate.Name == propertyName);
        if (hasInput || hasOutput)
            _portConfigs[propertyName] = (hasInput, hasOutput);
        else
            _portConfigs.Remove(propertyName);
    }

    /// <summary>删除引用指定端口的连线，避免留下阻塞后续连接的悬空记录。</summary>
    private void RemovePortConnections(string portId)
    {
        var service = Service;
        if (service == null) return;

        var connections = service.Connections
            .Where(connection => connection.SourcePortId == portId || connection.TargetPortId == portId)
            .ToList();
        foreach (var connection in connections)
            service.Connections.Remove(connection);
        if (connections.Count > 0)
            service.Model.IsModified = true;
    }

    System.Collections.IEnumerable IPortManageable.CustomInputPorts =>
        CustomInputPorts.Where(port => port.DataType != PortType.Flow);

    System.Collections.IEnumerable IPortManageable.CustomOutputPorts =>
        CustomOutputPorts.Where(port => port.DataType != PortType.Flow);

    /// <summary>
    /// 执行添加操作；对应入口为 AddCustomInputPort。
    /// 正文中的关键协作调用包括 AddCustomInputPort；具体条件和先后次序见方法体。
    /// </summary>
    void IPortManageable.AddCustomInputPort(string name) => AddCustomInputPort(name);

    /// <summary>
    /// 执行添加操作；对应入口为 AddCustomOutputPort。
    /// 正文中的关键协作调用包括 AddCustomOutputPort；具体条件和先后次序见方法体。
    /// </summary>
    void IPortManageable.AddCustomOutputPort(string name, string? boundProperty) =>
        AddCustomOutputPort(name, boundProperty);

    /// <summary>重命名自定义端口。</summary>
    public bool RenameCustomPort(string portId, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return false;
        var port = CustomInputPorts.FirstOrDefault(candidate => candidate.Id == portId)
            ?? CustomOutputPorts.FirstOrDefault(candidate => candidate.Id == portId);
        if (port == null) return false;
        port.Name = newName.Trim();
        InvalidatePorts();
        return true;
    }

    private readonly Dictionary<string, (bool IsInput, bool IsOutput)> _portConfigs = new();

    /// <summary>端口配置变更事件，属性面板监听此事件同步 UI。</summary>
    public event Action? ConfigChanged;

    /// <inheritdoc />
    /// <remarks>
    /// 获取当前实现提供的值或对象；对应入口为 GetPortConfig。
    /// 正文中的关键协作调用包括 TryGetValue、Any；具体条件和先后次序见方法体。
    /// </remarks>
    public (bool IsInput, bool IsOutput) GetPortConfig(string propertyName)
    {
        if (_portConfigs.TryGetValue(propertyName, out var config))
            return config;

        var hasInput = CustomInputPorts.Any(port =>
            port.PropertyName == propertyName || port.Name == propertyName);
        var hasOutput = CustomOutputPorts.Any(port =>
            port.PropertyName == propertyName || port.Name == propertyName);
        return (hasInput, hasOutput);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 设置当前实现管理的值或状态；对应入口为 SetPortConfig。
    /// 正文中的关键协作调用包括 GetPropertyDisplayName、UpdatePropertyPort、InvalidatePorts；具体条件和先后次序见方法体。
    /// </remarks>
    public void SetPortConfig(string propertyName, bool isInput, bool isOutput)
    {
        _portConfigs[propertyName] = (isInput, isOutput);
        var displayName = GetPropertyDisplayName(propertyName);
        UpdatePropertyPort(propertyName, displayName, PortDirection.Input, isInput);
        UpdatePropertyPort(propertyName, displayName, PortDirection.Output, isOutput);
        InvalidatePorts();
        ConfigChanged?.Invoke();
    }

    /// <summary>按照属性端口配置创建、更新或删除对应方向的端口。</summary>
    private void UpdatePropertyPort(
        string propertyName,
        string displayName,
        PortDirection direction,
        bool enabled)
    {
        var ports = GetCustomPorts(direction);
        var existing = ports.FirstOrDefault(port =>
            port.PropertyName == propertyName || port.Name == propertyName);
        if (!enabled)
        {
            if (existing != null)
            {
                RemovePortConnections(existing.Id);
                ports.Remove(existing);
            }
            return;
        }

        if (existing == null)
        {
            ports.Add(new PortModel
            {
                Id = $"{Id}_Property{(direction == PortDirection.Input ? "In" : "Out")}_{propertyName}",
                Name = displayName,
                PropertyName = propertyName,
                Direction = direction,
                DataType = PortTypeClassifier.FromClrType(
                    GetType().GetProperty(propertyName)?.PropertyType),
                OwnerNodeId = Id,
                Category = PortCategory.Custom
            });
            return;
        }

        if (string.IsNullOrWhiteSpace(existing.Name) || existing.Name == propertyName)
            existing.Name = displayName;
        existing.PropertyName = propertyName;
    }

    /// <summary>获取指定方向的可变自定义端口集合。</summary>
    private ObservableCollection<PortModel> GetCustomPorts(PortDirection direction) =>
        direction == PortDirection.Input ? CustomInputPorts : CustomOutputPorts;

    /// <summary>读取节点属性的展示名，不存在元数据时返回 CLR 属性名。</summary>
    private string GetPropertyDisplayName(string propertyName)
    {
        var property = GetType().GetProperty(propertyName);
        var attribute = property?.GetCustomAttribute<NodePropertyAttribute>();
        return string.IsNullOrWhiteSpace(attribute?.DisplayName)
            ? propertyName
            : attribute.DisplayName;
    }

    /// <summary>把属性元数据或历史端口信息归一化为非空端口显示名。</summary>
    private string NormalizePortDisplayName(string? name, string? boundProperty)
    {
        if (!string.IsNullOrWhiteSpace(name))
            return name;
        if (!string.IsNullOrWhiteSpace(boundProperty))
            return GetPropertyDisplayName(boundProperty);
        return "未命名端口";
    }
}
