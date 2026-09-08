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
            if (dispatcher == null || dispatcher.CheckAccess())
                action();
            else
                dispatcher.Invoke(action);
        }
    }
}
