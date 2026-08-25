namespace WPFCustomControls.PropertyGrid.Models;

/// <summary>
/// 端口对话框请求参数（纯数据，由 ViewModel 构造）。
/// </summary>
public record PortDialogRequest
{
    /// <summary>对话框标题。</summary>
    public string Title { get; init; } = "添加端口";

    /// <summary>是否为输入端口（true=输入端口对话框，false=输出端口对话框）。</summary>
    public bool IsInputPort { get; init; } = true;

    /// <summary>本节点可绑定的属性列表。</summary>
    public IReadOnlyList<string> AvailableProperties { get; init; } = Array.Empty<string>();

    /// <summary>上游节点列表（用于"绑定上游数据"选项）。</summary>
    public IReadOnlyList<UpstreamNodeInfo> UpstreamNodes { get; init; } = Array.Empty<UpstreamNodeInfo>();

    /// <summary>获取上游节点已有输出端口名称的回调。</summary>
    public Func<string, IReadOnlyList<string>>? GetUpstreamPorts { get; init; }

    /// <summary>修改绑定时的当前端口名称（只读显示用）。</summary>
    public string? CurrentPortName { get; init; }

    /// <summary>修改绑定时的当前上游节点 Id（预填用）。</summary>
    public string? CurrentUpstreamNodeId { get; init; }

    /// <summary>修改绑定时的当前上游输出端口名（预填用）。</summary>
    public string? CurrentUpstreamPortName { get; init; }
}

/// <summary>
/// 端口对话框结果（纯数据，由 ViewModel 消费）。
/// </summary>
public record PortDialogResult
{
    /// <summary>端口名称。</summary>
    public string? PortName { get; init; }

    /// <summary>绑定的工具属性名。</summary>
    public string? BoundPropertyName { get; init; }

    /// <summary>是否选择了"绑定工具属性"。</summary>
    public bool IsToolProperty { get; init; }

    /// <summary>是否选择了"绑定上游数据"。</summary>
    public bool IsUpstreamData { get; init; }

    /// <summary>选中的上游节点 Id。</summary>
    public string? UpstreamNodeId { get; init; }

    /// <summary>选中的上游输出端口名。</summary>
    public string? UpstreamPortName { get; init; }
}

/// <summary>
/// 端口对话框服务，隔离 ViewModel 与具体 View（MVVM：ViewModel 不得直接引用 View）。
/// </summary>
public interface IPortDialogService
{
    /// <summary>显示添加端口对话框，取消时返回 null。</summary>
    PortDialogResult? ShowAddPortDialog(PortDialogRequest request);

    /// <summary>显示修改绑定对话框，取消时返回 null。</summary>
    PortDialogResult? ShowModifyBindingDialog(PortDialogRequest request);
}
