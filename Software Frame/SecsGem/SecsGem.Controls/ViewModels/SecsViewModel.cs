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
    public ObservableCollection<string> Messages => Model.Messages;
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
        if (e.PropertyName is nameof(Model.IsConnected) or nameof(Model.IsClosed) or
            nameof(Model.Address) or nameof(Model.PortText) or nameof(Model.DeviceIdText) or
            nameof(Model.ConnectionModeIndex) or nameof(Model.Sml) or nameof(Model.SelectedVariable) or
            nameof(Model.SelectedEvent) or nameof(Model.SelectedAlarm))
            RefreshCommands();
    }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CanUseWorkspace。
    /// </summary>
    private bool CanUseWorkspace() => !_disposed && !Model.IsClosed;
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 CanConnect。
    /// 正文中的关键协作调用包括 CanUseWorkspace、IsNullOrWhiteSpace、TryParse；具体条件和先后次序见方法体。
    /// </summary>
    private bool CanConnect() => CanUseWorkspace() && !Model.IsConnected &&
        !string.IsNullOrWhiteSpace(Model.Address) && Model.ConnectionModeIndex is 0 or 1 &&
        int.TryParse(Model.PortText, out var port) && port is >= 1 and <= 65535 &&
        int.TryParse(Model.DeviceIdText, out var id) && id is >= 0 and <= 32767;
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
        OnlineCommand.NotifyCanExecuteChanged();
        OfflineCommand.NotifyCanExecuteChanged();
        LoadConfigCommand.NotifyCanExecuteChanged();
        LoadSampleCommand.NotifyCanExecuteChanged();
        SendSmlCommand.NotifyCanExecuteChanged();
        SetVariableCommand.NotifyCanExecuteChanged();
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
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnlineAsync。
    /// 正文中的关键协作调用包括 nameof、CanUseConnection、OnlineAsync；具体条件和先后次序见方法体。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUseConnection))]
    private Task OnlineAsync() => CanUseConnection() ? Workspace.OnlineAsync() : Task.CompletedTask;
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
