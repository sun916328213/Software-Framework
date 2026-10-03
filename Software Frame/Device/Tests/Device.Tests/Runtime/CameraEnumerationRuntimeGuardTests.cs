// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：CameraEnumerationRuntimeGuardTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Runtime.Cameras、Device.Runtime.Common、Xunit。导入不代表所有类型都会在运行时加载。
using Device.Runtime.Cameras;
using Device.Runtime.Common;
using Xunit;

namespace Device.Tests.Runtime;

/// <summary>
/// 验证某品牌因运行环境失败时，枚举仍继续处理其他品牌并生成 partial/failure 结果。
/// </summary>
[Collection("RuntimePlatformGuard")]
public sealed class CameraEnumerationRuntimeGuardTests
{
    /// <summary>
    /// 模拟 32 位进程时，三个受限品牌记为单品牌失败（品牌前缀保留），整体按失败/部分成功返回。
    /// </summary>
    [Fact]
    public async Task EnumerateDevicesAsync_GuardedBrandsFailOn32Bit_OthersStillProcessed()
    {
        try
        {
            RuntimePlatformGuard.Is64BitProcessProvider = static () => false;
            var factory = new CameraFactory();

            var result = await factory.EnumerateDevicesAsync();

            Assert.NotNull(result.Message);
            Assert.Contains("Hikvision", result.Message, StringComparison.Ordinal);
            Assert.Contains("Basler", result.Message, StringComparison.Ordinal);
            Assert.Contains("Dahua", result.Message, StringComparison.Ordinal);
            Assert.True(
                result.ErrorCode is "camera.enumerate_failed" or "camera.enumerate_partial",
                $"枚举错误码应为失败或部分成功，实际为 {result.ErrorCode}。");
        }
        finally
        {
            RuntimePlatformGuard.ResetForTests();
        }
    }
}
