using WPFControl.ProgramControl.Services;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Prism.Ioc;
using DataManager.Log.Definition; 

namespace WPFControl.Helper
{
    /// <summary>
    /// 异常处理帮助类，用于统一处理系统中的异常
    /// </summary>
    public   class TryCatchHelper
    {
        private static   LogInfoService _logInfoService;

        /// <summary>上报通道自身故障的 Trace 限频窗口（三十秒）。</summary>
        private const long DiagnosticIntervalTicks = TimeSpan.TicksPerSecond * 30;

        private static long _nextDiagnosticUtcTicks;
        private static int _suppressedDiagnosticCount;

        /// <summary>
        /// 初始化TryCatchHelper
        /// </summary>
        /// <param name="container">容器提供者</param>
        public   TryCatchHelper(IContainerProvider container)
        {
            _logInfoService = container.Resolve<LogInfoService>();
        }

        /// <summary>
        /// 执行可能抛出异常的操作，并记录异常信息
        /// 适用于Service层的异常处理
        /// </summary>
        /// <param name="action">要执行的操作</param>
        public static void TryExecute(  Action action, Action<Exception>  erraction = null)
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                erraction?.Invoke(ex );
                string message =  string.Join("\r\n", ex.Message, ex.StackTrace);
                _logInfoService?.AddRunInfo(message,  ELogLevel.SysError);
            }
        }

        /// <summary>
        /// 执行可能抛出异常的操作，但**不**把异常回报给 <see cref="LogInfoService.AddRunInfo"/>。
        /// 专供「日志上报路径自身」使用：本类的 <c>catch</c> 就是靠 AddRunInfo 上报的，
        /// 若 AddRunInfo 内部的失败又走回本类，就成了「抛 → 记 → 再抛」的无限递归，
        /// 实测直接 <c>StackOverflowException</c>（托管栈溢出不可捕获、无 dump、进程即死）。
        /// 因此这条路径上的故障只能降级为 <see cref="Trace"/> 诊断（三十秒限频）。
        /// </summary>
        /// <param name="action">要执行的操作</param>
        internal static void TryExecuteWithoutReport(Action action)
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                ReportFailure(ex);
            }
        }

        /// <summary>
        /// 以上报通道自身的故障写 Trace 诊断（三十秒限频 + 抑制计数），
        /// 绝不回调 <see cref="LogInfoService.AddRunInfo"/>，以免形成递归闭环。
        /// </summary>
        /// <param name="exception">上报过程中发生的异常</param>
        private static void ReportFailure(Exception exception)
        {
            var now = DateTime.UtcNow.Ticks;
            var next = Volatile.Read(ref _nextDiagnosticUtcTicks);
            if (now < next || Interlocked.CompareExchange(
                    ref _nextDiagnosticUtcTicks,
                    now + DiagnosticIntervalTicks,
                    next) != next)
            {
                Interlocked.Increment(ref _suppressedDiagnosticCount);
                return;
            }

            var suppressed = Interlocked.Exchange(ref _suppressedDiagnosticCount, 0);
            var suffix = suppressed == 0 ? string.Empty : $"；期间已抑制 {suppressed} 条重复错误";
            try
            {
                Trace.TraceError($"[TryCatch] 日志上报失败: {exception.Message}{suffix}");
            }
            catch
            {
                // 诊断通道自身的故障不得向上传播，否则又会绕回异常上报。
            }
        }

        /// <summary>
        /// 异步执行可能抛出异常的操作，并记录异常信息
        /// 适用于Service层的异步异常处理
        /// </summary>
        /// <param name="action">要执行的操作</param>
        public static async Task TryExecuteAsync(  Func<Task> action, Action<Exception> erraction = null)
        {
            try
            {
                if (action != null)
                    await action();
            }
            catch (Exception ex)
            {
                erraction?.Invoke(ex );
                string message = string.Join("\r\n", ex.Message, ex.StackTrace);
                _logInfoService?.AddRunInfo(message, ELogLevel.SysError);
            }
        }

        /// <summary>
        /// 执行可能抛出异常的函数，并返回结果或默认值
        /// 适用于Service层需要返回值的异常处理
        /// </summary>
        /// <typeparam name="T">返回值类型</typeparam>
        /// <param name="func">要执行的函数</param>
        /// <param name="defaultValue">发生异常时返回的默认值</param>
        /// <returns>函数执行结果或默认值</returns>
        public static T TryExecute<T>(  Func<T> func, T defaultValue = default(T))
        {
            try
            {
                return func != null ? func() : defaultValue;
            }
            catch (Exception ex)
            {
                string message = string.Join("\r\n", ex.Message, ex.StackTrace);
                _logInfoService?.AddRunInfo(message, ELogLevel.SysError);
                return defaultValue;
            }
        }

        /// <summary>
        /// 异步执行可能抛出异常的函数，并返回结果或默认值
        /// 适用于Service层需要异步返回值的异常处理
        /// </summary>
        /// <typeparam name="T">返回值类型</typeparam>
        /// <param name="func">要执行的异步函数</param>
        /// <param name="defaultValue">发生异常时返回的默认值</param>
        /// <returns>函数执行结果或默认值</returns>
        public static async Task<T> TryExecuteAsync<T>(  Func<Task<T>> func, T defaultValue = default(T))
        {
            try
            {
                return func != null ? await func() : defaultValue;
            }
            catch (Exception ex)
            {
                string message = string.Join("\r\n", ex.Message, ex.StackTrace);
                _logInfoService?.AddRunInfo(message, ELogLevel.SysError);
                return defaultValue;
            }
        }

        /// <summary>
        /// 执行可能抛出异常的操作，并返回执行结果
        /// 适用于ViewModel层需要知道操作结果的场景
        /// </summary>
        /// <param name="action">要执行的操作</param>
        /// <returns>操作是否成功执行</returns>
        public static bool TryExecuteWithResult(  Action action)
        {
            try
            {
                action?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                string message = string.Join("\r\n", ex.Message, ex.StackTrace);
                _logInfoService?.AddRunInfo(message, ELogLevel.SysError);
                return false;
            }
        }

        /// <summary>
        /// 执行可能抛出异常的操作，并返回执行结果
        /// </summary>
        /// <param name="action">要执行的操作</param>
        /// <param name="erraction">异常处理操作</param>
        /// <returns>操作是否成功执行</returns>
        public static bool TryExecuteWithResult(Action action,Action<Exception> erraction = null)
        {
            try
            {
                action?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                erraction.Invoke(ex);
                string message = string.Join("\r\n", ex.Message, ex.StackTrace);
                _logInfoService?.AddRunInfo(message, ELogLevel.SysError);
                return false;
            }
        }

        /// <summary>
        /// 异步执行可能抛出异常的操作，并返回执行结果
        /// 适用于ViewModel层需要知道异步操作结果的场景
        /// </summary>
        /// <param name="action">要执行的操作</param>
        /// <returns>操作是否成功执行</returns>
        public static async Task<bool> TryExecuteWithResultAsync(  Func<Task> action)
        {
            try
            {
                if (action != null)
                    await action();
                return true;
            }
            catch (Exception ex)
            {
                string message = string.Join("\r\n", ex.Message, ex.StackTrace);
                _logInfoService?.AddRunInfo(message, ELogLevel.SysError);
                return false;
            }
        }
    }
}