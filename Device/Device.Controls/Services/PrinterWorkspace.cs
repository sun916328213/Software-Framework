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

    protected override string BuildKey(PrinterProfile profile) =>
        $"{profile.DriverId}:{profile.IpAddress}:{profile.Port}:{profile.MaximumReadTimeoutMilliseconds}";

    protected override IDevice CreateDevice(PrinterProfile profile) =>
        factory.Create(profile.ToOptions());

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

    public async Task PrintRawAsync()
    {
        var printer = Connected<IPrinter>();
        var data = Encoding.UTF8.GetBytes(Model.RawData);
        var result = await printer.PrintRawAsync(data).ConfigureAwait(false);

        if (!result.Success)
            throw new InvalidOperationException(result.Message ?? result.ErrorCode ?? "打印失败");
    }
}
