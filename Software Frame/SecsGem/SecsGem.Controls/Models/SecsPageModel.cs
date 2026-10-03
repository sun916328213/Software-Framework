// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：SecsPageModel、VariableRow。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：CommunityToolkit.Mvvm.ComponentModel、System.Collections.ObjectModel、SecsGem.Runtime.Variable、Secs4Net。导入不代表所有类型都会在运行时加载。
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using SecsGem.Runtime.Variable;
using Secs4Net;

namespace SecsGem.Controls.Models;

/// <summary>Workspace 持有的页面状态；不执行协议操作。</summary>
public partial class SecsPageModel : ObservableObject
{
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isClosed;
    public ObservableCollection<string> Messages { get; } = [];
    [ObservableProperty] private int _connectionModeIndex = 0;
    [ObservableProperty] private string _address = "127.0.0.1";
    [ObservableProperty] private string _portText = "5000";
    [ObservableProperty] private string _deviceIdText = "0";
    [ObservableProperty] private string _sml = "'S1F1' W";
    [ObservableProperty] private bool _waitForReply = true;
    [ObservableProperty] private string _variableValue = "";
    [ObservableProperty] private string _hcack = "0";
    [ObservableProperty] private string _connectionStatus = "未连接";
    [ObservableProperty] private string _modeStatus = "Offline";
    [ObservableProperty] private string _deviceStatus = "Idle";
    [ObservableProperty] private List<VariableRow> _variables = [];
    [ObservableProperty] private IEnumerable<CEIDInfo> _events = [];
    [ObservableProperty] private IEnumerable<ReportInfo> _reports = [];
    [ObservableProperty] private IEnumerable<AlarmInfo> _alarms = [];
    [ObservableProperty] private VariableRow? _selectedVariable = null;
    [ObservableProperty] private CEIDInfo? _selectedEvent = null;
    [ObservableProperty] private AlarmInfo? _selectedAlarm = null;
}

/// <summary>
/// VariableRow 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
/// </summary>
public sealed record VariableRow(string Category, VIDInfo Variable)
{
    public string Id => Variable.ID;
    public string Name => Variable.Name;
    public string Format => Variable.DataType.ToString();
    public string Value => Variable.Value?.ToString() ?? string.Empty;
}
