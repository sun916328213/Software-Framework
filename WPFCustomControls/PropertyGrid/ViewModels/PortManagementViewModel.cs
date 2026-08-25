using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using WPFCustomControls.PropertyGrid.Models;

namespace WPFCustomControls.PropertyGrid.ViewModels;

/// <summary>自定义端口管理 ViewModel，封装输入/输出端口列表和增删命令。</summary>
public partial class PortManagementViewModel : ObservableObject, IDisposable
{
    private readonly IPortManageable _portManager;
    private readonly List<string> _availableProperties;
    private readonly IDataBindingProvider? _dataBindingProvider;
    private readonly IPortDialogService _dialogService;
    private readonly IPortConfigurable? _portConfigurable;
    private bool _isReloading;
    private bool _disposed;

    /// <summary>输入端口区域锁定（锁定后不可编辑）</summary>
    [ObservableProperty] private bool _isInputLocked = true;

    /// <summary>输出端口区域锁定（锁定后不可编辑）</summary>
    [ObservableProperty] private bool _isOutputLocked = true;

    private ObservableCollection<PortRowViewModel> _inputPorts = new();
    private ObservableCollection<PortRowViewModel> _outputPorts = new();

    public ObservableCollection<PortRowViewModel> InputPorts
    {
        get => _inputPorts;
        set => SetProperty(ref _inputPorts, value);
    }

    public ObservableCollection<PortRowViewModel> OutputPorts
    {
        get => _outputPorts;
        set => SetProperty(ref _outputPorts, value);
    }

    public PortManagementViewModel(IPortManageable target, List<string> availableProperties, IPortDialogService dialogService)
    {
        _portManager = target;
        _availableProperties = availableProperties;
        _dialogService = dialogService;
        _dataBindingProvider = target as IDataBindingProvider;
        _portConfigurable = target as IPortConfigurable;

        Reload();

        if (_portConfigurable != null)
            _portConfigurable.ConfigChanged += OnTargetPortConfigChanged;

        // 监听连线变更，自动刷新绑定状态
        if (_dataBindingProvider != null)
            _dataBindingProvider.DataBindingsChanged += OnDataBindingsChanged;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_portConfigurable != null)
            _portConfigurable.ConfigChanged -= OnTargetPortConfigChanged;
        if (_dataBindingProvider != null)
            _dataBindingProvider.DataBindingsChanged -= OnDataBindingsChanged;
    }

    private void OnDataBindingsChanged()
    {
        if (_isReloading) return;
        if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == false)
            System.Windows.Application.Current.Dispatcher.Invoke(Reload);
        else
            Reload();
    }

    private void OnTargetPortConfigChanged()
    {
        if (_isReloading) return;
        if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == false)
            System.Windows.Application.Current.Dispatcher.Invoke(Reload);
        else
            Reload();
    }

    public event Action? RefreshRequired;

    private void Reload()
    {
        if (_isReloading) return;
        _isReloading = true;

        var newInputs = new ObservableCollection<PortRowViewModel>();
        var newOutputs = new ObservableCollection<PortRowViewModel>();
        LoadPorts(_portManager.CustomInputPorts, newInputs, isInput: true);
        LoadPorts(_portManager.CustomOutputPorts, newOutputs, isInput: false);

        InputPorts = newInputs;
        OutputPorts = newOutputs;
        RefreshRequired?.Invoke();
        _isReloading = false;
    }

    private void LoadPorts(System.Collections.IEnumerable? ports, ObservableCollection<PortRowViewModel> list, bool isInput)
    {
        if (ports == null) return;

        // 预加载绑定信息（仅输入端口需要）
        List<DataBindingInfo>? existingBindings = null;
        if (isInput && _dataBindingProvider != null)
            existingBindings = _dataBindingProvider.GetDataBindings().ToList();

        int index = 0;
        foreach (var port in ports)
        {
            if (port == null) continue;

            var portType = port.GetType();

            var idProp = portType.GetProperty("Id");
            var nameProp = portType.GetProperty("Name");
            var propertyNameProp = portType.GetProperty("PropertyName");

            var id = idProp?.GetValue(port) as string ?? "";
            var name = nameProp?.GetValue(port) as string ?? "";
            var boundProp = propertyNameProp?.GetValue(port) as string;
            var value = portType.GetProperty("Value")?.GetValue(port);

            var vm = new PortRowViewModel(id, name, isInput, boundProp);
            vm.Value = value;
            vm.IsReadOnly = isInput ? IsInputLocked : IsOutputLocked;
            vm.AvailableProperties.AddRange(_availableProperties);
            vm.NotifyPropertiesReady();

            // ── 输入端口：设置绑定状态 ──
            if (isInput && existingBindings != null)
            {
                var existingBinding = FindBinding(existingBindings, id, boundProp ?? name);

                if (existingBinding != null)
                {
                    vm.IsBound = true;
                    vm.BindingStatusText = $"已绑定: {existingBinding.UpstreamNodeName}.{existingBinding.PropertyName}";
                    vm.BoundUpstreamNodeId = existingBinding.UpstreamNodeId;
                    vm.BoundUpstreamProperty = existingBinding.PropertyName;
                }

                // 修改绑定：通过对话框服务弹窗
                vm.ModifyBindingRequested += () =>
                {
                    if (_dataBindingProvider == null) return;
                    var upstreamNodes = _dataBindingProvider.GetUpstreamFlowNodes();
                    if (upstreamNodes.Count == 0) return;

                    var binding = FindBinding(id, boundProp ?? name);

                    var result = _dialogService.ShowModifyBindingDialog(new PortDialogRequest
                    {
                        UpstreamNodes = upstreamNodes,
                        GetUpstreamPorts = nodeId => _dataBindingProvider.GetUpstreamOutputPorts(nodeId),
                        CurrentPortName = name,
                        CurrentUpstreamNodeId = binding?.UpstreamNodeId,
                        CurrentUpstreamPortName = binding?.SourcePortName
                    });

                    if (result is { IsUpstreamData: true, UpstreamNodeId: not null })
                    {
                        // 先解绑旧的
                        if (binding != null)
                            _dataBindingProvider.RemoveDataBinding(binding.BindingId);
                        // 创建新绑定（目标端口保持当前端口名）
                        _dataBindingProvider.CreateDataBinding(
                            result.UpstreamNodeId, result.UpstreamPortName!, name);
                        Reload();
                    }
                };

                // 绑定请求（未绑定端口发起绑定）
                vm.BindRequested += () =>
                {
                    if (_dataBindingProvider == null) return;
                    var upstreamNodes = _dataBindingProvider.GetUpstreamFlowNodes();
                    if (upstreamNodes.Count == 0) return;

                    var result = _dialogService.ShowModifyBindingDialog(new PortDialogRequest
                    {
                        UpstreamNodes = upstreamNodes,
                        GetUpstreamPorts = nodeId => _dataBindingProvider.GetUpstreamOutputPorts(nodeId),
                        CurrentPortName = name  // 传递当前端口名，对话框中只读显示
                    });

                    if (result is { IsUpstreamData: true, UpstreamNodeId: not null })
                    {
                        _dataBindingProvider.CreateDataBinding(
                            result.UpstreamNodeId, result.UpstreamPortName!, name);
                        Reload();
                    }
                };

                // 解除绑定请求
                vm.UnbindRequested += () =>
                {
                    if (_dataBindingProvider == null) return;
                    var binding = FindBinding(id, boundProp ?? name);
                    if (binding != null)
                    {
                        _dataBindingProvider.RemoveDataBinding(binding.BindingId);
                        Reload();
                    }
                };
            }

            // 监听端口模型的 PropertyChanged 事件
            if (port is ObservableObject observablePort)
            {
                observablePort.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == "Value")
                    {
                        var latestValue = portType.GetProperty("Value")?.GetValue(port);
                        vm.Value = latestValue;
                    }
                };
            }

            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PortRowViewModel.Name))
                {
                    nameProp?.SetMethod?.Invoke(port, new[] { vm.Name });
                    _portManager.InvalidatePorts();
                }
                else if (e.PropertyName == nameof(PortRowViewModel.BoundPropertyName))
                {
                    propertyNameProp?.SetMethod?.Invoke(port, new[] { vm.BoundPropertyName });
                    // 绑定属性变更时，同步更新端口显示名称
                    nameProp?.SetMethod?.Invoke(port, new[] { vm.BoundPropertyName ?? "" });
                    _portManager.InvalidatePorts();
                }
                else if (e.PropertyName == nameof(PortRowViewModel.Value))
                {
                    portType.GetProperty("Value")?.SetMethod?.Invoke(port, new[] { vm.Value });
                    _portManager.InvalidatePorts();
                }
            };

            vm.DeleteRequested += () =>
            {
                // 如果有绑定，先删除关联的连线
                if (vm.IsBound && _dataBindingProvider != null)
                {
                    var binding = FindBinding(id, boundProp ?? name);
                    if (binding != null)
                        _dataBindingProvider.RemoveDataBinding(binding.BindingId);
                }
                _portManager.RemoveCustomPort(id);
                list.Remove(vm);
            };
            list.Add(vm);
            index++;
        }
    }

    /// <summary>
    /// 按端口 Id 或属性名查找数据绑定记录（输入端口既可通过 Id 又可通过属性名匹配）。
    /// </summary>
    private DataBindingInfo? FindBinding(string id, string matchProp)
    {
        if (_dataBindingProvider == null) return null;
        return FindBinding(_dataBindingProvider.GetDataBindings().ToList(), id, matchProp);
    }

    private static DataBindingInfo? FindBinding(List<DataBindingInfo> bindings, string id, string matchProp)
        => bindings.FirstOrDefault(b => b.TargetPortId == id)
           ?? bindings.FirstOrDefault(b => b.PropertyName == matchProp);

    [RelayCommand]
    private void AddInputPort()
    {
        // 获取上游节点列表（用于"绑定上游数据"选项）
        IReadOnlyList<UpstreamNodeInfo> upstreamNodes = Array.Empty<UpstreamNodeInfo>();
        Func<string, IReadOnlyList<string>>? getUpstreamPorts = null;
        if (_dataBindingProvider != null)
        {
            upstreamNodes = _dataBindingProvider.GetUpstreamFlowNodes();
            getUpstreamPorts = nodeId => _dataBindingProvider.GetUpstreamOutputPorts(nodeId);
        }

        var result = _dialogService.ShowAddPortDialog(new PortDialogRequest
        {
            Title = "添加输入端口",
            IsInputPort = true,
            UpstreamNodes = upstreamNodes,
            GetUpstreamPorts = getUpstreamPorts
        });
        if (result == null) return;

        if (result.IsUpstreamData && result.UpstreamNodeId != null)
        {
            // 绑定上游数据：按用户命名创建端口并连接上游输出端口
            _dataBindingProvider?.CreateDataBinding(
                result.UpstreamNodeId, result.UpstreamPortName!, result.PortName!);
        }
        else
        {
            _portManager.AddCustomInputPort(result.PortName!);
        }
        Reload();
    }

    [RelayCommand]
    private void AddOutputPort()
    {
        var result = _dialogService.ShowAddPortDialog(new PortDialogRequest
        {
            Title = "添加输出端口",
            IsInputPort = false,
            AvailableProperties = _availableProperties
        });
        if (result == null) return;

        var boundProp = result.IsToolProperty ? result.BoundPropertyName : null;
        _portManager.AddCustomOutputPort(result.PortName!, boundProp);
        Reload();
    }

    // ── 区域锁定 ──────────────────────────────────────

    partial void OnIsInputLockedChanged(bool oldValue, bool newValue)
    {
        foreach (var vm in InputPorts)
            vm.IsReadOnly = newValue;
    }

    partial void OnIsOutputLockedChanged(bool oldValue, bool newValue)
    {
        foreach (var vm in OutputPorts)
            vm.IsReadOnly = newValue;
    }

    [RelayCommand]
    private void ToggleInputLock() => IsInputLocked = !IsInputLocked;

    [RelayCommand]
    private void ToggleOutputLock() => IsOutputLocked = !IsOutputLocked;
}