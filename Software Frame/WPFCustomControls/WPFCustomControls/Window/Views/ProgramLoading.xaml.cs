// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：ProgramLoading。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、System、System.Collections.Generic、System.Threading、System.Threading.Tasks、System.Windows。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace WPFCustomControls.Window.Views;

/// <summary>
/// ProgramLoading 的类型定义。WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
/// 继承或实现的契约：System.Windows.Window。具体约束按这些契约及各成员实现解释。
/// </summary>
public partial class ProgramLoading : System.Windows.Window
{
    public static readonly DependencyProperty ProgressValueProperty;
    public static readonly DependencyProperty StatusTextProperty;
    public static readonly DependencyProperty StepInfoProperty;

    public double ProgressValue
    {
        get => (double)GetValue(ProgressValueProperty);
        set => SetValue(ProgressValueProperty, value);
    }
    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }
    public string StepInfo
    {
        get => (string)GetValue(StepInfoProperty);
        set => SetValue(StepInfoProperty, value);
    }

    /// <summary>
    /// 初始化 ProgramLoading 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
    /// </summary>
    public ProgramLoading() { InitializeComponent(); }

    /// <summary>
    /// 初始化 ProgramLoading 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 Register；具体条件和先后次序见方法体。
    /// </summary>
    static ProgramLoading()
    {
        ProgressValueProperty = DependencyProperty.Register("ProgressValue", typeof(double), typeof(ProgramLoading), new FrameworkPropertyMetadata(0.0));
        StatusTextProperty = DependencyProperty.Register("StatusText", typeof(string), typeof(ProgramLoading), new FrameworkPropertyMetadata("正在初始化..."));
        StepInfoProperty = DependencyProperty.Register("StepInfo", typeof(string), typeof(ProgramLoading), new FrameworkPropertyMetadata(""));
    }

    /// <summary>
    /// 在独立 STA 线程上创建并显示本窗口，立即返回实例（旋转动画随即开始）。
    /// 窗口的旋转动画由独立线程自己的 Dispatcher 消息循环驱动，与调用方（主 UI 线程）
    /// 的同步重活完全隔离、互不抢占。显示后调用 <see cref="RunAsync"/> 执行初始化步骤。
    /// </summary>
    public static ProgramLoading StartOnOwnThread()
    {
        ProgramLoading? window = null;
        var ready = new ManualResetEventSlim(false);

        var thread = new Thread(() =>
        {
            window = new ProgramLoading();
            window.Show();
            ready.Set();
            Dispatcher.Run();
        })
        { IsBackground = true, Name = "LoadingWindowThread" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();

        return window!;
    }

    /// <summary>
    /// 在已显示的 loading 窗口上顺序执行初始化步骤，完成后关闭窗口并结束其线程。
    /// 返回的 Task 在窗口关闭后完成。步骤会投递到主 UI 线程执行，本窗口线程只维护动画。
    /// </summary>
    public Task RunAsync(List<(string Title, Func<Task> Work)> actions)
    {
        var closed = new TaskCompletionSource();
        Closed += (_, _) => closed.TrySetResult();
        Dispatcher.InvokeAsync(async () =>
        {
            try { await ShowAsync(actions); }
            finally { Dispatcher.InvokeShutdown(); }
        });
        return closed.Task;
    }

    /// <summary>
    /// 跨线程安全地关闭窗口并结束其 Dispatcher 消息循环（幂等）。用于异常兜底关闭。
    /// </summary>
    public void CloseOnOwnThread()
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            return;
        try
        {
            Dispatcher.Invoke(() =>
            {
                if (IsVisible) Close();
                Dispatcher.InvokeShutdown();
            });
        }
        catch
        {
            // 线程可能已退出，忽略
        }
    }

    /// <summary>
    /// 异步显示加载窗口，按顺序执行初始化操作并更新进度。
    /// 每个步骤由 (标题, 工作) 组成：进入步骤时更新状态文本，完成后推进进度。
    /// 本方法应在其所属线程（独立 STA 线程）上调用；窗口的 Show/更新/Close 自动运行在
    /// 该线程的 Dispatcher 上，而每步工作被投递到主 UI 线程执行。这样 loading 线程在等待
    /// 工作时让出消息循环，旋转动画保持流畅，不被主线程的重活拖慢。
    /// </summary>
    public async Task ShowAsync(List<(string Title, Func<Task> Work)> actions)
    {
        Show();

        try
        {
            for (int i = 0; i < actions.Count; i++)
            {
                StatusText = actions[i].Title;
                StepInfo = $"步骤 {i + 1} / {actions.Count}";
                ProgressValue = Math.Round((double)i / actions.Count * 100, 2);

                // 把工作投递到主 UI 线程执行，loading 线程 await 期间让出动画消息循环。
                await await Application.Current.Dispatcher.InvokeAsync(actions[i].Work);
            }

            ProgressValue = 100;
            StatusText = "初始化完成";
            await Task.Delay(1000);
        }
        finally
        {
            Close();
        }
    }
}
