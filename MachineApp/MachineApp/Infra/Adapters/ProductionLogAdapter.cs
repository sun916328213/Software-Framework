// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：ProductionLogAdapter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core.Log.Definition、MachineApp.Business.Ports、WPFControls.ProgramControl.Services。导入不代表所有类型都会在运行时加载。
using DataManager.Core.Log.Definition;
using MachineApp.Business.Ports;
using WPFControls.ProgramControl.Services;

namespace MachineApp.Infra.Adapters;

/// <summary>
/// ProductionLogAdapter 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
/// 继承或实现的契约：IProductionLog。具体约束按这些契约及各成员实现解释。
/// </summary>
internal sealed class ProductionLogAdapter(LogInfoService service) : IProductionLog
{
    /// <summary>
    /// 执行添加操作；对应入口为 AddRunInfo。
    /// 正文中的关键协作调用包括 AddRunInfo；具体条件和先后次序见方法体。
    /// </summary>
    public void AddRunInfo(string info, ELogLevel level = ELogLevel.Information, int retainNum = 1000) =>
        service.AddRunInfo(info, level, retainNum);
}
