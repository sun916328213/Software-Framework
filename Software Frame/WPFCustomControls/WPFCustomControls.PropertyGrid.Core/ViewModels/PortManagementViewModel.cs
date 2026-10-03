// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：PortManagementViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections、System.Collections.ObjectModel、CommunityToolkit.Mvvm.ComponentModel、CommunityToolkit.Mvvm.Input、WPFCustomControls.PropertyGrid.Core.Composition、WPFCustomControls.PropertyGrid.Core.Internal。导入不代表所有类型都会在运行时加载。
using System.Collections;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WPFCustomControls.PropertyGrid.Core.Composition;
using WPFCustomControls.PropertyGrid.Core.Internal;
using WPFCustomControls.PropertyGrid.Core.Models;

namespace WPFCustomControls.PropertyGrid.Core.ViewModels;

/// <summary>Coordinates the input/output port editor exposed to the PropertyGrid View.</summary>
/// <remarks>
/// PortManagementViewModel 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
/// 继承或实现的契约：ObservableObject、IDisposable。具体约束按这些契约及各成员实现解释。
/// </remarks>
public partial class PortManagementViewModel : ObservableObject, IDisposable
{
    private readonly IPortManageable _portManager;
    private readonly IReadOnlyList<string> _availableProperties;
    private readonly IDataBindingProvider? _dataBindingProvider;
    private readonly PortBindingCoordinator? _bindings;
    private readonly IPortDialogService _dialogService;
    private readonly IUiDispatcher _dispatcher;
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

    /// <summary>
    /// 初始化 PortManagementViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public PortManagementViewModel(
        IPortManageable target,
        List<string> availableProperties,
        IPortDialogService dialogService)
        : this(target, availableProperties, dialogService, NullUiDispatcher.Instance)
    {
    }

    /// <summary>
    /// 初始化 PortManagementViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 ThrowIfNull、Reload；具体条件和先后次序见方法体。
    /// </summary>
    internal PortManagementViewModel(
        IPortManageable target,
        List<string> availableProperties,
        IPortDialogService dialogService,
        IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(availableProperties);
        ArgumentNullException.ThrowIfNull(dialogService);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _portManager = target;
        _availableProperties = availableProperties;
        _dialogService = dialogService;
        _dispatcher = dispatcher;
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

    /// <summary>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 DisposeSessions；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnExternalStateChanged。
    /// 正文中的关键协作调用包括 Dispatch；具体条件和先后次序见方法体。
    /// </summary>
    private void OnExternalStateChanged()
    {
        if (_isReloading || _disposed)
            return;
        Dispatch(Reload);
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Reload。
    /// 正文中的关键协作调用包括 DisposeSessions、LoadPorts；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 执行加载操作；对应入口为 LoadPorts。
    /// 正文中的关键协作调用包括 Where、Cast、Add、ConfigureBindingActions、DeletePort；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 配置当前对象或组合入口；对应入口为 ConfigureBindingActions。
    /// 正文中的关键协作调用包括 Find、ApplyBindingState、ModifyBinding、CreateBinding、Remove；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ModifyBinding。
    /// 正文中的关键协作调用包括 Find、ShowModifyBindingDialog、CreateBindingRequest、Replace、Reload；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateBinding。
    /// 正文中的关键协作调用包括 ShowModifyBindingDialog、CreateBindingRequest、Create、Reload；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateBindingRequest。
    /// </summary>
    private PortDialogRequest CreateBindingRequest(PortRowSession session, DataBindingInfo? existing) => new()
    {
        UpstreamNodes = _bindings!.UpstreamNodes,
        GetUpstreamPorts = _bindings.GetUpstreamPorts,
        CurrentPortName = session.Name,
        CurrentUpstreamNodeId = existing?.UpstreamNodeId,
        CurrentUpstreamPortName = existing?.SourcePortName
    };

    /// <summary>
    /// 执行删除操作；对应入口为 DeletePort。
    /// 正文中的关键协作调用包括 RemoveCustomPort、Remove、Dispose；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ApplyBindingState。
    /// </summary>
    private static void ApplyBindingState(PortRowViewModel row, DataBindingInfo? binding)
    {
        if (binding == null)
            return;
        row.IsBound = true;
        row.BindingStatusText = $"已绑定: {binding.UpstreamNodeName}.{binding.PropertyName}";
        row.BoundUpstreamNodeId = binding.UpstreamNodeId;
        row.BoundUpstreamProperty = binding.PropertyName;
    }

    /// <summary>
    /// 执行添加操作；对应入口为 AddInputPort。
    /// 正文中的关键协作调用包括 ShowAddPortDialog、AddCustomInputPort、Reload；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 执行添加操作；对应入口为 AddOutputPort。
    /// 正文中的关键协作调用包括 ShowAddPortDialog、AddCustomOutputPort、Reload；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnIsInputLockedChanged。
    /// </summary>
    partial void OnIsInputLockedChanged(bool value)
    {
        foreach (var row in InputPorts)
            row.IsReadOnly = value;
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnIsOutputLockedChanged。
    /// </summary>
    partial void OnIsOutputLockedChanged(bool value)
    {
        foreach (var row in OutputPorts)
            row.IsReadOnly = value;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ToggleInputLock。
    /// </summary>
    [RelayCommand]
    private void ToggleInputLock() => IsInputLocked = !IsInputLocked;

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ToggleOutputLock。
    /// </summary>
    [RelayCommand]
    private void ToggleOutputLock() => IsOutputLocked = !IsOutputLocked;

    /// <summary>
    /// 执行资源释放步骤；对应入口为 DisposeSessions。
    /// 正文中的关键协作调用包括 Dispose、Clear；具体条件和先后次序见方法体。
    /// </summary>
    private void DisposeSessions()
    {
        foreach (var session in _sessions)
            session.Dispose();
        _sessions.Clear();
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Dispatch。
    /// 正文中的关键协作调用包括 CheckAccess、action、Post；具体条件和先后次序见方法体。
    /// </summary>
    private void Dispatch(Action action)
    {
        if (_dispatcher.CheckAccess())
            action();
        else
            _dispatcher.Post(action);
    }
}
