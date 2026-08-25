using Extension.Core.TaskEx;
 
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Extension.Helper.TaskHelper
{    /// <summary>
     /// 事件参数类，包含指令上下文信息
     /// </summary>
    public class CEventArgs : EventArgs
    {
        /// <summary>
        /// 指令名称（如 "S1F17"）
        /// </summary>
        public string Command { get; }

        /// <summary>
        /// 额外数据对象（可以存储任意类型的数据）
        /// </summary>
        public object Data { get; }

        /// <summary>
        /// 初始化事件参数类
        /// </summary>
        /// <param name="command">指令名称</param>
        public CEventArgs(string command)
        {
            Command = command;
            Data = null;
        }

        /// <summary>
        /// 初始化事件参数类（带数据）
        /// </summary>
        /// <param name="command">指令名称</param>
        /// <param name="data">额外数据对象</param>
        public CEventArgs(string command, object data)
        {
            Command = command;
            Data = data;
        }
    }
    /// <summary>
    /// 通用事件管理助手，支持动态注册/取消注册特定指令的事件
    /// </summary>
    public class EventHelper
    {
        // 线程安全字典：指令名称 -> 事件委托（支持多播）
        private static readonly ConcurrentDictionary<string, Action<CEventArgs>> _Events
            = new ConcurrentDictionary<string, Action<CEventArgs>>();

        /// <summary>
        /// 注册指定指令的接收事件处理程序
        /// </summary>
        /// <param name="command">指令名称（如 "S1F17"）</param>
        /// <param name="handler">事件处理委托</param>
        /// <exception cref="ArgumentException">指令名称为空时抛出</exception>
        /// <exception cref="ArgumentNullException">处理委托为空时抛出</exception>
        public static void RegisterEvent(string command, Action<CEventArgs> handler)
        {
            if (string.IsNullOrEmpty(command))
                throw new ArgumentException("指令名称不能为空", nameof(command));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            _Events.AddOrUpdate(command, handler, (_, existing) => existing + handler);//多番委托
        }

        /// <summary>
        /// 取消注册指定指令的接收事件处理程序
        /// </summary>
        /// <param name="command">指令名称（如 "S1F17"）</param>
        /// <param name="handler">要取消的事件处理委托</param>
        /// <exception cref="ArgumentException">指令名称为空时抛出</exception>
        /// <exception cref="ArgumentNullException">处理委托为空时抛出</exception>
        public static void UnregisterEvent(string command, Action<CEventArgs> handler)
        {
            if (string.IsNullOrEmpty(command))
                throw new ArgumentException("指令名称不能为空", nameof(command));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (_Events.TryGetValue(command, out var existing))
            {
                var newHandler = existing - handler;
                if (newHandler == null)
                    _Events.TryRemove(command, out _);
                else
                    _Events[command] = newHandler;
            }
        }

        /// <summary>
        /// 清空指定指令的所有事件处理程序
        /// </summary>
        /// <param name="command">指令名称</param>
        public static void ClearEvents(string command)
        {
            if (string.IsNullOrEmpty(command))
                throw new ArgumentException("指令名称不能为空", nameof(command));
            
            _Events.TryRemove(command, out _);
        }

        /// <summary>
        /// 清空所有指令的事件处理程序
        /// </summary>
        public static void ClearAllEvents()
        {
            _Events.Clear();
        }

        /// <summary>
        /// 触发指定指令的事件（线程安全）
        /// </summary>
        /// <param name="command">指令名称（如 "S1F17"）</param>
        public static void TriggerEvent(string command)
        {
            if (string.IsNullOrEmpty(command))
                throw new ArgumentException("指令名称不能为空", nameof(command));
          
            if (_Events.TryGetValue(command, out var handlers))
            {
                // 复制委托到局部变量，避免调用过程中被修改
                var tempHandlers = Interlocked.CompareExchange(ref handlers, null, null);
                if (tempHandlers != null)
                {
                    // 使用泛型类型的结果创建事件参数
                    var eventArgs = new CEventArgs(command);

                    // 遍历多播委托的所有订阅者，逐个调用并捕获异常
                    foreach (var handler in tempHandlers.GetInvocationList().Cast<Action<CEventArgs>>())
                    {
                        try
                        {
                            handler(eventArgs);
                        }
                        catch (Exception ex)
                        {
                            // 可在此处记录异常日志（示例：使用日志组件）
                            // Logger.LogError(ex, $"事件处理异常，指令：{command}");
                            throw ex;
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// 触发指定指令的事件（带数据参数，线程安全）
        /// </summary>
        /// <param name="command">指令名称（如 "S1F17"）</param>
        /// <param name="data">要传递的数据对象</param>
        public static void TriggerEvent(string command, object data)
        {
            if (string.IsNullOrEmpty(command))
                throw new ArgumentException("指令名称不能为空", nameof(command));
          
            if (_Events.TryGetValue(command, out var handlers))
            {
                // 复制委托到局部变量，避免调用过程中被修改
                var tempHandlers = Interlocked.CompareExchange(ref handlers, null, null);
                if (tempHandlers != null)
                {
                    // 使用指令名称和数据创建事件参数
                    var eventArgs = new CEventArgs(command, data);

                    // 遍历多播委托的所有订阅者，逐个调用并捕获异常
                    foreach (var handler in tempHandlers.GetInvocationList().Cast<Action<CEventArgs>>())
                    {
                        try
                        {
                            handler(eventArgs);
                        }
                        catch (Exception ex)
                        {
                            // 可在此处记录异常日志（示例：使用日志组件）
                            // Logger.LogError(ex, $"事件处理异常，指令：{command}");
                            throw ex;
                        }
                    }
                }
            }
        }


        /// <summary>
        /// 通用事件等待扩展方法，支持超时和取消令牌
        /// </summary>
        /// <param name="register">注册事件处理的委托</param>
        /// <param name="unregister">取消注册事件处理的委托</param>
        /// <param name="timeout">超时时间（毫秒），-1表示无限等待</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>事件参数</returns>
        public static async Task<CEventArgs> WaitForEventAsync(Action<Action<CEventArgs>> register, Action<Action<CEventArgs>> unregister, int timeout = 10000, CancellationToken cancellationToken = default)
        {
            if (register == null) throw new ArgumentNullException(nameof(register));
            if (unregister == null) throw new ArgumentNullException(nameof(unregister));
            if (timeout < -1) throw new ArgumentOutOfRangeException(nameof(timeout), "超时时间不能小于-1");

            var tcs = new TaskCompletionSource<CEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            Action<CEventArgs> handler = null;
            CancellationTokenRegistration cancellationRegistration = default;

            try
            {
                // 定义一个事件处理程序，用于处理特定的事件或消息
                handler = args =>
                {
                    // 尝试将任务完成的结果设置为接收到的参数
                    if (tcs.TrySetResult(args))
                    {
                        // 如果结果设置成功，则取消注册该处理程序，以避免内存泄漏
                        unregister(handler);
                    }
                };

                // 检查是否可以取消操作
                if (cancellationToken.CanBeCanceled)
                {
                    // 注册一个取消操作的回调，当操作被取消时执行
                    cancellationRegistration = cancellationToken.Register(() =>
                    {
                        // 尝试将任务的状态设置为已取消
                        if (tcs.TrySetCanceled(cancellationToken))
                        {
                            // 如果取消操作成功，则取消注册该处理程序，以避免内存泄漏
                            unregister(handler);
                        }
                    });
                }

                // 注册事件处理程序，以便开始监听特定的事件或消息
                register(handler);

                // 使用TaskHelper的超时扩展方法
                return await tcs.Task.WithTimeout(timeout, cancellationToken);
            }
            finally
            {
                cancellationRegistration.Dispose();
            }
        }

        /// <summary>
        /// 简化版事件等待方法（指定指令名称、超时时间和取消令牌）
        /// </summary>
        /// <param name="command">要等待的指令名称</param>
        /// <param name="timeout">超时时间（毫秒），-1表示无限等待</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>事件参数</returns>
        public static async Task<CEventArgs> WaitForEventAsync(string command, int timeout = 100000, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(command))
                throw new ArgumentException("指令名称不能为空", nameof(command));

            return await WaitForEventAsync(
                h => RegisterEvent(command, h),
                h => UnregisterEvent(command, h),
                timeout,
                cancellationToken
            );
        }
    }
}