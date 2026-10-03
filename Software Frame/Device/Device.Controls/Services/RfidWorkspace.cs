// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：RfidWorkspace。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Controls、Device.Core.Devices、Device.Core.Rfid、Device.Controls.Models、Extension.UI.Controls。导入不代表所有类型都会在运行时加载。
using Device.Controls;
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

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 BuildKey。
    /// </summary>
    protected override string BuildKey(RfidProfile profile) =>
        $"{profile.DriverId}:{profile.PortName}:{profile.BaudRate}:{profile.DataBits}:{profile.StopBits}:{profile.Parity}:{profile.SlaveId}:{profile.PageSizeBytes}:{profile.TimeoutMilliseconds}";

    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateDevice。
    /// 正文中的关键协作调用包括 Create、ToOptions；具体条件和先后次序见方法体。
    /// </summary>
    protected override IDevice CreateDevice(RfidProfile profile) =>
        factory.Create(profile.ToOptions());

    /// <summary>
    /// 读取当前实现提供的数据；对应入口为 ReadAsync。
    /// 正文中的关键协作调用包括 Connected、ConfigureAwait、ReadPagesAsync、UIInvokeSafe；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 执行写入操作；对应入口为 WriteAsync。
    /// 正文中的关键协作调用包括 Connected、ToList、Select、Split、Replace；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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
