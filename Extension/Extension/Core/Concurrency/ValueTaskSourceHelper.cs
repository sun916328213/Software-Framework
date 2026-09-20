using System.Threading.Tasks.Sources;

namespace Extension.Core.Concurrency
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    public   class ValueTaskSourceHelper<TResult> : IValueTaskSource<TResult>, IDisposable
    {
        /// <summary>
        /// 手动重置的任务源核心，用于复用异步等待状态
        /// </summary>
        private  ManualResetValueTaskSourceCore<TResult> _tcs;
        private CancellationTokenRegistration _registration;
        private CancellationTokenSource _timeoutCts;

        /// <summary>
        /// 是否销毁
        /// </summary>
        private bool _isDisposed;

        public ValueTaskSourceHelper()
        {
            _tcs = new System.Threading.Tasks.Sources.ManualResetValueTaskSourceCore<TResult> { RunContinuationsAsynchronously = true };
        }

        /// <summary>
        /// 获取结果
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public virtual TResult GetResult(short token) => _tcs.GetResult(token);
        /// <summary>
        /// 获取状态
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public virtual ValueTaskSourceStatus GetStatus(short token) => _tcs.GetStatus(token);

        public virtual void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
            => _tcs.OnCompleted(continuation, state, token, flags);


        /// <summary>
        /// 设置触发信号
        /// </summary>
        /// <param name="result"></param>
        public virtual bool SetSignal(TResult result)
        {
                if (_isDisposed)
                    return false;

                // 检查状态，确保可以设置结果
                if (_tcs.GetStatus(_tcs.Version) != ValueTaskSourceStatus.Pending)
                    return false;

                _tcs.SetResult(result);
                return true;
        }

        /// <summary>
        /// 设置异常
        /// </summary>
        public virtual bool SetException(Exception exception)
        {
            if (_isDisposed)
                return false;

            if (_tcs.GetStatus(_tcs.Version) != ValueTaskSourceStatus.Pending)
                return false;

            _tcs.SetException(exception);
            return true;
        }

        /// <summary>
        /// 等待触发信号
        /// </summary>
        /// <param name="timeout">超时</param>
        /// <param name="token">外部令牌</param>
        /// <returns></returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public virtual ValueTask<TResult> WaitSignal(int timeout = -1, CancellationToken token = default)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(GetType().FullName);

            // 清理之前的资源
            CleanupResources();

            //ManualResetValueTaskSourceCore重置,准备下一次操作
            _tcs.Reset();

            // 处理超时
            if (timeout > 0)
            {
                _timeoutCts = new CancellationTokenSource(timeout);

                _timeoutCts.Token.Register(() =>
                {
                    if (_tcs.GetStatus(_tcs.Version) == ValueTaskSourceStatus.Pending)
                    {
                        SetException(new TimeoutException("等待超时"));
                    }
                });
            }
            if (token != default)
            {
                // 只注册外部取消令牌
                _registration = token.Register(() =>
                {
                    if (_tcs.GetStatus(_tcs.Version) == ValueTaskSourceStatus.Pending)
                    {
                        SetException(new OperationCanceledException(token));
                    }
                });
            }
          

            //创建并返回一个新的 ValueTask<int> 实例
            return new ValueTask<TResult>(this, _tcs.Version);
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        private void CleanupResources()
        {
            _registration.Dispose();
            _timeoutCts?.Dispose();
            _timeoutCts = null;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // 释放托管资源
                    CleanupResources();
                }

                _isDisposed = true;
            }
        }
    }
}
