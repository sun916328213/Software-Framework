using Extension.Automation.Device;
using Extension.Automation.Secs;
using Secs.Variable;
using SecsGem.Core;

namespace SecsGem.Runtime;

public partial class SecsGemRuntime : ISecsGemRuntime
{
    private ISecsGemEventHandler? _gemEventHandler;
    private IEquipmentDataProvider? _equipmentDataProvider;

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
        _equipmentDataProvider = provider;

    private void RefreshEquipmentData()
    {
        var provider = _equipmentDataProvider;
        if (provider == null) return;

        var values = provider.GetVariableValues()
            ?? throw new InvalidOperationException("Equipment data provider returned no variable snapshot.");
        foreach (var (name, value) in values)
        {
            if (_VIDManager.VIDDic_Name.TryGetValue(name, out var vid))
            {
                vid.Value = value!;
            }
        }
    }
}
