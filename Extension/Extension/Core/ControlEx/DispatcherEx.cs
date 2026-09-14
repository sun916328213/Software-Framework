using System;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Extension.Core.ControlEx
{
    public static class DispatcherEx
    {
        /// <summary>
        /// 在WPF应用程序中跨线程同步更新UI时调用此方法
        /// </summary>
        /// <param name="action">要在UI线程上执行的操作</param>
        public static void UIInvoke(this Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            GetRequiredDispatcher().Invoke(action);
        }

        /// <summary>
        /// 在WPF应用程序中跨线程同步更新UI时调用此方法
        /// </summary>
        /// <param name="action">要在UI线程上执行的操作</param>
        public static async Task UIInvokeAsync(this Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            await GetRequiredDispatcher().InvokeAsync(action);
        }

        /// <summary>
        /// 在WPF应用程序中跨线程异步更新UI时调用此方法
        /// </summary>
        /// <param name="action">要在UI线程上执行的操作</param>
        public static void UIBeginInvoke(this Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            GetRequiredDispatcher().BeginInvoke(action);
        }

        /// <summary>
        /// 在WPF应用程序中跨线程异步更新UI并返回结果时调用此方法
        /// </summary>
        /// <typeparam name="T">返回值类型</typeparam>
        /// <param name="func">要在UI线程上执行的函数</param>
        /// <returns>表示异步操作的任务</returns>
        public static Task<T> UIBeginInvoke<T>(this Func<T> func)
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            return GetRequiredDispatcher().InvokeAsync(func).Task;
        }

        /// <summary>
        /// 安全版 UI 封送（与 <see cref="UIInvoke(Action)"/> 的区别：无 Application 上下文
        /// （如单测）、已是 UI 线程、或 Dispatcher 已关闭时直接执行，不抛；
        /// 封送被取消/失效时回退直行，保证关键路径不丢。
        /// </summary>
        /// <param name="action">要在UI线程上执行的操作</param>
        public static void UIInvokeSafe(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            var dispatcher = GetUIDispatcher();
            // 无上下文 / 本线程 / 已关闭：直接执行。
            if (dispatcher == null || dispatcher.CheckAccess()
                || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            {
                action();
                return;
            }
            try
            {
                dispatcher.Invoke(action);
            }
            catch (TaskCanceledException)
            {
                // Dispatcher 正关闭或调用被取消：回退直行。
                action();
            }
            catch (InvalidOperationException)
            {
                action();
            }
        }

        /// <summary>
        /// 获取UI线程的Dispatcher
        /// </summary>
        /// <returns>UI线程的Dispatcher实例</returns>
        private static Dispatcher GetUIDispatcher()
        {
            // 直接获取WPF Application的Dispatcher
            return System.Windows.Application.Current?.Dispatcher;
        }

        /// <summary>
        /// 获取UI线程的Dispatcher，拿不到时抛错（严格版方法共用）。
        /// </summary>
        private static Dispatcher GetRequiredDispatcher()
        {
            return GetUIDispatcher()
                ?? throw new InvalidOperationException("无法获取UI Dispatcher，可能应用程序未正确初始化");
        }
    }
}
