using System;
using System.Collections.Generic;
using System.Threading;

namespace Extension.Core.ProtectionEx
{
    /// <summary>
    /// 威胁类型
    /// </summary>
    public enum ThreatType
    {
        Debugger,
        SuspiciousTool,
        Tampered,
    }

    /// <summary>
    /// 威胁处置方式
    /// </summary>
    public enum ThreatAction
    {
        /// <summary>仅触发事件，由业务方处理</summary>
        NotifyOnly,
        /// <summary>抛异常中断启动</summary>
        Throw,
        /// <summary>直接退出进程</summary>
        Exit,
    }

    /// <summary>
    /// 防护选项
    /// </summary>
    public sealed class ProtectionOptions
    {
        public bool CheckDebugger { get; set; } = true;
        public bool CheckSuspiciousTools { get; set; } = true;
        public bool CheckIntegrity { get; set; } = false;
        /// <summary>发布时计算好的入口程序集 SHA256（CheckIntegrity=true 时必填）</summary>
        public string? ExpectedEntryHash { get; set; }
        public ThreatAction Action { get; set; } = ThreatAction.Throw;
        /// <summary>后台轮询间隔（Zero = 只检查一次，不轮询）</summary>
        public TimeSpan PollInterval { get; set; } = TimeSpan.Zero;
    }

    /// <summary>
    /// 应用防护统一入口（程序启动最开头调用）
    /// 用法：AppProtectionEx.Protect(new ProtectionOptions { ... });
    /// </summary>
    public static class AppProtectionEx
    {
        private static Timer? _timer;

        /// <summary>检测到威胁时触发（NotifyOnly 模式下必须订阅处理）</summary>
        public static event Action<ThreatType, string>? OnThreatDetected;

        /// <summary>
        /// 执行一次全量检查，可选开启后台轮询
        /// </summary>
        public static void Protect(ProtectionOptions? options = null)
        {
            options ??= new ProtectionOptions();
            CheckOnce(options);

            if (options.PollInterval > TimeSpan.Zero)
            {
                _timer?.Dispose();
                _timer = new Timer(_ =>
                {
                    try { CheckOnce(options); } catch { /* 轮询中异常只上报不抛 */ }
                }, null, options.PollInterval, options.PollInterval);
            }
        }

        /// <summary>停止后台轮询</summary>
        public static void StopWatching()
        {
            _timer?.Dispose();
            _timer = null;
        }

        private static void CheckOnce(ProtectionOptions options)
        {
            if (options.CheckDebugger && AntiDebugEx.IsDebuggerAttached())
                Handle(ThreatType.Debugger, "检测到调试器。", options);

            if (options.CheckSuspiciousTools &&
                AntiToolProcessEx.IsSuspiciousToolRunning(out var hits))
                Handle(ThreatType.SuspiciousTool,
                    $"检测到逆向工具：{string.Join(",", (IEnumerable<string>)hits)}。", options);

            if (options.CheckIntegrity && !string.IsNullOrWhiteSpace(options.ExpectedEntryHash) &&
                !AntiTamperEx.VerifyEntryAssembly(options.ExpectedEntryHash!))
                Handle(ThreatType.Tampered, "程序完整性校验失败，文件可能被篡改。", options);
        }

        private static void Handle(ThreatType type, string message, ProtectionOptions options)
        {
            OnThreatDetected?.Invoke(type, message);
            switch (options.Action)
            {
                case ThreatAction.Throw:
                    throw new InvalidOperationException(message);
                case ThreatAction.Exit:
                    Environment.Exit(unchecked((int)0xDEAD0001));
                    break;
                case ThreatAction.NotifyOnly:
                default:
                    break;
            }
        }
    }
}
