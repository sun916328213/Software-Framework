// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：LogWorkspace。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Controls、DataManager.Core、DataManager.Controls.Models、Extension.UI.Controls、Extension.Core.Conversion、DataManager.Core.Log、DataManager.Runtime.Log、DataManager.Core.Log.Definition。导入不代表所有类型都会在运行时加载。
using DataManager.Controls;
using DataManager.Core;
using DataManager.Controls.Models;
using Extension.UI.Controls;
using Extension.Core.Conversion;
using DataManager.Controls.Configuration;
using DataManager.Core.Log;
using DataManager.Runtime.Log;
using DataManager.Core.Log.Definition;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace DataManager.Controls.Services
{
    /// <summary>
    /// 日志实际流程（对标 WPFControls 的 DataQueryService：持有 <see cref="LogPageModel"/> 并读写它）。
    /// 职责：Model 初始化、日志实例生命周期（指纹重建）、写入、文件枚举、查询（含结果集回填）。
    /// T08：慢查询/写入走可取消 Token；重建实例、切换档案与释放时先取消在途操作；
    /// 已释放或已取消的操作不再回写界面（与 T07 的 <see cref="SqlWorkspace{T}"/> 同型）。
    /// </summary>
    public sealed class LogWorkspace : IDisposable, IAsyncDisposable
    {
        /// <summary>日志文件名前缀（logyyyyMMdd，无扩展名，三种实现一致）。</summary>
        public const string LogFilePattern = "log*";

        private readonly ILogManager _logManager;
        private readonly Random _random = new();
        private ILog? _log;
        private string? _currentKey;

        /// <summary>
        /// T08：在途操作取消源。同一时刻至多一个在途操作——签发新 Token 前先取消旧的。
        /// 只取消不释放：取消后可能仍有在途调用在 Token 上注册回调，
        /// 提前 Dispose 会让注册抛 ObjectDisposedException；无定时器的 CTS 无托管外资源，交给 GC 即可。
        /// </summary>
        private CancellationTokenSource? _operationCts;
        private volatile bool _disposed;
        private readonly SemaphoreSlim _operationGate = new(1, 1);

        /// <summary>页状态（单例 Workspace 持有，VM 直接暴露）。</summary>
        public LogPageModel Model { get; }

        /// <summary>生产默认：配置落在 <see cref="ConfigRoot.Default"/>（即 &lt;BaseDirectory&gt;\Config）。</summary>
        public LogWorkspace(ILogManager logManager) : this(logManager, ConfigRoot.Default)
        {
        }

        /// <summary>
        /// 初始化 LogWorkspace 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 LoadProfiles；具体条件和先后次序见方法体。
        /// 传入自己的 <see cref="ConfigRoot"/> 可把本工作区读写配置的目录隔离到临时位置。
        /// </summary>
        public LogWorkspace(ILogManager logManager, ConfigRoot configRoot)
        {
            ArgumentNullException.ThrowIfNull(configRoot);
            _logManager = logManager;
            Model = new LogPageModel(configRoot);
            LoadProfiles();
        }

        /// <summary>实例指纹：全部配置要素参与，任一改动都重建实例。</summary>
        public static string BuildLogKey(LogParam param) =>
            $"{param.LogType}:{param.Name}:{param.FilePath}:{param.Separator}:{param.SaveDays}";

        /// <summary>
        /// T08：签发本次操作的取消标记。若已有在途操作，先取消它（新操作即“接管”）。
        /// 已释放时返回已取消的 Token，调用方会立刻观察到取消，不会再触碰日志实例或界面。
        /// </summary>
        public CancellationToken BeginOperation()
        {
            if (_disposed)
                return new CancellationToken(canceled: true);
            _operationCts?.Cancel();
            _operationCts = new CancellationTokenSource();
            return _operationCts.Token;
        }

        /// <summary>
        /// T08：取消在途操作（重建日志实例、切换档案、关闭工作台时调用）。
        /// 取消属正常流程——不抛异常、不弹错误，调用方按“操作已取消”处理。
        /// </summary>
        public void CancelInFlight() => _operationCts?.Cancel();

        /// <summary>
        /// T08：释放——先取消在途操作，再排空已进入的日志操作，最后摘除并释放实例；
        /// 此后一切界面回写都被丢弃。同步兼容入口，内部阻塞等待同一个异步释放。
        /// </summary>
        public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

        /// <summary>
        /// T08：取消在途操作 → 排空已进入的操作 → 从管理器摘除 → 释放实例。
        /// 幂等：重复调用不会二次释放（`_log` 已被摘走即直接返回）。
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            _disposed = true;
            CancelInFlight();
            await _operationGate.WaitAsync().ConfigureAwait(false);
            try
            {
                _operationCts = null;
                var log = _log;
                _log = null;
                _currentKey = null;
                if (log is null) return;
                await Task.Run(() =>
                {
                    try { _logManager.Remove(log); } catch { }
                    try { (log as IDisposable)?.Dispose(); } catch { }
                }).ConfigureAwait(false);
            }
            finally { _operationGate.Release(); }
        }

        /// <summary>
        /// T08：结果回填的统一出口。已释放、或本次操作已被取消时直接丢弃，
        /// 保证关闭工作台后不会再有后台任务继续改界面（排队期间被取消的也拦在第二道检查上）。
        /// </summary>
        private void PostToModel(CancellationToken operationToken, Action write)
        {
            if (_disposed || operationToken.IsCancellationRequested)
                return;
            DispatcherEx.UIInvokeSafe(() =>
            {
                if (_disposed || operationToken.IsCancellationRequested)
                    return;
                write();
            });
        }

        /// <summary>
        /// 强制重建实例（初始化命令用）。
        /// T08：重建前先取消在途操作并排空——否则旧查询会在已被释放的旧实例上继续跑
        /// （旧实现只 Dispose 不取消，旧查询的晚回写会覆盖新结果集）。
        /// </summary>
        public ILog ResetLog(LogParam param)
        {
            ArgumentNullException.ThrowIfNull(param);
            if (_disposed)
                throw new ObjectDisposedException(nameof(LogWorkspace));
            CancelInFlight();
            _operationGate.Wait();
            try
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_log != null)
                {
                    var previous = _log;
                    _log = null;
                    // 先摘除再释放：摘除后新操作拿不到它，避免与释放过程重叠。
                    try { _logManager.Remove(previous); } catch { }
                    try { (previous as IDisposable)?.Dispose(); } catch { }
                }
                _log = _logManager.Create(param);
                _currentKey = BuildLogKey(param);
                return _log;
            }
            finally { _operationGate.Release(); }
        }

        /// <summary>按指纹确保实例（命中复用，否则重建；返回是否发生重建）。</summary>
        public (ILog Log, bool Rebuilt) EnsureLog(LogParam param)
        {
            ArgumentNullException.ThrowIfNull(param);
            if (_disposed)
                throw new ObjectDisposedException(nameof(LogWorkspace));
            var key = BuildLogKey(param);
            if (_log != null && _currentKey == key)
                return (_log, false);
            var changed = _log != null;
            var log = ResetLog(param);
            return (log, changed);
        }

        /// <summary>
        /// 执行写入操作；对应入口为 WriteAsync。
        /// T08：写入同样走操作门闩与取消标记——切换档案/关闭时不会把日志写进已被摘除的实例。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public async Task WriteAsync()
        {
            var param = Model.CurrentProfile();
            var (log, _) = EnsureLog(param);
            var token = BeginOperation();
            var message = Model.Message;
            var level = Model.SelectedLevel;
            await WithLogAsync(log, token, async () =>
            {
                await log.AddAsync(message, level).ConfigureAwait(false);
                return true;
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 执行写入操作；对应入口为 WriteManyAsync。
        /// T08：循环内检查取消标记，批量写入被取消后不再继续。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public async Task WriteManyAsync(int count = 100)
        {
            var param = Model.CurrentProfile();
            var (log, _) = EnsureLog(param);
            var token = BeginOperation();
            await WithLogAsync(log, token, async () =>
            {
                for (int i = 1; i <= count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    await log.AddAsync($"第 {i} 条批量测试日志 - {_random.Next(1000, 9999)}", (ELogLevel)(i % 5)).ConfigureAwait(false);
                }
                return true;
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 RefreshFiles。
        /// 正文中的关键协作调用包括 ListLogFiles、CurrentProfile、UIInvokeSafe、LastOrDefault；具体条件和先后次序见方法体。
        /// </summary>
        public void RefreshFiles()
        {
            var files = ListLogFiles(Model.CurrentProfile().FilePath);
            DispatcherEx.UIInvokeSafe(() =>
            {
                Model.Files = new ObservableCollection<string>(files);
                Model.SelectedFile = Model.Files.LastOrDefault();
            });
        }

        /// <summary>
        /// 执行查询操作；对应入口为 QueryByFileAsync。
        /// T08：慢查询在后台线程执行且可取消；取消/释放后不回写结果集。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public async Task QueryByFileAsync()
        {
            var file = Model.SelectedFile ?? throw new InvalidOperationException("请先刷新并选择日志文件");
            var param = Model.CurrentProfile();
            var (log, _) = EnsureLog(param);
            var token = BeginOperation();
            var data = await WithLogAsync(log, token, () => Task.FromResult(log.Query(file).ToList())).ConfigureAwait(false);
            PostToModel(token, () => Model.Results = new ObservableCollection<LogInfo>(data));
        }

        /// <summary>
        /// 执行查询操作；对应入口为 QueryByRangeAsync。
        /// T08：慢查询在后台线程执行且可取消；取消/释放后不回写结果集。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public async Task QueryByRangeAsync()
        {
            var param = Model.CurrentProfile();
            var (log, _) = EnsureLog(param);
            var token = BeginOperation();
            var start = Model.StartDate;
            var end = Model.EndDate;
            var data = await WithLogAsync(log, token, () => Task.FromResult(log.Query(start, end).ToList())).ConfigureAwait(false);
            PostToModel(token, () => Model.Results = new ObservableCollection<LogInfo>(data));
        }

        /// <summary>
        /// 执行查询操作；对应入口为 QueryMultiAsync。
        /// T08：慢查询在后台线程执行且可取消；取消/释放后不回写文本。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public async Task QueryMultiAsync()
        {
            var file = Model.SelectedFile ?? throw new InvalidOperationException("请先刷新并选择日志文件");
            var param = Model.CurrentProfile();
            var (log, _) = EnsureLog(param);
            var token = BeginOperation();
            var lines = await WithLogAsync(log, token, () => Task.FromResult(log.Query_Multi(file).ToList())).ConfigureAwait(false);
            PostToModel(token, () => Model.MultiText = string.Join(Environment.NewLine, lines));
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 OpenLogFolder。
        /// 正文中的关键协作调用包括 CurrentProfile、CreateDirectory、Start；具体条件和先后次序见方法体。
        /// </summary>
        public void OpenLogFolder()
        {
            var dir = Model.CurrentProfile().FilePath;
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ListLogFiles。
        /// 正文中的关键协作调用包括 Exists、ToList、Select、GetFiles、GetFileName；具体条件和先后次序见方法体。
        /// </summary>
        public IReadOnlyList<string> ListLogFiles(string dir) =>
            Directory.Exists(dir)
                ? Directory.GetFiles(dir, LogFilePattern).Select(f => Path.GetFileName(f)!).ToList()
                : new List<string>();

        /// <summary>本次操作针对的实例是否仍是当前实例（指纹重建后旧操作必须自行作废）。</summary>
        private void CheckCurrent(ILog log)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!ReferenceEquals(log, _log)) throw new OperationCanceledException("Log profile changed.");
        }

        /// <summary>
        /// T08：在操作门闩内执行日志操作，并把它放到后台线程——
        /// Provider 可能捕获调用方上下文，同步排空（Dispose）绝不能等待需要界面线程的续体。
        /// </summary>
        private async Task<TResult> WithLogAsync<TResult>(ILog log, CancellationToken token, Func<Task<TResult>> operation)
        {
            await _operationGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                CheckCurrent(log);
                return await Task.Run(operation, token).ConfigureAwait(false);
            }
            finally { _operationGate.Release(); }
        }

        // ---------- Model 初始化（读 Config 文件 → 默认行） ----------
        private void LoadProfiles()
        {
            var profiles = JsonEx.TryRead<List<LogParam>>(Model.FullPath!);
            if (profiles is { Count: > 0 })
                ReplaceProfiles(profiles.Where(p => !string.IsNullOrWhiteSpace(p.Name)));
            if (Model.DataCollection.Count == 0)
            {
                var fallback = new LogParam
                {
                    Name = "workbench",
                    FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"),
                };
                Model.DataCollection.Add(fallback);
                Model.SelectedLogParam = fallback;
            }
            else
            {
                Model.SelectedLogParam ??= Model.DataCollection.FirstOrDefault();
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ReplaceProfiles。
        /// 正文中的关键协作调用包括 Clear、IsNullOrWhiteSpace、Add、FirstOrDefault；具体条件和先后次序见方法体。
        /// </summary>
        private void ReplaceProfiles(IEnumerable<LogParam> profiles)
        {
            Model.DataCollection.Clear();
            foreach (var p in profiles)
            {
                if (string.IsNullOrWhiteSpace(p.Name))
                    p.Name = $"日志{Model.DataCollection.Count + 1}";
                Model.DataCollection.Add(p);
            }
            Model.SelectedLogParam = Model.DataCollection.FirstOrDefault();
        }

    }
}
