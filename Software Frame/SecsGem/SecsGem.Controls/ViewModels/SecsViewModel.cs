// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：SecsViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：CommunityToolkit.Mvvm.ComponentModel、CommunityToolkit.Mvvm.Input、SecsGem.Controls.Models、SecsGem.Controls.Services、SecsGem.Runtime.Variable、System.Collections.ObjectModel。导入不代表所有类型都会在运行时加载。
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecsGem.Controls.Models;
using SecsGem.Controls.Services;
using SecsGem.Runtime.Variable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace SecsGem.Controls.ViewModels;

/// <summary>Toolkit 命令和页面绑定；所有流程转交 Workspace。</summary>
public partial class SecsViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    protected SecsWorkspace Workspace { get; }
    public SecsPageModel Model => Workspace.Model;
    public ObservableCollection<LogRow> Messages => Model.Messages;
    /// <summary>
    /// 初始化 SecsViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 AddHandler；具体条件和先后次序见方法体。
    /// </summary>
    public SecsViewModel(SecsWorkspace workspace)
    {
        Workspace = workspace;
        PropertyChangedEventManager.AddHandler(Model, OnModelChanged, string.Empty);
    }
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnModelChanged。
    /// 正文中的关键协作调用包括 OnPropertyChanged、nameof、RefreshCommands；具体条件和先后次序见方法体。
    /// </summary>
    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(e.PropertyName);
        // 选中行变化时回填输入框：新增与修改共用同一套框，不回填的话
        // 改一个 VID 列表得整段重打。ID 也带出来，好让"保存修改"能发现你把 ID 改了。
        if (e.PropertyName == nameof(Model.SelectedReport))
        {
            Model.NewReportId = Model.SelectedReport?.ID ?? "";
            Model.NewReportName = Model.SelectedReport?.Name ?? "";
            Model.NewReportVids = Model.SelectedReport?.VID_Collection ?? "";
        }
        if (e.PropertyName == nameof(Model.SelectedEvent))
        {
            Model.NewEventId = Model.SelectedEvent?.ID ?? "";
            Model.NewEventName = Model.SelectedEvent?.Name ?? "";
            Model.NewEventReports = Model.SelectedEvent?.ReportID_Collection ?? "";
            Model.NewEventEnable = Model.SelectedEvent?.Enable ?? true;
        }
        if (e.PropertyName is nameof(Model.IsConnected) or nameof(Model.IsClosed) or
            nameof(Model.Address) or nameof(Model.PortText) or nameof(Model.DeviceIdText) or
            nameof(Model.ConnectionModeIndex) or nameof(Model.Sml) or nameof(Model.SelectedVariable) or
            nameof(Model.NewVariableId) or nameof(Model.NewVariableName) or
            nameof(Model.NewReportId) or nameof(Model.NewReportName) or
            nameof(Model.NewEventId) or nameof(Model.NewEventName) or
            nameof(Model.SelectedEvent) or nameof(Model.SelectedReport) or nameof(Model.SelectedAlarm))
            RefreshCommands();
    }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CanUseWorkspace。
    /// </summary>
    private bool CanUseWorkspace() => !_disposed && !Model.IsClosed;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CanConnect。
    /// <para>
    /// DeviceID 只在被动模式（ConnectionModeIndex==1）下参与通信；主动模式下填什么都不该
    /// 影响连接，所以这一路不校验它 —— 原实现在两种模式下都要求 0-32767，
    /// 填 40000 时连接按钮直接变灰，连试都试不了。
    /// </para>
    /// </summary>
    private bool CanConnect() => CanUseWorkspace() && !Model.IsConnected &&
        !string.IsNullOrWhiteSpace(Model.Address) && Model.ConnectionModeIndex is 0 or 1 &&
        int.TryParse(Model.PortText, out var port) && port is >= 1 and <= 65535 &&
        (Model.ConnectionModeIndex == 0 ||
            (int.TryParse(Model.DeviceIdText, out var id) && id is >= 0 and <= 32767));
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CanUseConnection。
    /// 正文中的关键协作调用包括 CanUseWorkspace；具体条件和先后次序见方法体。
    /// </summary>
    private bool CanUseConnection() => CanUseWorkspace() && Model.IsConnected;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CanSendSml。
    /// 正文中的关键协作调用包括 CanUseConnection、IsNullOrWhiteSpace；具体条件和先后次序见方法体。
    /// </summary>
    private bool CanSendSml() => CanUseConnection() && !string.IsNullOrWhiteSpace(Model.Sml);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CanSetVariable。
    /// 正文中的关键协作调用包括 CanUseWorkspace；具体条件和先后次序见方法体。
    /// </summary>
    private bool CanSetVariable() => CanUseWorkspace() && Model.SelectedVariable != null;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CanAddVariable。
    /// 正文中的关键协作调用包括 CanUseWorkspace、IsNullOrWhiteSpace；具体条件和先后次序见方法体。
    /// </summary>
    private bool CanAddVariable() => CanUseWorkspace() &&
        !string.IsNullOrWhiteSpace(Model.NewVariableId) && !string.IsNullOrWhiteSpace(Model.NewVariableName);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CanReportEvent。
    /// 正文中的关键协作调用包括 CanUseConnection；具体条件和先后次序见方法体。
    /// </summary>
    private bool CanReportEvent() => CanUseConnection() && Model.SelectedEvent != null;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CanReportAlarm。
    /// 正文中的关键协作调用包括 CanUseConnection；具体条件和先后次序见方法体。
    /// </summary>
    private bool CanReportAlarm() => CanUseConnection() && Model.SelectedAlarm != null;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 RefreshCommands。
    /// 正文中的关键协作调用包括 NotifyCanExecuteChanged；具体条件和先后次序见方法体。
    /// </summary>
    private void RefreshCommands()
    {
        ConnectCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
        EstablishCommand.NotifyCanExecuteChanged();
        OnlineLocalCommand.NotifyCanExecuteChanged();
        OnlineRemoteCommand.NotifyCanExecuteChanged();
        OfflineCommand.NotifyCanExecuteChanged();
        LoadConfigCommand.NotifyCanExecuteChanged();
        LoadSampleCommand.NotifyCanExecuteChanged();
        SendSmlCommand.NotifyCanExecuteChanged();
        SetVariableCommand.NotifyCanExecuteChanged();
        AddVariableCommand.NotifyCanExecuteChanged();
        DeleteVariableCommand.NotifyCanExecuteChanged();
        AddReportCommand.NotifyCanExecuteChanged();
        DeleteReportCommand.NotifyCanExecuteChanged();
        SetReportCommand.NotifyCanExecuteChanged();
        AddEventCommand.NotifyCanExecuteChanged();
        DeleteEventCommand.NotifyCanExecuteChanged();
        SetEventCommand.NotifyCanExecuteChanged();
        RefreshCatalogNowCommand.NotifyCanExecuteChanged();
        ReportEventCommand.NotifyCanExecuteChanged();
        RaiseAlarmCommand.NotifyCanExecuteChanged();
        ResetAlarmCommand.NotifyCanExecuteChanged();
        PreviewTerminalCommand.NotifyCanExecuteChanged();
        ClearLogCommand.NotifyCanExecuteChanged();
    }
    public int ConnectionModeIndex { get => Model.ConnectionModeIndex; set => Model.ConnectionModeIndex = value; }
    public string Address { get => Model.Address; set => Model.Address = value; }
    public string PortText { get => Model.PortText; set => Model.PortText = value; }
    public string DeviceIdText { get => Model.DeviceIdText; set => Model.DeviceIdText = value; }
    public string Sml { get => Model.Sml; set => Model.Sml = value; }
    public bool WaitForReply { get => Model.WaitForReply; set => Model.WaitForReply = value; }
    public string VariableValue { get => Model.VariableValue; set => Model.VariableValue = value; }
    public string NewVariableId { get => Model.NewVariableId; set => Model.NewVariableId = value; }
    public string NewVariableName { get => Model.NewVariableName; set => Model.NewVariableName = value; }
    public string NewReportId { get => Model.NewReportId; set => Model.NewReportId = value; }
    public string NewReportName { get => Model.NewReportName; set => Model.NewReportName = value; }
    public string NewReportVids { get => Model.NewReportVids; set => Model.NewReportVids = value; }
    public string NewEventId { get => Model.NewEventId; set => Model.NewEventId = value; }
    public string NewEventName { get => Model.NewEventName; set => Model.NewEventName = value; }
    public string NewEventReports { get => Model.NewEventReports; set => Model.NewEventReports = value; }
    public string Hcack { get => Model.Hcack; set => Model.Hcack = value; }
    public string ConnectionStatus { get => Model.ConnectionStatus; set => Model.ConnectionStatus = value; }
    public string ModeStatus { get => Model.ModeStatus; set => Model.ModeStatus = value; }
    public string DeviceStatus { get => Model.DeviceStatus; set => Model.DeviceStatus = value; }
    public List<VariableRow> Variables { get => Model.Variables; set => Model.Variables = value; }
    public IEnumerable<CEIDInfo> Events { get => Model.Events; set => Model.Events = value; }
    public IEnumerable<ReportInfo> Reports { get => Model.Reports; set => Model.Reports = value; }
    public IEnumerable<AlarmInfo> Alarms { get => Model.Alarms; set => Model.Alarms = value; }
    public VariableRow? SelectedVariable { get => Model.SelectedVariable; set => Model.SelectedVariable = value; }
    public CEIDInfo? SelectedEvent { get => Model.SelectedEvent; set => Model.SelectedEvent = value; }
    public ReportInfo? SelectedReport { get => Model.SelectedReport; set => Model.SelectedReport = value; }
    public AlarmInfo? SelectedAlarm { get => Model.SelectedAlarm; set => Model.SelectedAlarm = value; }
    /// <summary>
    /// 发起连接操作；对应入口为 ConnectAsync。
    /// 正文中的关键协作调用包括 nameof、CanUseWorkspace、ConnectAsync；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanConnect))]
    private Task ConnectAsync() => CanUseWorkspace() && !Model.IsConnected ? Workspace.ConnectAsync() : Task.CompletedTask;
    /// <summary>
    /// 执行断开连接操作；对应入口为 DisconnectAsync。
    /// 正文中的关键协作调用包括 nameof、CanUseConnection、DisconnectAsync；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUseConnection))]
    private Task DisconnectAsync() => CanUseConnection() ? Workspace.DisconnectAsync() : Task.CompletedTask;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 EstablishAsync。
    /// 正文中的关键协作调用包括 nameof、CanUseConnection、EstablishAsync；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUseConnection))]
    private Task EstablishAsync() => CanUseConnection() ? Workspace.EstablishAsync() : Task.CompletedTask;
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnlineLocalAsync。
    /// 正文中的关键协作调用包括 nameof、CanUseConnection、OnlineLocalAsync；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUseConnection))]
    private Task OnlineLocalAsync() => CanUseConnection() ? Workspace.OnlineLocalAsync() : Task.CompletedTask;
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnlineRemoteAsync。
    /// 正文中的关键协作调用包括 nameof、CanUseConnection、OnlineRemoteAsync；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUseConnection))]
    private Task OnlineRemoteAsync() => CanUseConnection() ? Workspace.OnlineRemoteAsync() : Task.CompletedTask;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 OfflineAsync。
    /// 正文中的关键协作调用包括 nameof、CanUseConnection、OfflineAsync；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUseConnection))]
    private Task OfflineAsync() => CanUseConnection() ? Workspace.OfflineAsync() : Task.CompletedTask;
    /// <summary>
    /// 执行加载操作；对应入口为 LoadConfig。
    /// 正文中的关键协作调用包括 nameof、CanUseWorkspace、LoadConfig；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUseWorkspace))]
    private void LoadConfig() { if (CanUseWorkspace()) Workspace.LoadConfig(); }
    /// <summary>
    /// 执行加载操作；对应入口为 LoadSample。
    /// 正文中的关键协作调用包括 nameof、CanUseWorkspace、LoadSample；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUseWorkspace))]
    private void LoadSample() { if (CanUseWorkspace()) Workspace.LoadSample(); }
    /// <summary>
    /// 执行发送操作；对应入口为 SendSmlAsync。
    /// 正文中的关键协作调用包括 nameof、CanSendSml、SendSmlAsync；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSendSml))]
    private Task SendSmlAsync() => CanSendSml() ? Workspace.SendSmlAsync() : Task.CompletedTask;
    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetVariable。
    /// 正文中的关键协作调用包括 nameof、CanSetVariable、SetVariable；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSetVariable))]
    private void SetVariable() { if (CanSetVariable()) Workspace.SetVariable(); }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 RefreshCatalogNow。
    /// 正文中的关键协作调用包括 nameof、CanUseWorkspace、RefreshCatalogNow；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUseWorkspace))]
    private void RefreshCatalogNow() { if (CanUseWorkspace()) Workspace.RefreshCatalogNow(); }
    /// <summary>
    /// 执行新增操作；对应入口为 AddVariable。
    /// 正文中的关键协作调用包括 nameof、CanAddVariable、AddVariable；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddVariable))]
    private void AddVariable() { if (CanAddVariable()) Workspace.AddVariable(); }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CanDeleteVariable。
    /// </summary>
    private bool CanDeleteVariable() => CanUseWorkspace() && Model.SelectedVariable != null;
    /// <summary>
    /// 执行删除操作；对应入口为 DeleteVariable。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDeleteVariable))]
    private void DeleteVariable() { if (CanDeleteVariable()) Workspace.DeleteVariable(); }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CanAddReport。
    /// 正文中的关键协作调用包括 CanUseWorkspace、IsNullOrWhiteSpace；具体条件和先后次序见方法体。
    /// </summary>
    private bool CanAddReport() => CanUseWorkspace() &&
        !string.IsNullOrWhiteSpace(Model.NewReportId) && !string.IsNullOrWhiteSpace(Model.NewReportName);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CanAddEvent。
    /// 正文中的关键协作调用包括 CanUseWorkspace、IsNullOrWhiteSpace；具体条件和先后次序见方法体。
    /// </summary>
    private bool CanAddEvent() => CanUseWorkspace() &&
        !string.IsNullOrWhiteSpace(Model.NewEventId) && !string.IsNullOrWhiteSpace(Model.NewEventName);
    /// <summary>
    /// 执行新增操作；对应入口为 AddReport。
    /// 正文中的关键协作调用包括 nameof、CanAddReport、AddReport；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddReport))]
    private void AddReport() { if (CanAddReport()) Workspace.AddReport(); }
    /// <summary>
    /// 执行新增操作；对应入口为 AddEvent。
    /// 正文中的关键协作调用包括 nameof、CanAddEvent、AddEvent；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddEvent))]
    private void AddEvent() { if (CanAddEvent()) Workspace.AddEvent(); }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CanDeleteReport。
    /// </summary>
    private bool CanDeleteReport() => CanUseWorkspace() && Model.SelectedReport != null;
    /// <summary>
    /// 执行删除操作；对应入口为 DeleteReport。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDeleteReport))]
    private void DeleteReport() { if (CanDeleteReport()) Workspace.DeleteReport(); }
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 CanSetReport。
    /// 正文中的关键协作调用包括 CanUseWorkspace；具体条件和先后次序见方法体。
    /// </summary>
    private bool CanSetReport() => CanUseWorkspace() && Model.SelectedReport != null;
    /// <summary>
    /// 执行修改操作；对应入口为 SetReport。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSetReport))]
    private void SetReport() { if (CanSetReport()) Workspace.SetReport(); }
    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 CanSetEvent。
    /// </summary>
    private bool CanSetEvent() => CanUseWorkspace() && Model.SelectedEvent != null;
    /// <summary>
    /// 执行修改操作；对应入口为 SetEvent。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSetEvent))]
    private void SetEvent() { if (CanSetEvent()) Workspace.SetEvent(); }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CanDeleteEvent。
    /// </summary>
    private bool CanDeleteEvent() => CanUseWorkspace() && Model.SelectedEvent != null;
    /// <summary>
    /// 执行删除操作；对应入口为 DeleteEvent。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDeleteEvent))]
    private void DeleteEvent() { if (CanDeleteEvent()) Workspace.DeleteEvent(); }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ReportEventAsync。
    /// 正文中的关键协作调用包括 nameof、CanReportEvent、ReportEventAsync；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanReportEvent))]
    private Task ReportEventAsync() => CanReportEvent() ? Workspace.ReportEventAsync() : Task.CompletedTask;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 RaiseAlarmAsync。
    /// 正文中的关键协作调用包括 nameof、CanReportAlarm、RaiseAlarmAsync；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanReportAlarm))]
    private Task RaiseAlarmAsync() => CanReportAlarm() ? Workspace.RaiseAlarmAsync() : Task.CompletedTask;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ResetAlarmAsync。
    /// 正文中的关键协作调用包括 nameof、CanReportAlarm、ResetAlarmAsync；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanReportAlarm))]
    private Task ResetAlarmAsync() => CanReportAlarm() ? Workspace.ResetAlarmAsync() : Task.CompletedTask;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 PreviewTerminal。
    /// 正文中的关键协作调用包括 nameof、CanUseWorkspace、PreviewTerminal；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUseWorkspace))]
    private void PreviewTerminal() { if (CanUseWorkspace()) Workspace.PreviewTerminal(); }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ClearLog。
    /// 正文中的关键协作调用包括 nameof、CanUseWorkspace、ClearLog；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUseWorkspace))]
    private void ClearLog() { if (CanUseWorkspace()) Workspace.ClearLog(); }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 RefreshState。
    /// 正文中的关键协作调用包括 CanUseWorkspace、RefreshState；具体条件和先后次序见方法体。
    /// </summary>
    public void RefreshState() { if (CanUseWorkspace()) Workspace.RefreshState(); }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Subscribe。
    /// 正文中的关键协作调用包括 ThrowIf、Subscribe；具体条件和先后次序见方法体。
    /// </summary>
    public IDisposable Subscribe()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Workspace.Subscribe();
    }
    /// <summary>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 RemoveHandler、RefreshCommands；具体条件和先后次序见方法体。
    /// </summary>
    public virtual void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        PropertyChangedEventManager.RemoveHandler(Model, OnModelChanged, string.Empty);
        RefreshCommands();
    }
}
