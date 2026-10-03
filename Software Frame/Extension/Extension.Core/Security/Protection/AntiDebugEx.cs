// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：AntiDebugEx。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.ComponentModel、System.Diagnostics、System.Runtime.InteropServices。导入不代表所有类型都会在运行时加载。
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Extension.Security.Protection
{
    /// <summary>
    /// 反调试检测（托管 + Win32 + NT 多层检查）
    /// 注意：只能提高逆向门槛，无法 100% 防住内核级调试器，需配合混淆/加壳/AOT。
    /// </summary>
    public static class AntiDebugEx
    {
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 IsDebuggerPresent。
        /// </summary>
        [DllImport("kernel32.dll")]
        private static extern bool IsDebuggerPresent();

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 CheckRemoteDebuggerPresent。
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, ref bool isDebuggerPresent);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 NtQueryInformationProcess。
        /// </summary>
        [DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(
            IntPtr processHandle, int processInformationClass,
            ref int processInformation, int processInformationLength, out int returnLength);

        private const int ProcessDebugPort = 7;

        /// <summary>
        /// 是否正被调试（任一检查命中即返回 true）
        /// </summary>
        public static bool IsDebuggerAttached()
        {
            if (Debugger.IsAttached || Debugger.IsLogging())
                return true;

            // 以下三个 Win32/NT 探测都为"尽力而为"：探测 API 不可用时（沙箱、裁剪运行时、
            // ntdll 未导出该函数、权限不足）不应让检测逻辑本身崩溃，否则会误杀正常环境。
            // 因此这里只吞"环境不支持"这一类异常，且必须留下诊断痕迹，避免检测悄悄失效却无人知晓。

            try
            {
                if (IsDebuggerPresent())
                    return true;
            }
            catch (Exception ex) when (IsProbeUnsupported(ex))
            {
                Debug.WriteLine($"[AntiDebugEx] IsDebuggerPresent 探测不可用：{ex.Message}");
            }

            try
            {
                bool remote = false;
                CheckRemoteDebuggerPresent(Process.GetCurrentProcess().Handle, ref remote);
                if (remote) return true;
            }
            catch (Exception ex) when (IsProbeUnsupported(ex))
            {
                Debug.WriteLine($"[AntiDebugEx] CheckRemoteDebuggerPresent 探测不可用：{ex.Message}");
            }

            try
            {
                int debugPort = 0;
                NtQueryInformationProcess(Process.GetCurrentProcess().Handle,
                    ProcessDebugPort, ref debugPort, sizeof(int), out _);
                if (debugPort != 0 && debugPort != -1) return true;
            }
            catch (Exception ex) when (IsProbeUnsupported(ex))
            {
                Debug.WriteLine($"[AntiDebugEx] NtQueryInformationProcess 探测不可用：{ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// 计时陷阱：被单步调试时 Sleep(100) 实测耗时会远超阈值
        /// </summary>
        /// <param name="thresholdMs">判定阈值（默认 500ms），必须为正数</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="thresholdMs"/> 小于等于 0。修前传 0 会**恒返回 true**（Sleep(100) 必然 &gt; 0），
        /// 调用方若从配置读到"未配置"的 0，反调试会立刻误报，且**无法与真的被调试区分**。
        /// </exception>
        /// <remarks>
        /// ⛔ 序 37（2026-09-18 修）：补下限保护。取值口径与同模块的"参数错误一律抛出、不静默吞掉"一致
        /// （见 <see cref="IsProbeUnsupported"/> 的注释：其余异常继续抛出，避免掩盖真实缺陷）。
        /// ⚠️ 本方法固定 <c>Sleep(100)</c> ⇒ 只要 <paramref name="thresholdMs"/> ≤ 100 就**恒为 true**。
        /// 本次**只拦 ≤ 0**（"未配置"哨兵值）；"100 以下同样无意义"这一隐患**已记录、未改**
        /// （改它要引入"最小阈值"策略，属于行为变更，需单独授权）。
        /// </remarks>
        public static bool IsTimingAnomaly(int thresholdMs = 500)
        {
            if (thresholdMs <= 0)
                throw new ArgumentOutOfRangeException(nameof(thresholdMs), thresholdMs,
                    "判定阈值必须为正数（单位 ms）：传 0 或负数会让本方法恒返回 true，把正常环境误判为被调试。");

            var sw = Stopwatch.StartNew();
            System.Threading.Thread.Sleep(100);
            sw.Stop();
            return sw.ElapsedMilliseconds > thresholdMs;
        }

        /// <summary>
        /// 若检测到调试器则抛 InvalidOperationException（建议在程序入口调用）
        /// </summary>
        public static void ThrowIfDebuggerAttached()
        {
            if (IsDebuggerAttached())
                throw new InvalidOperationException("检测到调试器，程序拒绝运行。");
        }

        /// <summary>
        /// 判断异常是否属于"当前环境不支持该探测 API"，是则可以安全跳过该探测；
        /// 其他异常（如空引用、参数错误）一律继续抛出，避免掩盖真实缺陷。
        /// </summary>
        private static bool IsProbeUnsupported(Exception ex) =>
            ex is DllNotFoundException
                or EntryPointNotFoundException
                or Win32Exception
                or PlatformNotSupportedException
                or NotSupportedException;
    }
}
