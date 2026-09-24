using Device.Core.Cameras;
using Device.Core.Devices;
using Device.Runtime.Cameras;
using Device.Runtime.Common;
using Xunit;

namespace Device.Runtime.Tests;

/// <summary>
/// 验证受限驱动在创建前执行运行时守卫，且不会被误报为“驱动不存在”。
/// </summary>
[Collection("RuntimePlatformGuard")]
public sealed class CameraFactoryRuntimeGuardTests
{
    /// <summary>
    /// 模拟 32 位进程时，受限品牌在触碰厂商类型前即被拒绝，错误明确指向运行环境。
    /// </summary>
    /// <param name="driverId">受 64 位守卫保护的驱动标识。</param>
    /// <param name="expectedBrandText">错误消息中应出现的驱动显示名。</param>
    [Theory]
    [InlineData("camera.hik.gige", "海康 GigE 相机")]
    [InlineData("camera.basler.gige", "Basler GigE 相机")]
    [InlineData("camera.dahua.gige", "大华 GigE 相机")]
    public void Create_GuardedDriverOn32Bit_ThrowsPlatformErrorBeforeVendorTypes(
        string driverId,
        string expectedBrandText)
    {
        try
        {
            RuntimePlatformGuard.Is64BitProcessProvider = static () => false;
            var factory = new CameraFactory();
            var options = ValidOptions(driverId);

            var exception = Assert.Throws<PlatformNotSupportedException>(() => factory.Create(options));

            Assert.Contains(expectedBrandText, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            RuntimePlatformGuard.ResetForTests();
        }
    }

    /// <summary>
    /// 未知驱动标识仍走“驱动不存在”路径，不被平台守卫掩盖。
    /// </summary>
    [Fact]
    public void Create_UnknownDriverOn32Bit_StillReportsNotFound()
    {
        try
        {
            RuntimePlatformGuard.Is64BitProcessProvider = static () => false;
            var factory = new CameraFactory();

            Assert.Throws<CameraDriverNotFoundException>(
                () => factory.Create(ValidOptions("camera.unknown.gige")));
        }
        finally
        {
            RuntimePlatformGuard.ResetForTests();
        }
    }

    private static CameraOptions ValidOptions(string driverId) => new()
    {
        Name = "Camera01",
        DriverId = new DeviceDriverId(driverId),
        IpAddress = "192.168.1.10",
    };
}
