using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Extension.Core.ProtectionEx
{
    /// <summary>
    /// 反调试检测（托管 + Win32 + NT 多层检查）
    /// 注意：只能提高逆向门槛，无法 100% 防住内核级调试器，需配合混淆/加壳/AOT。
    /// </summary>
    public static class AntiDebugEx
    {
        [DllImport("kernel32.dll")]
        private static extern bool IsDebuggerPresent();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, ref bool isDebuggerPresent);

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

            try
            {
                if (IsDebuggerPresent())
                    return true;
            }
            catch { }

            try
            {
                bool remote = false;
                CheckRemoteDebuggerPresent(Process.GetCurrentProcess().Handle, ref remote);
                if (remote) return true;
            }
            catch { }

            try
            {
                int debugPort = 0;
                NtQueryInformationProcess(Process.GetCurrentProcess().Handle,
                    ProcessDebugPort, ref debugPort, sizeof(int), out _);
                if (debugPort != 0 && debugPort != -1) return true;
            }
            catch { }

            return false;
        }

        /// <summary>
        /// 计时陷阱：被单步调试时 Sleep(100) 实测耗时会远超阈值
        /// </summary>
        /// <param name="thresholdMs">判定阈值（默认 500ms）</param>
        public static bool IsTimingAnomaly(int thresholdMs = 500)
        {
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
    }
}
