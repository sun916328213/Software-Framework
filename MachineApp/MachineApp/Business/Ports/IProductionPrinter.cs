// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：IProductionPrinter。成员说明可结合下方具体实现阅读。
namespace MachineApp.Business.Ports;

/// <summary>
/// IProductionPrinter 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
/// </summary>
public interface IProductionPrinter
{
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Print。
    /// </summary>
    Task Print(int index);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 PrintLabel_Path。
    /// </summary>
    Task PrintLabel_Path(string labelPath, string printerName);
    /// <summary>
    /// 执行关闭步骤；对应入口为 Close。
    /// </summary>
    void Close();
}
