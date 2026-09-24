namespace Device.Runtime.Common;

/// <summary>
/// Device.Runtime 内部运行时平台守卫：项目统一 AnyCPU，不替厂商 SDK 决定进程位数；
/// 已知仅提供 64 位程序集的驱动在真正 Create / Enumerate 之前显式检查并给出可诊断错误。
/// <para>internal 静态守卫，不进入 Device.Core，不新增公共服务、接口或 DI 组件。</para>
/// </summary>
internal static class RuntimePlatformGuard
{
    private static Func<bool> _is64BitProcess = static () => Environment.Is64BitProcess;

    /// <summary>
    /// “当前是否 64 位进程”判断的可替换边界，仅供无硬件测试模拟不满足条件的进程；
    /// 生产代码始终走 <see cref="Environment.Is64BitProcess"/> 默认实现。
    /// </summary>
    internal static Func<bool> Is64BitProcessProvider
    {
        get => _is64BitProcess;
        set => _is64BitProcess = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>恢复默认的进程位数判断（测试清理用）。</summary>
    internal static void ResetForTests() => _is64BitProcess = static () => Environment.Is64BitProcess;

    /// <summary>
    /// 要求当前进程为 64 位；不满足时抛 <see cref="PlatformNotSupportedException"/>，
    /// 文案明确项目仍为 AnyCPU、系该驱动运行条件不满足（而非驱动不存在）。
    /// </summary>
    /// <param name="driverDisplayName">驱动显示名（如“海康 GigE 相机”）。</param>
    internal static void Require64BitProcess(string driverDisplayName)
    {
        if (Is64BitProcessProvider())
        {
            return;
        }

        throw new PlatformNotSupportedException(
            $"驱动“{driverDisplayName}”需要 64 位进程运行，但当前进程为 32 位。" +
            "项目统一为 AnyCPU，不通过 csproj 固定架构；请以 64 位进程启动应用后再使用该驱动。");
    }
}
