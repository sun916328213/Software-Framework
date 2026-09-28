using System.Diagnostics;

namespace MotionControl.Internal
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

        public void Dispose() => Stop();
    }
}
