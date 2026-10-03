// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：SecsGemRuntime。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Extension.Automation.Device、Extension.Automation.Secs、SecsGem.Runtime.Variable、SecsGem.Core。导入不代表所有类型都会在运行时加载。
using Extension.Automation.Device;
using Extension.Automation.Secs;
using SecsGem.Runtime.Variable;
using SecsGem.Core;

namespace SecsGem.Runtime;

/// <summary>
/// SecsGemRuntime 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
/// 继承或实现的契约：ISecsGemRuntime。具体约束按这些契约及各成员实现解释。
/// </summary>
public partial class SecsGemRuntime : ISecsGemRuntime
{
    private ISecsGemEventHandler? _gemEventHandler;

    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetState。
    /// </summary>
    public SecsGemState GetState() => new(
        IsConnected,
        Mode switch
        {
            ESecsMode.Offline => SecsGemMode.Offline,
            ESecsMode.Online_Local => SecsGemMode.OnlineLocal,
            ESecsMode.Online_Remote => SecsGemMode.OnlineRemote,
            _ => throw new InvalidOperationException($"Unknown SECS mode: {Mode}")
        },
        Status switch
        {
            EDeviceStatus.Idle => SecsGemEquipmentStatus.Idle,
            EDeviceStatus.Ready => SecsGemEquipmentStatus.Ready,
            EDeviceStatus.Running => SecsGemEquipmentStatus.Running,
            EDeviceStatus.Paused => SecsGemEquipmentStatus.Paused,
            EDeviceStatus.Error => SecsGemEquipmentStatus.Error,
            _ => throw new InvalidOperationException($"Unknown equipment status: {Status}")
        });

    /// <summary>
    /// 发起连接操作；对应入口为 ConnectAsync。
    /// 正文中的关键协作调用包括 ThrowIfNull、Validate、ConnectAsync；具体条件和先后次序见方法体。
    /// </summary>
    public Task<bool> ConnectAsync(SecsGemOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        return ConnectAsync(
            options.IsActiveMode,
            options.IpAddress,
            options.Port,
            options.DeviceId,
            options.BufferSize);
    }

    /// <summary>
    /// 执行断开连接操作；对应入口为 DisconnectAsync。
    /// 正文中的关键协作调用包括 CloseAsync；具体条件和先后次序见方法体。
    /// </summary>
    public Task DisconnectAsync() => CloseAsync();

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetVariable。
    /// 正文中的关键协作调用包括 SetVid；具体条件和先后次序见方法体。
    /// </summary>
    public void SetVariable(string name, object value) => SetVid(name, value);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 AppendVariable。
    /// 正文中的关键协作调用包括 AppendVid；具体条件和先后次序见方法体。
    /// </summary>
    public void AppendVariable(string name, object value) => AppendVid(name, value);

    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetVariableValue。
    /// 正文中的关键协作调用包括 RefreshEquipmentData、GetVidValue；具体条件和先后次序见方法体。
    /// </summary>
    public string? GetVariableValue(string name)
    {
        RefreshEquipmentData();
        return GetVidValue(name);
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ReportEventAsync。
    /// 正文中的关键协作调用包括 S6F11EventReportSendAsync；具体条件和先后次序见方法体。
    /// </summary>
    public Task<bool> ReportEventAsync(string eventName) => S6F11EventReportSendAsync(eventName);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ReportAlarmAsync。
    /// 正文中的关键协作调用包括 ThrowIfNull、FirstOrDefault、GetDatas、S5F1AlarmReportSendAsync；具体条件和先后次序见方法体。
    /// </summary>
    public Task<bool> ReportAlarmAsync(SecsGemAlarm alarm)
    {
        ArgumentNullException.ThrowIfNull(alarm);
        var configured = _vidManager.Alarm.GetDatas().FirstOrDefault(x => x.ID == alarm.Id);
        if (configured != null)
            configured.Code = alarm.Code;
        return S5F1AlarmReportSendAsync(new AlarmInfo
        {
            ID = alarm.Id,
            Description = alarm.Description,
            Code = alarm.Code,
            Enable = alarm.Enabled
        });
    }

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetEventHandler。
    /// </summary>
    public void SetEventHandler(ISecsGemEventHandler? handler) => _gemEventHandler = handler;

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetEquipmentDataProvider。
    /// 正文中的关键协作调用包括 SetProvider；具体条件和先后次序见方法体。
    /// </summary>
    public void SetEquipmentDataProvider(IEquipmentDataProvider? provider) =>
        _dataBinding.SetProvider(provider);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 RefreshEquipmentData。
    /// 正文中的关键协作调用包括 Refresh；具体条件和先后次序见方法体。
    /// </summary>
    private void RefreshEquipmentData() => _dataBinding.Refresh();
}
