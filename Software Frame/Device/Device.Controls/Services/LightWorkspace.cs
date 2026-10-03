// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：LightWorkspace。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Controls、Device.Core.Devices、Device.Core.Lights、Device.Controls.Models。导入不代表所有类型都会在运行时加载。
using Device.Controls;
using Device.Core.Devices;
using Device.Core.Lights;
using Device.Controls.Models;

namespace Device.Controls.Services;

/// <summary>
/// 光源控制器测试页流程：单通道与全通道亮度下发。
/// </summary>
public sealed class LightWorkspace(ILightControllerFactory factory)
    : DeviceWorkspaceBase<LightPageModel, LightProfile>(factory.AvailableDrivers)
{
    protected override string CategoryName => "光源";

    protected override string FallbackProfileName => "光源01";

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 BuildKey。
    /// </summary>
    protected override string BuildKey(LightProfile profile) =>
        $"{profile.DriverId}:{profile.PortName}:{profile.ChannelCount}:{profile.MaximumIntensity}";

    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateDevice。
    /// 正文中的关键协作调用包括 Create、ToOptions；具体条件和先后次序见方法体。
    /// </summary>
    protected override IDevice CreateDevice(LightProfile profile) =>
        factory.Create(profile.ToOptions());

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetChannelAsync。
    /// 正文中的关键协作调用包括 Connected、ConfigureAwait、SetIntensityAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    public async Task SetChannelAsync()
    {
        var controller = Connected<ILightController>();
        var result = await controller
            .SetIntensityAsync(Model.Channel, Model.Intensity)
            .ConfigureAwait(false);

        if (!result.Success)
            throw new InvalidOperationException(result.Message ?? result.ErrorCode ?? "亮度设置失败");
    }

    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetAllAsync。
    /// 正文中的关键协作调用包括 Connected、ConfigureAwait、SetAllIntensityAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    public async Task SetAllAsync()
    {
        var controller = Connected<ILightController>();
        var result = await controller
            .SetAllIntensityAsync(Model.AllIntensity)
            .ConfigureAwait(false);

        if (!result.Success)
            throw new InvalidOperationException(result.Message ?? result.ErrorCode ?? "全通道设置失败");
    }
}
