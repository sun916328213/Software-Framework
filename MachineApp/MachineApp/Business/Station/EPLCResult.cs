// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：EPLCResult。成员说明可结合下方具体实现阅读。
namespace MachineApp.Business.Station
{
    /// <summary>
    /// EPLCResult 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// </summary>
    public enum EPLCResult
    {
        OK = 1,
        NG = 2,
        ERROR = 3,
    }
}