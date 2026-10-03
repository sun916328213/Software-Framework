// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：ValueTaskSourceHelper。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Threading.Tasks.Sources。导入不代表所有类型都会在运行时加载。
using System.Threading.Tasks.Sources;

namespace Extension.Core.Concurrency
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <remarks>
    /// ValueTaskSourceHelper 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
    /// 继承或实现的契约：IValueTaskSource&lt;TResult&gt;、IDisposable。具体约束按这些契约及各成员实现解释。
    /// </remarks>
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

        /// <summary>
        /// 初始化 ValueTaskSourceHelper 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
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

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnCompleted。
        /// 正文中的关键协作调用包括 OnCompleted；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 执行资源释放步骤；对应入口为 Dispose。
        /// 正文中的关键协作调用包括 CleanupResources；具体条件和先后次序见方法体。
        /// </summary>
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
