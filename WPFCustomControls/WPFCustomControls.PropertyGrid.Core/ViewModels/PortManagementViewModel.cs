using System.Collections;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WPFCustomControls.PropertyGrid.Composition;
using WPFCustomControls.PropertyGrid.Internal;
using WPFCustomControls.PropertyGrid.Models;

namespace WPFCustomControls.PropertyGrid.ViewModels;

/// <summary>Coordinates the input/output port editor exposed to the PropertyGrid View.</summary>
public partial class PortManagementViewModel : ObservableObject, IDisposable
{
    private readonly IPortManageable _portManager;
    private readonly IReadOnlyList<string> _availableProperties;
    private readonly IDataBindingProvider? _dataBindingProvider;
    private readonly PortBindingCoordinator? _bindings;
    private readonly IPortDialogService _dialogService;
    private readonly IPortConfigurable? _portConfigurable;
    private readonly List<PortRowSession> _sessions = [];
    private bool _isReloading;
    private bool _disposed;

    [ObservableProperty]
    private bool _isInputLocked = true;

    [ObservableProperty]
    private bool _isOutputLocked = true;

    [ObservableProperty]
    private ObservableCollection<PortRowViewModel> _inputPorts = [];

    [ObservableProperty]
    private ObservableCollection<PortRowViewModel> _outputPorts = [];

    public PortManagementViewModel(
        IPortManageable target,
        List<string> availableProperties,
        IPortDialogService dialogService)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(availableProperties);
        ArgumentNullException.ThrowIfNull(dialogService);

        _portManager = target;
        _availableProperties = availableProperties;
        _dialogService = dialogService;
        _dataBindingProvider = target as IDataBindingProvider;
        _bindings = _dataBindingProvider == null ? null : new PortBindingCoordinator(_dataBindingProvider);
        _portConfigurable = target as IPortConfigurable;

        Reload();
        if (_portConfigurable != null)
            _portConfigurable.ConfigChanged += OnExternalStateChanged;
        if (_dataBindingProvider != null)
            _dataBindingProvider.DataBindingsChanged += OnExternalStateChanged;
    }

    public event Action? RefreshRequired;

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (_portConfigurable != null)
            _portConfigurable.ConfigChanged -= OnExternalStateChanged;
        if (_dataBindingProvider != null)
            _dataBindingProvider.DataBindingsChanged -= OnExternalStateChanged;
        DisposeSessions();
    }

    private void OnExternalStateChanged()
    {
        if (_isReloading || _disposed)
            return;
        Dispatch(Reload);
    }

    private void Reload()
    {
        if (_isReloading || _disposed)
            return;
        _isReloading = true;
        try
        {
            DisposeSessions();
            InputPorts = LoadPorts(_portManager.CustomInputPorts, isInput: true);
            OutputPorts = LoadPorts(_portManager.CustomOutputPorts, isInput: false);
            RefreshRequired?.Invoke();
        }
        finally
        {
            _isReloading = false;
        }
    }

    private ObservableCollection<PortRowViewModel> LoadPorts(IEnumerable? models, bool isInput)
    {
        var rows = new ObservableCollection<PortRowViewModel>();
        if (models == null)
            return rows;

        var currentBindings = isInput ? _bindings?.Bindings : null;
        foreach (var model in models.Cast<object?>().Where(model => model != null))
        {
            var session = new PortRowSession(
                model!,
                isInput,
                isInput ? IsInputLocked : IsOutputLocked,
                _availableProperties,
                _portManager);
            _sessions.Add(session);
            rows.Add(session.Row);

            if (isInput && _bindings != null && currentBindings != null)
                ConfigureBindingActions(session, currentBindings);
            session.Row.DeleteRequested += () => DeletePort(session, rows);
        }
        return rows;
    }

    private void ConfigureBindingActions(
        PortRowSession session,
        IReadOnlyList<DataBindingInfo> currentBindings)
    {
        var existing = PortBindingCoordinator.Find(
            currentBindings,
            session.Id,
            session.BindingMatchName);
        ApplyBindingState(session.Row, existing);

        session.Row.ModifyBindingRequested += () => ModifyBinding(session);
        session.Row.BindRequested += () => CreateBinding(session);
        session.Row.UnbindRequested += () =>
        {
            if (_bindings!.Remove(session.Id, session.BindingMatchName))
                Reload();
        };
    }

    private void ModifyBinding(PortRowSession session)
    {
        if (_bindings == null || _bindings.UpstreamNodes.Count == 0)
            return;

        var existing = _bindings.Find(session.Id, session.BindingMatchName);
        var result = _dialogService.ShowModifyBindingDialog(CreateBindingRequest(session, existing));
        if (result == null)
            return;

        _bindings.Replace(existing, result, session.Name);
        Reload();
    }

    private void CreateBinding(PortRowSession session)
    {
        if (_bindings == null || _bindings.UpstreamNodes.Count == 0)
            return;

        var result = _dialogService.ShowModifyBindingDialog(CreateBindingRequest(session, null));
        if (result == null)
            return;

        _bindings.Create(result, session.Name);
        Reload();
    }

    private PortDialogRequest CreateBindingRequest(PortRowSession session, DataBindingInfo? existing) => new()
    {
        UpstreamNodes = _bindings!.UpstreamNodes,
        GetUpstreamPorts = _bindings.GetUpstreamPorts,
        CurrentPortName = session.Name,
        CurrentUpstreamNodeId = existing?.UpstreamNodeId,
        CurrentUpstreamPortName = existing?.SourcePortName
    };

    private void DeletePort(
        PortRowSession session,
        ObservableCollection<PortRowViewModel> rows)
    {
        if (session.Row.IsBound)
            _bindings?.Remove(session.Id, session.BindingMatchName);

        _portManager.RemoveCustomPort(session.Id);
        rows.Remove(session.Row);
        _sessions.Remove(session);
        session.Dispose();
    }

    private static void ApplyBindingState(PortRowViewModel row, DataBindingInfo? binding)
    {
        if (binding == null)
            return;
        row.IsBound = true;
        row.BindingStatusText = $"已绑定: {binding.UpstreamNodeName}.{binding.PropertyName}";
        row.BoundUpstreamNodeId = binding.UpstreamNodeId;
        row.BoundUpstreamProperty = binding.PropertyName;
    }

    [RelayCommand]
    private void AddInputPort()
    {
        var result = _dialogService.ShowAddPortDialog(new PortDialogRequest
        {
            Title = "添加输入端口",
            IsInputPort = true,
            UpstreamNodes = _bindings?.UpstreamNodes ?? [],
            GetUpstreamPorts = _bindings == null ? null : _bindings.GetUpstreamPorts
        });
        if (result?.PortName is not { Length: > 0 } portName)
            return;

        if (result.IsUpstreamData)
            _bindings?.Create(result, portName);
        else
            _portManager.AddCustomInputPort(portName);
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
        if (result?.PortName is not { Length: > 0 } portName)
            return;

        _portManager.AddCustomOutputPort(
            portName,
            result.IsToolProperty ? result.BoundPropertyName : null);
        Reload();
    }

    partial void OnIsInputLockedChanged(bool value)
    {
        foreach (var row in InputPorts)
            row.IsReadOnly = value;
    }

    partial void OnIsOutputLockedChanged(bool value)
    {
        foreach (var row in OutputPorts)
            row.IsReadOnly = value;
    }

    [RelayCommand]
    private void ToggleInputLock() => IsInputLocked = !IsInputLocked;

    [RelayCommand]
    private void ToggleOutputLock() => IsOutputLocked = !IsOutputLocked;

    private void DisposeSessions()
    {
        foreach (var session in _sessions)
            session.Dispose();
        _sessions.Clear();
    }

    private static void Dispatch(Action action)
    {
        if (UiDispatcher.Current.CheckAccess())
            action();
        else
            UiDispatcher.Current.Post(action);
    }
}
