// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：SecsPageModel、VariableRow。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：CommunityToolkit.Mvvm.ComponentModel、System.Collections.ObjectModel、SecsGem.Runtime.Variable、Secs4Net。导入不代表所有类型都会在运行时加载。
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using SecsGem.Runtime.Variable;
using Secs4Net;
using System.Collections.Generic;

namespace SecsGem.Controls.Models;

/// <summary>Workspace 持有的页面状态；不执行协议操作。</summary>
public partial class SecsPageModel : ObservableObject
{
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isClosed;
    public ObservableCollection<LogRow> Messages { get; } = [];
    [ObservableProperty] private LogRow? _selectedLog;
    [ObservableProperty] private int _connectionModeIndex = 0;
    [ObservableProperty] private string _address = "127.0.0.1";
    [ObservableProperty] private string _portText = "5000";
    [ObservableProperty] private string _deviceIdText = "0";
    [ObservableProperty] private string _sml = "'S1F1' W";
    [ObservableProperty] private bool _waitForReply = true;
    [ObservableProperty] private string _variableValue = "";
    [ObservableProperty] private string _newVariableId = "";
    [ObservableProperty] private string _newVariableName = "";
    /// <summary>0=SVID，1=DVID，2=ECID；与 SecsView 的下拉顺序一致。</summary>
    [ObservableProperty] private int _newVariableCategoryIndex = 0;
    [ObservableProperty] private SecsFormat _newVariableFormat = SecsFormat.ASCII;
    public IReadOnlyList<SecsFormat> VariableFormats { get; } = Enum.GetValues<SecsFormat>();
    [ObservableProperty] private string _newReportId = "";
    [ObservableProperty] private string _newReportName = "";
    [ObservableProperty] private string _newReportVids = "";
    [ObservableProperty] private string _newEventId = "";
    [ObservableProperty] private string _newEventName = "";
    [ObservableProperty] private string _newEventReports = "";
    /// <summary>事件的启用位；新增与修改共用（新增默认开）。</summary>
    [ObservableProperty] private bool _newEventEnable = true;
    [ObservableProperty] private string _newAlarmId = "";
    [ObservableProperty] private string _newAlarmDescription = "";
    /// <summary>报警的启用位；新增与修改共用（新增默认开）。</summary>
    [ObservableProperty] private bool _newAlarmEnable = true;
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
    [ObservableProperty] private ReportInfo? _selectedReport = null;
    [ObservableProperty] private AlarmInfo? _selectedAlarm = null;
}

/// <summary>
/// 消息监视器的一行：时间、种类、内容分三列放。
/// <para>
/// 原来是拼好的一个长字符串（`HH:mm:ss.fff [种类] 内容`），SML 一长就横向滚死、
/// 行高参差导致虚拟化失效。用结构化行 + DataGrid：行高等高虚拟化才起作用，
/// 长内容走横向滚动条和悬停 tooltip。
/// </para>
/// </summary>
public sealed record LogRow(string Time, string Kind, string Text)
{
    /// <summary>
    /// 列表里只显示第一行，保证行高等高（虚拟化 + 看得全的前提）。
    /// 全文在下面的详情框里竖着展开。
    /// </summary>
    public string Summary => Text.Split('\n', 2)[0].TrimEnd('\r');
}

/// <summary>
/// VariableRow 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
/// </summary>
public sealed record VariableRow(string Category, VIDInfo Variable)
{
    public string Id => Variable.ID;
    /// <summary>写进 ReportID 的引用形式（形如 SVID:102）。ID 只在本表内唯一，报表必须带类别才不歧义。</summary>
    public string QualifiedId => Variable.QualifiedId;
    public string Name => Variable.Name;
    public string Format => Variable.DataType.ToString();
    public string Value => Variable.Value?.ToString() ?? string.Empty;
}
