using Device.Core.Cameras;
using Device.Core.Devices;
using Device.Runtime.Cameras;
using Xunit;

namespace Device.Runtime.Tests;

/// <summary>
/// 验证各品牌相机适配器在生命周期边界上的一致行为。
/// </summary>
/// <remarks>
/// 这些测试只使用尚未连接的相机实例，执行路径在进入厂商 SDK 之前就返回，
/// 因此不需要相机硬件或厂商运行时环境。
/// </remarks>
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

    private static ICamera CreateCamera(string driverId) =>
        new CameraFactory().Create(new CameraOptions
        {
            Name = "Camera01",
            DriverId = new DeviceDriverId(driverId),
            IpAddress = "192.168.1.10",
        });
}
