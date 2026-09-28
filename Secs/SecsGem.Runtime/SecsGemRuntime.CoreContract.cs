using Extension.Automation.Device;
using Extension.Automation.Secs;
using Secs.Variable;
using SecsGem.Core;

namespace SecsGem.Runtime;

public partial class SecsGemRuntime : ISecsGemRuntime
{
    private ISecsGemEventHandler? _gemEventHandler;

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

    public Task<bool> ConnectAsync(SecsGemOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return ConnectAsync(
            options.IsActiveMode,
            options.IpAddress,
            options.Port,
            options.DeviceId,
            options.BufferSize);
    }

    public Task DisconnectAsync() => CloseAsync();

    public void SetVariable(string name, object value) => SetVid(name, value);

    public void AppendVariable(string name, object value) => AppendVid(name, value);

    public string? GetVariableValue(string name)
    {
        RefreshEquipmentData();
        return GetVidValue(name);
    }

    public Task<bool> ReportEventAsync(string eventName) => S6F11EventReportSendAsync(eventName);

    public Task<bool> ReportAlarmAsync(SecsGemAlarm alarm)
    {
        ArgumentNullException.ThrowIfNull(alarm);
        return S5F1AlarmReportSendAsync(new AlarmInfo
        {
            ID = alarm.Id,
            Description = alarm.Description,
            Code = alarm.Code,
            Enable = alarm.Enabled
        });
    }

    public void SetEventHandler(ISecsGemEventHandler? handler) => _gemEventHandler = handler;

    public void SetEquipmentDataProvider(IEquipmentDataProvider? provider) =>
        _dataBinding.SetProvider(provider);

    private void RefreshEquipmentData() => _dataBinding.Refresh();
}
