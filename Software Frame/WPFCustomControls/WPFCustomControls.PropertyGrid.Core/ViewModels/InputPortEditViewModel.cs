// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：InputPortEditViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections.ObjectModel、CommunityToolkit.Mvvm.ComponentModel、CommunityToolkit.Mvvm.Input、WPFCustomControls.PropertyGrid.Core.Models。导入不代表所有类型都会在运行时加载。
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WPFCustomControls.PropertyGrid.Core.Models;

namespace WPFCustomControls.PropertyGrid.Core.ViewModels;

/// <summary>输入端口编辑对话框 ViewModel（绑定上游数据 / 自定义端口两种模式）。</summary>
public partial class InputPortEditViewModel : ObservableObject
{
    [ObservableProperty] private bool _isModifyMode;

    [ObservableProperty] private string? _currentPortName;

    [ObservableProperty] private string? _portName;

    [ObservableProperty] private UpstreamNodeInfo? _selectedUpstreamNode;

    [ObservableProperty] private string? _selectedUpstreamPort;

    [ObservableProperty] private bool _isUpstreamData = true;

    [ObservableProperty] private string? _customPortName;

    public IReadOnlyList<UpstreamNodeInfo> UpstreamNodes { get; }
    public ObservableCollection<string> UpstreamPorts { get; } = new();
    public bool HasUpstreamNodes => UpstreamNodes.Count > 0;

    /// <summary>请求关闭对话框，参数为 DialogResult 值。</summary>
    public event Action<bool?>? CloseRequested;

    private readonly Func<string, IReadOnlyList<string>>? _getUpstreamPorts;

    /// <summary>
    /// 初始化 InputPortEditViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 IsNullOrEmpty、FirstOrDefault、LoadUpstreamPorts、Contains；具体条件和先后次序见方法体。
    /// </summary>
    public InputPortEditViewModel(
        IReadOnlyList<UpstreamNodeInfo> upstreamNodes,
        Func<string, IReadOnlyList<string>>? getUpstreamPorts,
        string? currentPortName = null,
        string? currentUpstreamNodeId = null,
        string? currentUpstreamPortName = null)
    {
        UpstreamNodes = upstreamNodes;
        _getUpstreamPorts = getUpstreamPorts;
        _currentPortName = currentPortName ?? "";
        _isModifyMode = !string.IsNullOrEmpty(currentPortName);

        if (currentUpstreamNodeId != null)
        {
            var node = upstreamNodes.FirstOrDefault(n => n.NodeId == currentUpstreamNodeId);
            if (node != null)
            {
                _selectedUpstreamNode = node;
                LoadUpstreamPorts(node.NodeId);
                if (currentUpstreamPortName != null && UpstreamPorts.Contains(currentUpstreamPortName))
                    _selectedUpstreamPort = currentUpstreamPortName;
            }
        }
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnSelectedUpstreamNodeChanged。
    /// 正文中的关键协作调用包括 LoadUpstreamPorts；具体条件和先后次序见方法体。
    /// </summary>
    partial void OnSelectedUpstreamNodeChanged(UpstreamNodeInfo? oldValue, UpstreamNodeInfo? newValue)
    {
        LoadUpstreamPorts(newValue?.NodeId);
    }

    /// <summary>
    /// 执行加载操作；对应入口为 LoadUpstreamPorts。
    /// 正文中的关键协作调用包括 Clear、_getUpstreamPorts、Add；具体条件和先后次序见方法体。
    /// </summary>
    private void LoadUpstreamPorts(string? nodeId)
    {
        UpstreamPorts.Clear();
        if (nodeId != null && _getUpstreamPorts != null)
        {
            var ports = _getUpstreamPorts(nodeId);
            foreach (var port in ports)
                UpstreamPorts.Add(port);
        }
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Confirm。
    /// 正文中的关键协作调用包括 IsNullOrWhiteSpace；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand]
    private void Confirm()
    {
        if (IsUpstreamData)
        {
            PortName = CurrentPortName?.Trim();
            if (string.IsNullOrWhiteSpace(PortName)) return;
            if (SelectedUpstreamNode == null) return;
            if (SelectedUpstreamPort == null) return;
        }
        else
        {
            PortName = CustomPortName?.Trim();
            if (string.IsNullOrWhiteSpace(PortName)) return;
        }
        CloseRequested?.Invoke(true);
    }

    /// <summary>
    /// 请求取消当前操作；对应入口为 Cancel。
    /// </summary>
    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);
}