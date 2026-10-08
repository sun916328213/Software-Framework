// 【中文阅读说明】
// 工作台测试共用支持：把生产关闭路径（Window.Close → Closing 排空 → Closed → Application.Exit → UI 线程退出）
// 串成一次可断言的关闭，供 Communication / DataManager / Device / MotionControl / ScriptEditor /
// SecsGem / Vision 七个 Workbench.Tests 夹具共用（各 csproj 以源码链接引入，见 Build/PROVENANCE.md）。
// ⚠️ 本文件是 2026-10-08 **重建**（原文件丢失，按七个夹具的实际用法推断），不是逐字节恢复。
using System;
using System.Threading;
using System.Windows;

namespace Workbench.TestSupport
{
    /// <summary>
    /// 测试宿主用的 Workbench 生命周期桥。
    /// 生产侧 <c>WorkbenchLifetime</c> 由各 Workbench 的 <c>App.OnInitialized</c> 自行装配；
    /// 这里只负责在 UI 线程上登记主窗、发起真实关闭，并等待 UI 线程真正退出——
    /// 从而让夹具断言的是「真实 Window.Close/Closed/Exit/Join」，而不是复用旧记录。
    /// </summary>
    public sealed class WorkbenchTestLifetime
    {
        /// <summary>等待 UI 线程退出的上限；生产关闭排空默认 30 秒，这里留出余量。</summary>
        private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(60);

        private readonly Application _app;
        private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Exception? _threadFailure;
        private Window? _mainWindow;

        /// <summary>
        /// 初始化 WorkbenchTestLifetime 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 必须在 UI 线程（夹具线程体内、<c>new App()</c> 之后）构造。
        /// </summary>
        public WorkbenchTestLifetime(Application app)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
            _app.Exit += (_, _) => _exited.TrySetResult();
        }

        /// <summary>
        /// 在 UI 线程登记主窗（夹具在 <c>Startup</c> 后经 <c>ApplicationIdle</c> 调用）。
        /// 主窗触发 <c>Closed</c> 即视为「已关闭」证据。
        /// </summary>
        /// <exception cref="InvalidOperationException">应用未创建主窗口。</exception>
        public void ObserveMainWindow()
        {
            var window = _app.MainWindow
                ?? throw new InvalidOperationException("工作台未创建主窗口（Application.MainWindow 为 null）。");
            if (ReferenceEquals(_mainWindow, window))
                return;

            _mainWindow = window;
            window.Closed += (_, _) => _closed.TrySetResult();
        }

        /// <summary>记录 UI 线程抛出的启动/运行异常，供 <see cref="CloseAndWait(Thread)"/> 统一上报。</summary>
        public void RecordThreadFailure(Exception error) => _threadFailure ??= error;

        /// <summary>
        /// 经真实关闭路径关掉主窗并等待 UI 线程退出；关闭失败、超时、线程异常都会抛出。
        /// </summary>
        /// <param name="thread">承载该 WPF 应用的 UI 线程（夹具自己创建并 Start 的那个）。</param>
        /// <exception cref="InvalidOperationException">未登记主窗、UI 线程异常结束、Closed 或 Exit 未触发。</exception>
        /// <exception cref="TimeoutException">UI 线程在 <see cref="ShutdownTimeout"/> 内未退出。</exception>
        public void CloseAndWait(Thread thread) => CloseAndWait(thread, beforeClose: null);

        /// <summary>
        /// 带「关闭前场景」的关闭：先在 UI 线程执行 <paramref name="beforeClose"/>
        /// （用于把界面摆成「正在运行」状态，再验证关闭是否真的排空），随后走真实关闭路径。
        /// 场景自身抛错时仍会继续关闭（避免 UI 线程不退导致夹具挂死），错误在关闭完成后重新抛出。
        /// </summary>
        /// <param name="thread">承载该 WPF 应用的 UI 线程。</param>
        /// <param name="beforeClose">在 UI 线程、关闭之前执行的场景；可为 null。</param>
        /// <exception cref="InvalidOperationException">未登记主窗、UI 线程异常结束、场景失败、Closed 或 Exit 未触发。</exception>
        /// <exception cref="TimeoutException">UI 线程在 <see cref="ShutdownTimeout"/> 内未退出。</exception>
        public void CloseAndWait(Thread thread, Action<Application>? beforeClose)
        {
            ArgumentNullException.ThrowIfNull(thread);

            var window = _mainWindow ?? throw new InvalidOperationException("工作台未初始化退出处理。");
            var dispatcher = window.Dispatcher;
            Exception? scenarioFailure = null;

            // 走生产关闭路径：Close → Closing（生产 WorkbenchLifetime 排空资源）→ Closed → Exit。
            if (!dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
            {
                dispatcher.Invoke(() =>
                {
                    try { beforeClose?.Invoke(_app); }
                    catch (Exception error) { scenarioFailure = error; }
                    finally { window.Close(); }
                });
            }

            if (!thread.Join(ShutdownTimeout))
                throw new TimeoutException($"工作台 UI 线程在 {ShutdownTimeout.TotalSeconds:0} 秒内未退出。");

            if (_threadFailure is not null)
                throw new InvalidOperationException("工作台 UI 线程以异常结束。", _threadFailure);

            if (scenarioFailure is not null)
                throw new InvalidOperationException("关闭前的运行场景失败。", scenarioFailure);

            if (!_closed.Task.IsCompleted)
                throw new InvalidOperationException("主窗口未触发 Closed 事件。");

            if (!_exited.Task.IsCompleted)
                throw new InvalidOperationException("Application.Exit 未触发。");
        }
    }
}
