using Device.Core.Devices;
using Device.Core.Rfid;
using Device.Controls.Models;
using Extension.UI.Controls;

namespace Device.Controls.Services;

/// <summary>
/// RFID 读写器测试页流程：按页读取/写入（写入内容一行一页）。
/// </summary>
public sealed class RfidWorkspace(IRfidDeviceFactory factory)
    : DeviceWorkspaceBase<RfidPageModel, RfidProfile>(factory.AvailableDrivers)
{
    protected override string CategoryName => "RFID";

    protected override string FallbackProfileName => "RFID01";

    protected override string BuildKey(RfidProfile profile) =>
        $"{profile.DriverId}:{profile.PortName}:{profile.BaudRate}:{profile.DataBits}:{profile.StopBits}:{profile.Parity}:{profile.SlaveId}:{profile.PageSizeBytes}:{profile.TimeoutMilliseconds}";

    protected override IDevice CreateDevice(RfidProfile profile) =>
        factory.Create(profile.ToOptions());

    public async Task ReadAsync()
    {
        var device = Connected<IRfidDevice>();
        var result = await device
            .ReadPagesAsync(Model.StartPage, Model.PageCount)
            .ConfigureAwait(false);

        if (!result.Success)
            throw new InvalidOperationException(result.Message ?? result.ErrorCode ?? "读卡失败");

        var text = result.Value ?? string.Empty;
        DispatcherEx.UIInvokeSafe(() => Model.ReadResult = text);
    }

    public async Task WriteAsync()
    {
        var device = Connected<IRfidDevice>();
        var pages = Model.WriteText
            .Replace("\r", string.Empty)
            .Split('\n')
            .Select(p => p.TrimEnd())
            .ToList();

        if (pages.Count == 0 || pages.All(string.IsNullOrEmpty))
            throw new InvalidOperationException("写入内容为空");

        var result = await device
            .WritePagesAsync(Model.StartPage, pages)
            .ConfigureAwait(false);

        if (!result.Success)
            throw new InvalidOperationException(result.Message ?? result.ErrorCode ?? "写卡失败");
    }
}
