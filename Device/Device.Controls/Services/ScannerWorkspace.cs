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

    protected override string BuildKey(ScannerProfile profile) =>
        $"{profile.DriverId}:{profile.PortName}:{profile.BaudRate}:{profile.DataBits}:{profile.StopBits}:{profile.Parity}";

    protected override IDevice CreateDevice(ScannerProfile profile) =>
        factory.Create(profile.ToOptions());

    protected override void OnDeviceCreated(IDevice device)
    {
        if (device is not IScanner scanner)
            return;

        _scanHandler = (_, e) => DispatcherEx.UIInvokeSafe(() =>
            Model.AddScan(new ScanRecord(e.ReceivedAt.ToLocalTime().ToString("HH:mm:ss"), e.Value)));
        scanner.ScanReceived += _scanHandler;
    }

    protected override void OnDeviceReleasing(IDevice device)
    {
        if (device is IScanner scanner && _scanHandler is not null)
            scanner.ScanReceived -= _scanHandler;
        _scanHandler = null;
    }

    protected override void OnConnectionChanged(bool connected)
    {
        if (connected)
            Model.Scans.Clear();
    }
}
