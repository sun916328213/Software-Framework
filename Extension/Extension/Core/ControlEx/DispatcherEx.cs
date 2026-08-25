using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
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

            var dispatcher = GetUIDispatcher();
            if (dispatcher == null)
            {
                throw new InvalidOperationException("无法获取UI Dispatcher，可能应用程序未正确初始化");
            }
            
            dispatcher.Invoke(action);
        }

        /// <summary>
        /// 在WPF应用程序中跨线程同步更新UI时调用此方法
        /// </summary>
        /// <param name="action">要在UI线程上执行的操作</param>
        public static async Task UIInvokeAsync(this Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            var dispatcher = GetUIDispatcher();
            if (dispatcher == null)
            {
                throw new InvalidOperationException("无法获取UI Dispatcher，可能应用程序未正确初始化");
            }

            await dispatcher.InvokeAsync(action);
        }

        /// <summary>
        /// 在WPF应用程序中跨线程异步更新UI时调用此方法
        /// </summary>
        /// <param name="action">要在UI线程上执行的操作</param>
        public static void UIBeginInvoke(this Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            var dispatcher = GetUIDispatcher();
            if (dispatcher == null)
            {
                throw new InvalidOperationException("无法获取UI Dispatcher，可能应用程序未正确初始化");
            }

            dispatcher.BeginInvoke(action);
        }

        /// <summary>
        /// 在WPF应用程序中跨线程异步更新UI时调用此方法，可指定优先级
        /// </summary>
        /// <param name="action">要在UI线程上执行的操作</param>
        /// <param name="priority">调度优先级</param>
        public static void UIBeginInvoke(this Action action, DispatcherPriority priority)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            var dispatcher = GetUIDispatcher();
            if (dispatcher == null)
            {
                throw new InvalidOperationException("无法获取UI Dispatcher，可能应用程序未正确初始化");
            }

            dispatcher.BeginInvoke(priority, action);
        }

        /// <summary>
        /// 在WPF应用程序中跨线程同步更新UI并返回结果时调用此方法
        /// </summary>
        /// <typeparam name="T">返回值类型</typeparam>
        /// <param name="func">要在UI线程上执行的函数</param>
        /// <returns>函数执行结果</returns>
        public static T UIInvoke<T>(this Func<T> func)
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            var dispatcher = GetUIDispatcher();
            if (dispatcher == null)
            {
                throw new InvalidOperationException("无法获取UI Dispatcher，可能应用程序未正确初始化");
            }

            return dispatcher.Invoke(func);
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

            var dispatcher = GetUIDispatcher();
            if (dispatcher == null)
            {
                throw new InvalidOperationException("无法获取UI Dispatcher，可能应用程序未正确初始化");
            }

            var tcs = new TaskCompletionSource<T>();
            dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    tcs.SetResult(func());
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }));
            return tcs.Task;
        }

        /// <summary>
        /// 检查当前是否在UI线程上
        /// </summary>
        /// <returns>如果当前在UI线程上返回true，否则返回false</returns>
        public static bool IsUIThread()
        {
            var dispatcher = GetUIDispatcher();
            return dispatcher?.CheckAccess() ?? false;
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
        /// 在不同的线程中更新WinForms UI控件时调用此方法，以避免跨线程操作问题。
        /// </summary>
        /// <param name="control">需要更新的UI控件。</param>
        /// <param name="action">在线程安全的方式下执行的操作。</param>
        //public static void CrossThreadDisplay(this System.Windows.Forms.Control control, Action action)
        //{
        //    // 判断是否需要跨线程调用
        //    if (control.InvokeRequired)
        //    {
        //        // 开始跨线程调用，将操作委托给UI线程执行
        //        control.BeginInvoke(action);
        //    }
        //    else
        //    {
        //        // 如果不需要跨线程调用，直接执行操作
        //        action();
        //    }
        //}
    }
}