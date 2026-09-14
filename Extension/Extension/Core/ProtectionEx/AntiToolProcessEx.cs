using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Extension.Core.ProtectionEx
{
    /// <summary>
    /// 反逆向工具进程检测（dnSpy / ILSpy / dotPeek / de4dot / x64dbg / WinDbg 等）
    /// 命中只代表“可疑环境”，由调用方决定是警告、退出还是上报。
    /// </summary>
    public static class AntiToolProcessEx
    {
        /// <summary>已知逆向/调试工具进程名（小写，不含 .exe）</summary>
        public static readonly string[] SuspiciousProcessNames =
        {
            "dnspy", "dnspy-x86", "dnspy.console",
            "ilspy", "ilspycmd",
            "dotpeek", "dotpeek64",
            "de4dot", "confuserex",
            "x64dbg", "x32dbg", "x96dbg",
            "ollydbg", "ollydbg64",
            "windbg", "windbgx",
            "processhacker", "processexplorer",
            "cheatengine", "cheatengine-x86_64",
            "reflector", "vboxservice", "vmtoolsd",
            "wireshark", "fiddler", "procmon", "procexp",
        };

        /// <summary>
        /// 当前是否有可疑工具进程在运行
        /// </summary>
        public static bool IsSuspiciousToolRunning(out IReadOnlyList<string> hitNames)
        {
            var hits = GetSuspiciousProcesses().Select(p =>
            {
                try { return p.ProcessName; }
                catch { return string.Empty; }
            })
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
            hitNames = hits;
            return hits.Count > 0;
        }

        /// <summary>
        /// 获取命中的可疑进程
        /// </summary>
        public static IReadOnlyList<Process> GetSuspiciousProcesses()
        {
            var all = new List<Process>();
            foreach (string name in SuspiciousProcessNames)
            {
                try
                {
                    all.AddRange(Process.GetProcessesByName(name));
                }
                catch { /* 权限不足或进程退出，跳过 */ }
            }
            return all;
        }

        /// <summary>
        /// 若检测到可疑工具则抛 InvalidOperationException
        /// </summary>
        public static void ThrowIfSuspiciousToolRunning()
        {
            if (IsSuspiciousToolRunning(out var hits))
                throw new InvalidOperationException($"检测到逆向工具：{string.Join(",", hits)}，程序拒绝运行。");
        }
    }
}
