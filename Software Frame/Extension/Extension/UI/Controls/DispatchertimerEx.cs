// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：DispatcherTimerEx。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections.Concurrent、System.Windows.Threading。导入不代表所有类型都会在运行时加载。
using System.Collections.Concurrent;
using System.Windows.Threading;

namespace Extension.UI.Controls
{
    /// <summary>
    /// 跨线程定时器扩展。
    /// </summary>
    public static class DispatcherTimerEx
    {
        /// <summary>命名定时器注册表：同 key 重复调用时替换旧定时器，保证幂等。</summary>
        private static readonly ConcurrentDictionary<string, DispatcherTimer> NamedTimers = new();

        /// <summary>
        /// 跨线程定时器。
        /// 默认绑定主 UI 线程的 Dispatcher（Application.Current.Dispatcher）：
        /// 从后台/脚本线程调用时，CurrentDispatcher 是一个没有消息循环的临时 Dispatcher，
        /// 定时器将永远不会触发——因此本扩展默认改绑主 UI 线程。
        /// 确需绑定当前线程时，显式传入 dispatcher 参数。
        /// </summary>
        /// <param name="action">要执行的操作</param>
        /// <param name="pInterval">间隔时间(毫秒)</param>
        /// <param name="key">
        /// 命名键（推荐脚本场景使用）：同 key 重复调用会<b>替换</b>旧定时器而不是叠加，
        /// 保证脚本重复运行/循环运行时幂等，不会累积多个定时器。null = 由调用方自行管理实例。
        /// </param>
        /// <param name="priority">调度优先级</param>
        /// <param name="isReentrant">是否允许重入(默认false)</param>
        /// <param name="dispatcher">目标 Dispatcher；null = 主 UI 线程</param>

        /// <returns>DispatcherTimer实例</returns>
        public static DispatcherTimer DispatcherTimer(this Action action, int pInterval, string? key = null, DispatcherPriority priority = DispatcherPriority.Render, bool isReentrant = false, System.Windows.Threading.Dispatcher? dispatcher = null)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (pInterval <= 0)
                throw new ArgumentOutOfRangeException(nameof(pInterval), "间隔时间必须大于0");

            // 默认绑定主 UI 线程：后台线程的 CurrentDispatcher 无消息循环，定时器不会触发
            var targetDispatcher = dispatcher ?? System.Windows.Application.Current?.Dispatcher
                                                ?? Dispatcher.CurrentDispatcher;

            DispatcherTimer _timer = new DispatcherTimer(priority, targetDispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(pInterval) // 精确到毫秒
            };

            if (!isReentrant) //不允许重入
            {
                // 防重入机制，确保当前异步任务完成后再触发下一次执行
                bool _isProcessing = false;
                _timer.Tick += (s, e) =>
                {
                    if (_isProcessing)
                        return;

                    _isProcessing = true;
                    try
                    {
                        action();
                    }
                    finally
                    {
                        _isProcessing = false;
                    }
                };
            }
            else
            {
                _timer.Tick += (s, e) => action();
            }

            if (key != null)
            {
                // 命名幂等：同 key 先停旧定时器再登记新的，防止重复调用叠加
                if (NamedTimers.TryRemove(key, out var old))
                    old.Stop();
                NamedTimers[key] = _timer;
            }

            _timer.Start();
            return _timer; // 返回定时器实例，方便外部控制
        }

        /// <summary>停止并移除指定命名定时器。返回是否存在并被停止。</summary>
        public static bool StopTimer(string key)
        {
            if (!NamedTimers.TryRemove(key, out var timer))
                return false;
            timer.Stop();
            return true;
        }

        /// <summary>停止并清空全部命名定时器（如关机流程收尾时调用）。</summary>
        public static void StopAllTimers()
        {
            foreach (var kv in NamedTimers)
            {
                if (NamedTimers.TryRemove(kv.Key, out var timer))
                    timer.Stop();
            }
        }

        /// <summary>当前存活的命名定时器数量（诊断用）。</summary>
        public static int NamedTimerCount => NamedTimers.Count;
    }
}
