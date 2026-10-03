// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：RuntimePlatformGuardCollection、RuntimePlatformGuardTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Runtime.Common、Xunit。导入不代表所有类型都会在运行时加载。
using Device.Runtime.Common;
using Xunit;

namespace Device.Tests.Runtime;

/// <summary>运行时守卫测试集合：守卫的可替换边界是进程级静态状态，同集合测试串行执行。</summary>
[CollectionDefinition("RuntimePlatformGuard")]
public sealed class RuntimePlatformGuardCollection
{
}

/// <summary>
/// 验证运行时平台守卫的错误类型与消息稳定性（纯逻辑，不触碰厂商 SDK）。
/// </summary>
[Collection("RuntimePlatformGuard")]
public sealed class RuntimePlatformGuardTests
{
    /// <summary>
    /// 满足 64 位条件时守卫静默通过。
    /// </summary>
    [Fact]
    public void Require64BitProcess_When64Bit_DoesNotThrow()
    {
        try
        {
            RuntimePlatformGuard.Is64BitProcessProvider = static () => true;

            var exception = Record.Exception(() => RuntimePlatformGuard.Require64BitProcess("测试相机"));

            Assert.Null(exception);
        }
        finally
        {
            RuntimePlatformGuard.ResetForTests();
        }
    }

    /// <summary>
    /// 不满足 64 位条件时抛 PlatformNotSupportedException，消息稳定且可诊断。
    /// </summary>
    [Fact]
    public void Require64BitProcess_When32Bit_ThrowsStableDiagnosticError()
    {
        try
        {
            RuntimePlatformGuard.Is64BitProcessProvider = static () => false;

            var exception = Assert.Throws<PlatformNotSupportedException>(
                () => RuntimePlatformGuard.Require64BitProcess("海康 GigE 相机"));

            Assert.Contains("海康 GigE 相机", exception.Message, StringComparison.Ordinal);
            Assert.Contains("64 位", exception.Message, StringComparison.Ordinal);
            Assert.Contains("AnyCPU", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            RuntimePlatformGuard.ResetForTests();
        }
    }

    /// <summary>
    /// 守卫的进程判断委托不可设空。
    /// </summary>
    [Fact]
    public void Is64BitProcessProvider_SetNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => RuntimePlatformGuard.Is64BitProcessProvider = null!);
    }
}
