namespace WPFCustomControls.PropertyGrid.Models;

/// <summary>上游节点信息。</summary>
public class UpstreamNodeInfo
{
    /// <summary>节点 ID。</summary>
    public string NodeId { get; set; } = string.Empty;
    /// <summary>节点显示名称。</summary>
    public string NodeName { get; set; } = string.Empty;
}

/// <summary>数据绑定信息。</summary>
public class DataBindingInfo
{
    /// <summary>绑定唯一标识。</summary>
    public string BindingId { get; set; } = string.Empty;
    /// <summary>上游节点 ID。</summary>
    public string UpstreamNodeId { get; set; } = string.Empty;
    /// <summary>上游节点显示名称。</summary>
    public string UpstreamNodeName { get; set; } = string.Empty;
    /// <summary>绑定的属性名（上游端口绑定的属性，仅用于显示）。</summary>
    public string PropertyName { get; set; } = string.Empty;
    /// <summary>上游输出端口名称（修改绑定时预填用）。</summary>
    public string SourcePortName { get; set; } = string.Empty;
    /// <summary>目标端口 ID（用于匹配端口行）。</summary>
    public string TargetPortId { get; set; } = string.Empty;
}

/// <summary>
/// 目标对象实现此接口后，属性网格会自动渲染数据绑定区域。
/// 允许用户从上游节点已有的输出端口中选择，连接到本节点自定义命名的输入端口。
/// </summary>
public interface IDataBindingProvider
{
    /// <summary>连线变更时触发，通知 UI 刷新绑定状态。</summary>
    event Action? DataBindingsChanged;

    /// <summary>获取所有上游节点（通过 Flow 连线连接的节点）。</summary>
    IReadOnlyList<UpstreamNodeInfo> GetUpstreamFlowNodes();

    /// <summary>获取指定上游节点已有的输出端口名称列表（不含 Flow 端口）。</summary>
    IReadOnlyList<string> GetUpstreamOutputPorts(string upstreamNodeId);

    /// <summary>创建数据绑定：将上游已有输出端口连接到本节点指定名称的输入端口（端口不存在时按该名称创建）。</summary>
    void CreateDataBinding(string upstreamNodeId, string upstreamPortName, string targetPortName);

    /// <summary>获取当前所有数据绑定。</summary>
    IReadOnlyList<DataBindingInfo> GetDataBindings();

    /// <summary>删除数据绑定。</summary>
    void RemoveDataBinding(string bindingId);
}