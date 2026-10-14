// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：ISecsGemRuntime。成员说明可结合下方具体实现阅读。
namespace SecsGem.Core;

/// <summary>业务宿主使用的 SECS/GEM 运行时入口，不暴露传输库或 UI 类型。</summary>
public interface ISecsGemRuntime : IDisposable
{
    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetState。
    /// </summary>
    SecsGemState GetState();

    /// <summary>启动 HSMS 连接；返回值表示连接流程已启动，不表示已完成 Select。</summary>
    Task<bool> ConnectAsync(SecsGemOptions options);

    /// <summary>
    /// 执行断开连接操作；对应入口为 DisconnectAsync。
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 EstablishCommunicationAsync。
    /// </summary>
    Task EstablishCommunicationAsync();

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 GoOnlineRemoteAsync。
    /// </summary>
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 GoOnlineLocalAsync。
    /// 操作员侧进 Online_Local：只改 SEMI 模式并上报 S6F11，不发 S1F17（Host→Equipment）。
    /// </summary>
    Task<bool> GoOnlineLocalAsync();

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 GoOnlineRemoteAsync。
    /// 操作员侧进 Online_Remote：只改 SEMI 模式并上报 S6F11，不发 S1F17（Host→Equipment）。
    /// </summary>
    Task<bool> GoOnlineRemoteAsync();

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 GoOfflineAsync。
    /// </summary>
    Task<bool> GoOfflineAsync();

    /// <summary>
    /// 关闭前的有序下线：先把控制状态转 OFFLINE 并上报 S6F11(ControlStateChange)，
    /// 再断 HSMS/TCP。EQ 不发 S1F15（那是 Host→Equipment），关闭时靠状态变更事件通知 Host。
    /// 下线通知是尽力而为——超时或失败都只告警，不阻断关闭。
    /// </summary>
    /// <param name="notifyTimeout">等待下线通知（含 S6F12 应答）的上限。</param>
    Task ShutdownAsync(TimeSpan notifyTimeout);

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetVariable。
    /// </summary>
    void SetVariable(string name, object value);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 AppendVariable。
    /// </summary>
    void AppendVariable(string name, object value);

    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetVariableValue。
    /// </summary>
    string? GetVariableValue(string name);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ReportEventAsync。
    /// </summary>
    Task<bool> ReportEventAsync(string eventName);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ReportAlarmAsync。
    /// </summary>
    Task<bool> ReportAlarmAsync(SecsGemAlarm alarm);

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetEventHandler。
    /// </summary>
    void SetEventHandler(ISecsGemEventHandler? handler);

    /// <summary>注册后，Host 查询及事件上报前的设备快照会更新同名 VID 值。</summary>
    void SetEquipmentDataProvider(IEquipmentDataProvider? provider);
}
