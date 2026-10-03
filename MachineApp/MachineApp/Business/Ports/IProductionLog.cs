// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：IProductionLog。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core.Log.Definition。导入不代表所有类型都会在运行时加载。
using DataManager.Core.Log.Definition;

namespace MachineApp.Business.Ports;

/// <summary>
/// IProductionLog 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
/// </summary>
public interface IProductionLog
{
    /// <summary>
    /// 执行添加操作；对应入口为 AddRunInfo。
    /// </summary>
    void AddRunInfo(string info, ELogLevel level = ELogLevel.Information, int retainNum = 1000);
}
