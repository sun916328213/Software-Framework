// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：AxisMonitorSample、AxisPollingMonitor。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Diagnostics。导入不代表所有类型都会在运行时加载。
using System.Diagnostics;

namespace MotionControl.Runtime.Internal
{
    /// <summary>一次轴状态轮询的标准化结果。</summary>
    internal readonly record struct AxisMonitorSample(long RawStatus, bool Alarm);

    /// <summary>
    /// 统一管理轴状态轮询、取消、异常隔离和报警上升沿检测。
    /// 厂商适配器只负责读取状态和处理状态样本。
    /// </summary>
    internal sealed class AxisPollingMonitor : IDisposable
    {
        private readonly object _sync = new();
        private readonly int _intervalMs;
        private readonly Func<int, AxisMonitorSample?> _readSample;
        private readonly Action<int, long> _alarmRaised;
        private readonly Action<int, long>? _sampleObserved;
        private readonly Action<Exception>? _errorObserved;
        private CancellationTokenSource? _cancellation;

        /// <summary>
        /// 初始化 AxisPollingMonitor 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 nameof；具体条件和先后次序见方法体。
        /// </summary>
        public AxisPollingMonitor(
            int intervalMs,
            Func<int, AxisMonitorSample?> readSample,
            Action<int, long> alarmRaised,
            Action<int, long>? sampleObserved = null,
            Action<Exception>? errorObserved = null)
        {
            if (intervalMs <= 0)
                throw new ArgumentOutOfRangeException(nameof(intervalMs), "轮询间隔必须大于 0。");

            _intervalMs = intervalMs;
            _readSample = readSample ?? throw new ArgumentNullException(nameof(readSample));
            _alarmRaised = alarmRaised ?? throw new ArgumentNullException(nameof(alarmRaised));
            _sampleObserved = sampleObserved;
            _errorObserved = errorObserved;
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Start。
        /// 正文中的关键协作调用包括 nameof、Run、RunAsync；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        public void Start(int axisCount)
        {
            if (axisCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(axisCount), "监控轴数必须大于 0。");

            CancellationTokenSource cancellation;
            lock (_sync)
            {
                _cancellation?.Cancel();
                cancellation = new CancellationTokenSource();
                _cancellation = cancellation;
            }

            _ = Task.Run(() => RunAsync(axisCount, cancellation));
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Stop。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        public void Stop()
        {
            CancellationTokenSource? cancellation;
            lock (_sync)
            {
                cancellation = _cancellation;
                _cancellation = null;
            }
            cancellation?.Cancel();
        }

        /// <summary>
        /// 运行当前处理流程；对应入口为 RunAsync。
        /// 正文中的关键协作调用包括 _readSample、_alarmRaised、_errorObserved、TraceError、ConfigureAwait；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        private async Task RunAsync(int axisCount, CancellationTokenSource cancellation)
        {
            var token = cancellation.Token;
            var lastAlarm = new bool[axisCount];
            var unobservedErrorReported = false;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        for (var axis = 0; axis < axisCount; axis++)
                        {
                            if (token.IsCancellationRequested)
                                break;

                            var sample = _readSample(axis);
                            if (token.IsCancellationRequested)
                                break;
                            if (!sample.HasValue)
                                continue;

                            var value = sample.Value;
                            var alarmRisingEdge = value.Alarm && !lastAlarm[axis];
                            lastAlarm[axis] = value.Alarm;
                            if (alarmRisingEdge)
                                _alarmRaised(axis, value.RawStatus);
                            _sampleObserved?.Invoke(axis, value.RawStatus);
                        }
                        unobservedErrorReported = false;
                    }
                    catch (Exception ex)
                    {
                        if (_errorObserved is not null)
                            _errorObserved(ex);
                        else if (!unobservedErrorReported)
                        {
                            Trace.TraceError($"轴状态轮询异常：{ex}");
                            unobservedErrorReported = true;
                        }
                    }

                    await Task.Delay(_intervalMs, token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            finally
            {
                lock (_sync)
                {
                    if (ReferenceEquals(_cancellation, cancellation))
                        _cancellation = null;
                }
                cancellation.Dispose();
            }
        }

        /// <summary>
        /// 执行资源释放步骤；对应入口为 Dispose。
        /// 正文中的关键协作调用包括 Stop；具体条件和先后次序见方法体。
        /// </summary>
        public void Dispose() => Stop();
    }
}

