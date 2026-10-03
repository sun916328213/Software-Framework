// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：IProductionPlc。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MachineApp.Business.Station、WPFControls.Controls.Models。导入不代表所有类型都会在运行时加载。
using MachineApp.Business.Station;
using WPFControls.Controls.Models;

namespace MachineApp.Business.Ports;

/// <summary>
/// IProductionPlc 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
/// </summary>
public interface IProductionPlc
{
    bool IsOnline { get; }
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Refresh。
    /// </summary>
    void Refresh();
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 StopRefresh。
    /// </summary>
    void StopRefresh();
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 TriggerReply。
    /// </summary>
    void TriggerReply(string stationName, string pPLCOrder);
    /// <summary>
    /// 执行发送操作；对应入口为 SendData。
    /// </summary>
    void SendData(string stationName, string pPLCOrder, int offset, object info);
    /// <summary>
    /// 执行发送操作；对应入口为 SendResult。
    /// </summary>
    void SendResult(string stationName, string pPLCOrder, EPLCResult info);
    /// <summary>
    /// 读取当前实现提供的数据；对应入口为 ReadData。
    /// </summary>
    string ReadData(string stationName, string pPLCOrder, int offset = 0, bool islog = true);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 TriggerUserInfo。
    /// </summary>
    void TriggerUserInfo(UserInfo value);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 TriggerAsync。
    /// </summary>
    Task<EPLCResult> TriggerAsync(string stationName, string pPLCOrder, string data);
}
