// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：ScannerWorkspace。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Controls、Device.Core.Devices、Device.Core.Scanners、Device.Controls.Models、Extension.UI.Controls。导入不代表所有类型都会在运行时加载。
using Device.Controls;
using Device.Core.Devices;
using Device.Core.Scanners;
using Device.Controls.Models;
using Extension.UI.Controls;

namespace Device.Controls.Services;

/// <summary>
/// 扫码器测试页流程：连接后订阅 <see cref="IScanner.ScanReceived"/>，
/// 把每条完整扫码记录写入收件箱（被动设备，无主动触发命令）。
/// </summary>
public sealed class ScannerWorkspace(IScannerFactory factory)
    : DeviceWorkspaceBase<ScannerPageModel, ScannerProfile>(factory.AvailableDrivers)
{
    private EventHandler<ScanReceivedEventArgs>? _scanHandler;

    protected override string CategoryName => "扫码器";

    protected override string FallbackProfileName => "扫码器01";

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 BuildKey。
    /// </summary>
    protected override string BuildKey(ScannerProfile profile) =>
        $"{profile.DriverId}:{profile.PortName}:{profile.BaudRate}:{profile.DataBits}:{profile.StopBits}:{profile.Parity}";

    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateDevice。
    /// 正文中的关键协作调用包括 Create、ToOptions；具体条件和先后次序见方法体。
    /// </summary>
    protected override IDevice CreateDevice(ScannerProfile profile) =>
        factory.Create(profile.ToOptions());

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnDeviceCreated。
    /// 正文中的关键协作调用包括 UIInvokeSafe、AddScan、ToString、ToLocalTime；具体条件和先后次序见方法体。
    /// </summary>
    protected override void OnDeviceCreated(IDevice device)
    {
        if (device is not IScanner scanner)
            return;

        _scanHandler = (_, e) => DispatcherEx.UIInvokeSafe(() =>
            Model.AddScan(new ScanRecord(e.ReceivedAt.ToLocalTime().ToString("HH:mm:ss"), e.Value)));
        scanner.ScanReceived += _scanHandler;
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnDeviceReleasing。
    /// </summary>
    protected override void OnDeviceReleasing(IDevice device)
    {
        if (device is IScanner scanner && _scanHandler is not null)
            scanner.ScanReceived -= _scanHandler;
        _scanHandler = null;
    }

    /// <summary>
    /// 处理对应通知、事件或生命周期回调；对应入口为 OnConnectionChanged。
    /// 正文中的关键协作调用包括 Clear；具体条件和先后次序见方法体。
    /// </summary>
    protected override void OnConnectionChanged(bool connected)
    {
        if (connected)
            Model.Scans.Clear();
    }
}
