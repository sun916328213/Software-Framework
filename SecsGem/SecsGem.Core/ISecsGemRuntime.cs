namespace SecsGem.Core;

/// <summary>业务宿主使用的 SECS/GEM 运行时入口，不暴露传输库或 UI 类型。</summary>
public interface ISecsGemRuntime : IDisposable
{
    SecsGemState GetState();

    /// <summary>启动 HSMS 连接；返回值表示连接流程已启动，不表示已完成 Select。</summary>
    Task<bool> ConnectAsync(SecsGemOptions options);

    Task DisconnectAsync();

    Task EstablishCommunicationAsync();

    Task<bool> GoOnlineRemoteAsync();

    Task<bool> GoOfflineAsync();

    void SetVariable(string name, object value);

    void AppendVariable(string name, object value);

    string? GetVariableValue(string name);

    Task<bool> ReportEventAsync(string eventName);

    Task<bool> ReportAlarmAsync(SecsGemAlarm alarm);

    void SetEventHandler(ISecsGemEventHandler? handler);

    /// <summary>注册后，Host 查询及事件上报前的设备快照会更新同名 VID 值。</summary>
    void SetEquipmentDataProvider(IEquipmentDataProvider? provider);
}
