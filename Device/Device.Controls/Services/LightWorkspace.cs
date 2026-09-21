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

    protected override string BuildKey(LightProfile profile) =>
        $"{profile.DriverId}:{profile.PortName}:{profile.ChannelCount}:{profile.MaximumIntensity}";

    protected override IDevice CreateDevice(LightProfile profile) =>
        factory.Create(profile.ToOptions());

    public async Task SetChannelAsync()
    {
        var controller = Connected<ILightController>();
        var result = await controller
            .SetIntensityAsync(Model.Channel, Model.Intensity)
            .ConfigureAwait(false);

        if (!result.Success)
            throw new InvalidOperationException(result.Message ?? result.ErrorCode ?? "亮度设置失败");
    }

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
