using System;

namespace DataManager.Workbench.Infrastructure
{
    /// <summary>
    /// P2-5：UI 线程封送。ViewModel 在 Task.Run 后台线程中更新绑定属性/集合时，
    /// 必须回到 Dispatcher，否则 WPF 抛跨线程访问异常；无 Application 上下文（如单测）时直接执行。
    /// </summary>
    public static class UiThread
    {
        public static void Invoke(Action action)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            // 无上下文 / 本线程 / 已关闭：直接执行（单测无 Dispatcher；XAML 冒烟关闭后并行测试仍可能命中）。
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
                // Dispatcher 正关闭或调用被取消：回退直行，保证 Log 等关键路径不丢。
                action();
            }
            catch (InvalidOperationException)
            {
                action();
            }
        }
    }
}
