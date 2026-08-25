using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WPFCustomControls.PropertyGrid.Models;

namespace WPFCustomControls.PropertyGrid.ViewModels;

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

    partial void OnSelectedUpstreamNodeChanged(UpstreamNodeInfo? oldValue, UpstreamNodeInfo? newValue)
    {
        LoadUpstreamPorts(newValue?.NodeId);
    }

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

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);
}
