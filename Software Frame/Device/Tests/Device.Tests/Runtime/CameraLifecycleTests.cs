// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：CameraLifecycleTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Core.Cameras、Device.Core.Devices、Device.Runtime.Cameras、Xunit。导入不代表所有类型都会在运行时加载。
using Device.Core.Cameras;
using Device.Core.Devices;
using Device.Runtime.Cameras;
using Xunit;

namespace Device.Tests.Runtime;

/// <summary>
/// 验证各品牌相机适配器在生命周期边界上的一致行为。
/// </summary>
/// <remarks>
/// 这些测试只使用尚未连接的相机实例，执行路径在进入厂商 SDK 之前就返回，
/// 因此不需要相机硬件或厂商运行时环境。
/// 与运行时守卫测试同集合串行执行，避免进程级守卫状态互相干扰。
/// </remarks>
[Collection("RuntimePlatformGuard")]
public sealed class CameraLifecycleTests
{
    private static readonly string[] DriverIds =
    [
        "camera.basler.gige",
        "camera.dahua.gige",
        "camera.hik.gige",
        "camera.itek.gige",
    ];

    /// <summary>
    /// 未连接时下发触发配置必须失败，并且错误信息里带上真实的相机名称。
    /// </summary>
    /// <param name="driverId">待验证的驱动标识。</param>
    [Theory]
    [InlineData("camera.basler.gige")]
    [InlineData("camera.dahua.gige")]
    [InlineData("camera.hik.gige")]
    [InlineData("camera.itek.gige")]
    public async Task SetTriggerAsync_NotConnected_ReportsRealCameraName(string driverId)
    {
        await using var camera = CreateCamera(driverId);

        var result = await camera.SetTriggerAsync(
            new CameraTriggerSettings(),
            new CameraTriggerSettings());

        Assert.False(result.Success);
        Assert.NotNull(result.Message);
        Assert.Contains(camera.Name, result.Message!, StringComparison.Ordinal);
        Assert.DoesNotContain("{Name}", result.Message!, StringComparison.Ordinal);
    }

    /// <summary>
    /// 未连接时下发曝光增益必须返回稳定错误码，而不是抛出厂商异常。
    /// </summary>
    /// <param name="driverId">待验证的驱动标识。</param>
    /// <param name="expectedErrorCode">该品牌未连接时应返回的稳定错误码。</param>
    [Theory]
    [InlineData("camera.basler.gige", "camera.basler.not_connected")]
    [InlineData("camera.dahua.gige", "camera.dahua.not_connected")]
    [InlineData("camera.hik.gige", "camera.hik.not_connected")]
    [InlineData("camera.itek.gige", "camera.itek.not_connected")]
    public async Task SetExposureGainAsync_NotConnected_ReturnsStableErrorCode(
        string driverId,
        string expectedErrorCode)
    {
        await using var camera = CreateCamera(driverId);

        var result = await camera.SetExposureGainAsync(20, 0);

        Assert.False(result.Success);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
    }

    /// <summary>
    /// 重复释放必须幂等：第二次调用不能抛出，也不能把相机留在已连接状态。
    /// </summary>
    /// <param name="driverId">待验证的驱动标识。</param>
    /// <remarks>
    /// 这里不断言最终状态一定是 <see cref="DeviceState.Disconnected"/>：未安装厂商运行时的机器上
    /// 资源清理会返回失败并置为 <see cref="DeviceState.Faulted"/>，该差异由部署环境决定，不属于契约。
    /// </remarks>
    [Theory]
    [InlineData("camera.basler.gige")]
    [InlineData("camera.dahua.gige")]
    [InlineData("camera.hik.gige")]
    [InlineData("camera.itek.gige")]
    public async Task DisposeAsync_RepeatedCalls_AreIdempotent(string driverId)
    {
        var camera = CreateCamera(driverId);

        await camera.DisposeAsync();
        await camera.DisposeAsync();

        Assert.NotEqual(DeviceState.Connected, camera.State);
    }

    /// <summary>
    /// 验证 CreateCamera 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Create；具体条件和先后次序见方法体。
    /// </summary>
    private static ICamera CreateCamera(string driverId) =>
        new CameraFactory().Create(new CameraOptions
        {
            Name = "Camera01",
            DriverId = new DeviceDriverId(driverId),
            IpAddress = "192.168.1.10",
        });
}
