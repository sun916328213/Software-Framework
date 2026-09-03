using CommunicationNew.Core.EventArgs;
using CommunicationNew.Core.Interfaces;
using CommunicationNew.Core.Models;

namespace CommunicationNew.Core.Services
{
    /// <summary>
    /// 数据点自动轮询读取器 —— 后台按固定间隔读取所有可读数据点，
    /// 值变化时触发 DataPointChanged 事件。与协议无关，基于统一 ICommunicationDevice 接口。
    /// </summary>
    public class DataPointAutoReader : IAutoReader
    {
        private CancellationTokenSource? _cts;
        private Task? _loopTask;
        private readonly ICommunicationDevice _device;
        private readonly DataPointCollection _collection;
        private readonly object _lock = new();

        public bool IsRunning { get; private set; }
        public int IntervalMs { get; set; } = 1000;

        public event EventHandler<DataPointChangedEventArgs>? DataPointChanged;

        /// <summary>读取失败回调（参数：数据点名，异常）</summary>
        public event EventHandler<(string name, Exception ex)>? ReadFailed;

        public DataPointAutoReader(ICommunicationDevice device, DataPointCollection collection)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _collection = collection ?? throw new ArgumentNullException(nameof(collection));
        }

        public void Start()
        {
            lock (_lock)
            {
                if (IsRunning) return;
                _cts = new CancellationTokenSource();
                IsRunning = true;
                _loopTask = RunLoopAsync(_cts.Token);
            }
        }

        /// <summary>
        /// 兼容旧同步调用的停止入口。
        /// 新代码应使用 <see cref="StopAsync"/>，避免阻塞调用线程。
        /// </summary>
        public void Stop()
        {
            StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>取消轮询，并在当前读取与循环退出后返回。</summary>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            Task? loopTask;
            CancellationTokenSource? cts;
            lock (_lock)
            {
                loopTask = _loopTask;
                cts = _cts;
                if (loopTask is null || cts is null)
                {
                    IsRunning = false;
                    return;
                }

                cts.Cancel();
            }

            try
            {
                await loopTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // 轮询自身的停止取消属于正常生命周期。
            }
            finally
            {
                lock (_lock)
                {
                    if (ReferenceEquals(_loopTask, loopTask))
                    {
                        _loopTask = null;
                        _cts = null;
                        IsRunning = false;
                    }
                }

                cts.Dispose();
            }
        }

        private async Task RunLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await PollOnceAsync(ct).ConfigureAwait(false);
                    await Task.Delay(IntervalMs, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    ReadFailed?.Invoke(this, ("<poll-loop>", ex));
                }
            }

            lock (_lock)
            {
                IsRunning = false;
            }
        }

        /// <summary>执行一轮轮询（可复用给手动刷新）</summary>
        public async Task PollOnceAsync(CancellationToken ct = default)
        {
            foreach (var point in _collection)
            {
                if (ct.IsCancellationRequested) break;
                if (!point.Readable) continue;
                await PollPointAsync(point, ct).ConfigureAwait(false);
            }
        }

        private async Task PollPointAsync(IDataPoint point, CancellationToken ct)
        {
            try
            {
                var old = point.Value;
                var bytes = await _device.ReadAsync(point.Address, point.Length, ct).ConfigureAwait(false);
                point.RawValue = bytes;
                point.Quality = DataQuality.Good;
                var newValue = point.Value;

                if (!Equals(old, newValue))
                    DataPointChanged?.Invoke(this, new DataPointChangedEventArgs(point, old, newValue));
            }
            catch (Exception ex)
            {
                point.Quality = DataQuality.Bad;
                ReadFailed?.Invoke(this, (point.Name, ex));
            }
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }

        /// <summary>等待轮询结束后释放取消源。</summary>
        public async ValueTask DisposeAsync()
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }
    }
}
