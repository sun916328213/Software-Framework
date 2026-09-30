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
