// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：PrinterWorkspace。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Controls、System.Text、Device.Core.Devices、Device.Core.Printers、Device.Controls.Models、Extension.UI.Controls。导入不代表所有类型都会在运行时加载。
using Device.Controls;
using System.Text;
using Device.Core.Devices;
using Device.Core.Printers;
using Device.Controls.Models;
using Extension.UI.Controls;

namespace Device.Controls.Services;

/// <summary>
/// 网络打印机测试页流程：状态查询摘要 + 原始打印语言（ZPL/SBPL）文本直发。
/// </summary>
public sealed class PrinterWorkspace(IPrinterFactory factory)
    : DeviceWorkspaceBase<PrinterPageModel, PrinterProfile>(factory.AvailableDrivers)
{
    protected override string CategoryName => "打印机";

    protected override string FallbackProfileName => "打印机01";

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 BuildKey。
    /// </summary>
    protected override string BuildKey(PrinterProfile profile) =>
        $"{profile.DriverId}:{profile.IpAddress}:{profile.Port}:{profile.MaximumReadTimeoutMilliseconds}";

    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateDevice。
    /// 正文中的关键协作调用包括 Create、ToOptions；具体条件和先后次序见方法体。
    /// </summary>
    protected override IDevice CreateDevice(PrinterProfile profile) =>
        factory.Create(profile.ToOptions());

    /// <summary>
    /// 执行查询操作；对应入口为 QueryStatusAsync。
    /// 正文中的关键协作调用包括 Connected、ConfigureAwait、GetStatusAsync、UIInvokeSafe；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    public async Task QueryStatusAsync()
    {
        var printer = Connected<IPrinter>();
        var result = await printer.GetStatusAsync().ConfigureAwait(false);
        if (!result.Success || result.Value is null)
            throw new InvalidOperationException(result.Message ?? result.ErrorCode ?? "状态查询失败");

        var status = result.Value;
        DispatcherEx.UIInvokeSafe(() =>
            Model.StatusSummary = $"{status.Summary} · {(status.HasFault ? "存在故障" : "正常")}");
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 PrintRawAsync。
    /// 正文中的关键协作调用包括 Connected、GetBytes、ConfigureAwait、PrintRawAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    public async Task PrintRawAsync()
    {
        var printer = Connected<IPrinter>();
        var data = Encoding.UTF8.GetBytes(Model.RawData);
        var result = await printer.PrintRawAsync(data).ConfigureAwait(false);

        if (!result.Success)
            throw new InvalidOperationException(result.Message ?? result.ErrorCode ?? "打印失败");
    }
}
