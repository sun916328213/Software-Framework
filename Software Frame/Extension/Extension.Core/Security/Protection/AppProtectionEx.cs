// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：ThreatType、ThreatAction、ProtectionOptions、AppProtectionEx。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.Diagnostics、System.Threading。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Extension.Security.Protection
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
                    // Timer 回调运行在线程池线程上，异常会直接终止进程，所以这里不能向外抛。
                    // 但也不能完全静默：否则"轮询防护一直在跑"会是假象，本轮失败无人知晓。
                    try
                    {
                        CheckOnce(options);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[AppProtectionEx] 本轮防护检查异常，已跳过：{ex}");
                    }
                }, null, options.PollInterval, options.PollInterval);
            }
        }

        /// <summary>停止后台轮询</summary>
        public static void StopWatching()
        {
            _timer?.Dispose();
            _timer = null;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 CheckOnce。
        /// 正文中的关键协作调用包括 IsDebuggerAttached、Handle、IsSuspiciousToolRunning、Join、IsNullOrWhiteSpace；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Handle。
        /// 正文中的关键协作调用包括 Exit；具体条件和先后次序见方法体。
        /// </summary>
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
