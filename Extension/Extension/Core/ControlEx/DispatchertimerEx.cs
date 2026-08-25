using System.Windows.Threading;

namespace Extension.Core.ControlEx
{
    public static class DispatchertimerEx 
    {
        /// <summary>
        /// 跨线程定时器
        /// </summary>
        /// <param name="action">要执行的操作</param>
        /// <param name="pInterval">间隔时间(毫秒)</param>
        /// <param name="priority">调度优先级</param>
        /// <param name="isReentrant">是否允许重入(默认false)</param>
        /// <returns>DispatcherTimer实例</returns>
        public static DispatcherTimer DispatcherTimer(this Action action, int pInterval, DispatcherPriority priority = DispatcherPriority.Render, bool isReentrant = false)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (pInterval <= 0)
                throw new ArgumentOutOfRangeException(nameof(pInterval), "间隔时间必须大于0");

            DispatcherTimer _timer = new DispatcherTimer(priority)
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

            _timer.Start();
            return _timer; // 返回定时器实例，方便外部控制
        }
   
    
    }
}