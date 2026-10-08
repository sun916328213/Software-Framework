// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：TaskEx。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.Linq、System.Text、System.Threading、System.Threading.Tasks。导入不代表所有类型都会在运行时加载。
 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Extension.Core.Concurrency
{
    /// <summary>
    /// 任务辅助工具类，提供任务取消和超时控制的扩展方法
    /// </summary>
    public static class TaskEx
    {
        /// <summary>
        /// 为有返回值的任务添加超时控制（支持取消令牌）
        /// </summary>
        /// <typeparam name="TResult">任务返回结果类型</typeparam>
        /// <param name="task">要执行的原始任务</param>
        /// <param name="timeout">超时时间（毫秒），-1 表示无限等待</param>
        /// <param name="token">可选的取消令牌（可与超时同时生效）</param>
        /// <returns>原始任务的执行结果</returns>
        public static async Task<TResult> WithTimeout<TResult>(this Task<TResult> task, int timeout, CancellationToken token = default)
        {
            if (timeout < -1)
                throw new ArgumentOutOfRangeException(nameof(timeout), "超时时间不能小于 -1");

            // 优化：当超时为-1且没有外部取消令牌时，直接返回原始任务
            if (timeout == -1 && !token.CanBeCanceled)
            {
                return await task;
            }

            // 优化：当超时为-1但有外部取消令牌时，只需要使用WithCancellation方法
            if (timeout == -1)
            {
                return await task.WithCancellation(token);
            }

            // 创建超时取消令牌源
            var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeout));

            // 关联外部取消令牌（超时和外部取消任一触发即取消）
             var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, token);

            try
            {
                // 使用现有取消机制等待任务完成
                return await task.WithCancellation(linkedCts.Token);
            }
            catch (OperationCanceledException ex)
            {
                // 区分超时和外部取消
                if (timeoutCts.IsCancellationRequested && !token.IsCancellationRequested)
                {
                    throw new TimeoutException($"任务在 {timeout}ms 后超时", ex);
                }
                throw; // 外部取消令牌触发，保持原始异常
            }
        }


        /// <summary>
        /// 为无返回值的任务添加超时控制（支持取消令牌）
        /// </summary>
        /// <param name="task">要执行的原始任务</param>
        /// <param name="timeout">超时时间（毫秒），-1 表示无限等待</param>
        /// <param name="token">可选的取消令牌（可与超时同时生效）</param>
        public static async Task WithTimeout(this Task task, int timeout, CancellationToken token = default)
        {
            if (timeout < -1)
                throw new ArgumentOutOfRangeException(nameof(timeout), "超时时间不能小于 -1");

            // 优化：当超时为-1且没有外部取消令牌时，直接返回原始任务
            if (timeout == -1 && !token.CanBeCanceled)
            {
                await task;
                return;
            }

            // 优化：当超时为-1但有外部取消令牌时，只需要使用WithCancellation方法
            if (timeout == -1)
            {
                await task.WithCancellation(token);
                return;
            }

            // 创建超时取消令牌源
             var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeout));

            // 关联外部取消令牌（超时和外部取消任一触发即取消）
             var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, token);

            try
            {
                // 使用现有取消机制等待任务完成
                await task.WithCancellation(linkedCts.Token);
            }
            catch (OperationCanceledException ex)
            {
                // 区分超时和外部取消
                if (timeoutCts.IsCancellationRequested && !token.IsCancellationRequested)
                {
                    throw new TimeoutException($"任务在 {timeout}ms 后超时", ex);
                }
                throw; // 外部取消令牌触发，保持原始异常
            }
        }

        /// <summary>
        /// 为有返回值的任务添加取消支持，当取消令牌触发时抛出取消异常
        /// </summary>
        /// <typeparam name="TResult">任务返回结果的类型</typeparam>
        /// <param name="task">需要执行的原始任务</param>
        /// <param name="token">取消令牌，用于外部触发任务取消</param>
        /// <returns>原始任务的执行结果</returns>
        public static async Task<TResult> WithCancellation<TResult>(this Task<TResult> task, CancellationToken token = default)
        {
            // 创建一个任务完成源，用于在取消令牌触发时发出信号
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            // 注册取消令牌回调：当令牌触发取消时，完成任务完成源
            using (token.Register(() => tcs.TrySetResult(true)))
            {
                // 如果取消已请求，立即抛出异常
                if (token.IsCancellationRequested)
                {
                    throw new OperationCanceledException(token);
                }

                // 等待原始任务或取消信号任务完成
                Task completedTask = await Task.WhenAny(task, tcs.Task);

                // 如果是取消信号任务先完成，抛出取消异常
                if (completedTask == tcs.Task)
                {
                    throw new OperationCanceledException(token);
                }

                // 返回原始任务的结果（此时原始任务已完成）
                return await task;
            }
        }


        /// <summary>
        /// 为无返回值的任务添加取消支持，当取消令牌触发时抛出取消异常
        /// </summary>
        /// <param name="task">需要执行的原始任务</param>
        /// <param name="token">取消令牌，用于外部触发任务取消</param>
        public static async Task WithCancellation(this Task task, CancellationToken token = default)
        {
            // 创建一个任务完成源，用于在取消令牌触发时发出信号
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            // 注册取消令牌回调：当令牌触发取消时，完成任务完成源
            using (token.Register(() => tcs.TrySetResult(true)))
            {
                // 如果取消已请求，立即抛出异常
                if (token.IsCancellationRequested)
                {
                    throw new OperationCanceledException(token);
                }

                // 等待原始任务或取消信号任务完成
                Task completedTask = await Task.WhenAny(task, tcs.Task);

                // 如果是取消信号任务先完成，抛出取消异常
                if (completedTask == tcs.Task)
                {
                    throw new OperationCanceledException(token);
                }

                // 等待原始任务完成（确保传播原始任务可能的异常）
                await task;
            }
        }


        /// <summary>
        /// Task长时间运行任务
        /// </summary>
        /// <param name="action">要执行的操作</param>
        /// <param name="pInterval">执行间隔（毫秒）</param>
        /// <param name="token">取消令牌</param>
        /// <returns>表示长时间运行任务的Task</returns>
        public static async Task LongTimeRun(this Action action, int pInterval = 500, CancellationToken token = default)
        {
            // 参数验证
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            if (pInterval < 0)
                throw new ArgumentOutOfRangeException(nameof(pInterval), "执行间隔不能小于 0");

            await Task.Factory.StartNew(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        action.Invoke();
                    }
                    catch (Exception ex)
                    {
                        // 优化：添加异常处理，防止单个异常终止整个任务
                        // 可以根据实际需求选择记录日志或重新抛出
                        Console.WriteLine($"LongTimeRun 执行异常: {ex.Message}");
                    }

                    // 优化：在Task.Delay中使用取消令牌
                    await Task.Delay(pInterval, token);
                }
            }, TaskCreationOptions.LongRunning);
        }


        /// <summary>
        /// Task 循环次数 执行任务
        /// </summary>
        /// <param name="action">要执行的操作</param>
        /// <param name="pInterval">执行间隔（毫秒）</param>
        /// <param name="pTimes">执行次数</param>
        /// <returns>表示循环执行任务的Task</returns>
        public static async Task TimesRun(this Action action, int pInterval, int pTimes)
        {
            // 参数验证
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            if (pInterval < 0)
                throw new ArgumentOutOfRangeException(nameof(pInterval), "执行间隔不能小于 0");
            if (pTimes < 0)
                throw new ArgumentOutOfRangeException(nameof(pTimes), "执行次数不能小于 0");

            await Task.Factory.StartNew(async () =>
            {
                // 优化：使用for循环代替while循环，更直观
                for (int i = 0; i < pTimes; i++)
                {
                    try
                    {
                        action.Invoke();
                    }
                    catch (Exception ex)
                    {
                        // 优化：添加异常处理，防止单个异常终止整个任务
                        Console.WriteLine($"TimesRun 执行异常: {ex.Message}");
                    }
                    
                    // 优化：最后一次执行后不需要延迟
                    if (i < pTimes - 1)
                    {
                        await Task.Delay(pInterval);
                    }
                }
            });
        }


        /// <summary>
        /// Thread线程执行任务
        /// </summary>
        /// <param name="action">要执行的操作</param>
        /// <param name="threadPriority">优先级</param>
        public static void ThreadRun( this Action action, ThreadPriority threadPriority = ThreadPriority.Normal)
        {
            Thread thread = new Thread(() =>
            {
                action.Invoke();
            });
            thread.IsBackground = true;
            thread.Start();
        }

    }
}
