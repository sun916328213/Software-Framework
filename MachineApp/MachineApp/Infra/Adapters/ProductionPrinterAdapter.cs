// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：ProductionPrinterAdapter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MachineApp.Business.Ports、WPFControls.ProgramControl.Services。导入不代表所有类型都会在运行时加载。
using MachineApp.Business.Ports;
using WPFControls.ProgramControl.Services;

namespace MachineApp.Infra.Adapters;

/// <summary>
/// ProductionPrinterAdapter 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
/// 继承或实现的契约：IProductionPrinter。具体约束按这些契约及各成员实现解释。
/// </summary>
internal sealed class ProductionPrinterAdapter(PrintManagerService service) : IProductionPrinter
{
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Print。
    /// 正文中的关键协作调用包括 Print；具体条件和先后次序见方法体。
    /// </summary>
    public Task Print(int index) => service.Print(index);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 PrintLabel_Path。
    /// 正文中的关键协作调用包括 PrintLabel_Path；具体条件和先后次序见方法体。
    /// </summary>
    public Task PrintLabel_Path(string labelPath, string printerName) => service.PrintLabel_Path(labelPath, printerName);
    /// <summary>
    /// 执行关闭步骤；对应入口为 Close。
    /// 正文中的关键协作调用包括 Close；具体条件和先后次序见方法体。
    /// </summary>
    public void Close() => service.Close();
}
