// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：CameraFactoryRuntimeGuardTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Core.Cameras、Device.Core.Devices、Device.Runtime.Cameras、Device.Runtime.Common、Xunit。导入不代表所有类型都会在运行时加载。
using Device.Core.Cameras;
using Device.Core.Devices;
using Device.Runtime.Cameras;
using Device.Runtime.Common;
using Xunit;

namespace Device.Tests.Runtime;

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

    /// <summary>
    /// 验证 ValidOptions 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// </summary>
    private static CameraOptions ValidOptions(string driverId) => new()
    {
        Name = "Camera01",
        DriverId = new DeviceDriverId(driverId),
        IpAddress = "192.168.1.10",
    };
}
