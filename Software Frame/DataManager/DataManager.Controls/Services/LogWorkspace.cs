// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：LogWorkspace。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Controls、DataManager.Core、DataManager.Controls.Models、Extension.UI.Controls、Extension.Core.Conversion、DataManager.Core.Log。导入不代表所有类型都会在运行时加载。
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

namespace DataManager.Controls.Services
{
    /// <summary>
    /// 日志实际流程（对标 WPFControls 的 DataQueryService：持有 <see cref="LogPageModel"/> 并读写它）。
    /// 职责：Model 初始化、日志实例生命周期（指纹重建）、写入、文件枚举、查询（含结果集回填）。
    /// </summary>
    public sealed class LogWorkspace : IDisposable
    {
        /// <summary>日志文件名前缀（logyyyyMMdd，无扩展名，三种实现一致）。</summary>
        public const string LogFilePattern = "log*";

        private readonly ILogManager _logManager;
        private readonly Random _random = new();
        private ILog? _log;
        private string? _currentKey;

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

        /// <summary>强制重建实例（初始化命令用）。</summary>
        public ILog ResetLog(LogParam param)
        {
            if (_log != null)
            {
                try { (_log as IDisposable)?.Dispose(); } catch { }
                _logManager.Remove(_log);
                _log = null;
            }
            _log = _logManager.Create(param);
            _currentKey = BuildLogKey(param);
            return _log;
        }

        /// <summary>按指纹确保实例（命中复用，否则重建；返回是否发生重建）。</summary>
        public (ILog Log, bool Rebuilt) EnsureLog(LogParam param)
        {
            var key = BuildLogKey(param);
            if (_log != null && _currentKey == key)
                return (_log, false);
            var changed = _log != null;
            var log = ResetLog(param);
            return (log, changed);
        }

        /// <summary>
        /// 执行写入操作；对应入口为 WriteAsync。
        /// 正文中的关键协作调用包括 CurrentProfile、EnsureLog、AddAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public async Task WriteAsync()
        {
            var param = Model.CurrentProfile();
            var (log, _) = EnsureLog(param);
            await log.AddAsync(Model.Message, Model.SelectedLevel);
        }

        /// <summary>
        /// 执行写入操作；对应入口为 WriteManyAsync。
        /// 正文中的关键协作调用包括 CurrentProfile、EnsureLog、AddAsync、Next；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public async Task WriteManyAsync(int count = 100)
        {
            var param = Model.CurrentProfile();
            var (log, _) = EnsureLog(param);
            for (int i = 1; i <= count; i++)
            {
                await log.AddAsync($"第 {i} 条批量测试日志 - {_random.Next(1000, 9999)}", (ELogLevel)(i % 5));
            }
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
        /// 正文中的关键协作调用包括 CurrentProfile、EnsureLog、Run、ToList、Query；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public async Task QueryByFileAsync()
        {
            var file = Model.SelectedFile ?? throw new InvalidOperationException("请先刷新并选择日志文件");
            var param = Model.CurrentProfile();
            var (log, _) = EnsureLog(param);
            var data = await Task.Run(() => log.Query(file).ToList());
            DispatcherEx.UIInvokeSafe(() => Model.Results = new ObservableCollection<LogInfo>(data));
        }

        /// <summary>
        /// 执行查询操作；对应入口为 QueryByRangeAsync。
        /// 正文中的关键协作调用包括 CurrentProfile、EnsureLog、Run、ToList、Query；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public async Task QueryByRangeAsync()
        {
            var param = Model.CurrentProfile();
            var (log, _) = EnsureLog(param);
            var start = Model.StartDate;
            var end = Model.EndDate;
            var data = await Task.Run(() => log.Query(start, end).ToList());
            DispatcherEx.UIInvokeSafe(() => Model.Results = new ObservableCollection<LogInfo>(data));
        }

        /// <summary>
        /// 执行查询操作；对应入口为 QueryMultiAsync。
        /// 正文中的关键协作调用包括 CurrentProfile、EnsureLog、Run、ToList、Query_Multi；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public async Task QueryMultiAsync()
        {
            var file = Model.SelectedFile ?? throw new InvalidOperationException("请先刷新并选择日志文件");
            var param = Model.CurrentProfile();
            var (log, _) = EnsureLog(param);
            var lines = await Task.Run(() => log.Query_Multi(file).ToList());
            DispatcherEx.UIInvokeSafe(() => Model.MultiText = string.Join(Environment.NewLine, lines));
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
        /// <summary>
        /// 执行资源释放步骤；对应入口为 Dispose。
        /// </summary>
        public void Dispose()
        {
            var log = _log;
            _log = null;
            (log as IDisposable)?.Dispose();
        }

    }
}
