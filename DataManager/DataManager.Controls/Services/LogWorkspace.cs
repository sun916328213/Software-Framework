using DataManager.Controls.Models;
using Extension.Core.ControlEx;
using Extension.Core.ConvertEx;
using DataManager.Log;
using DataManager.Log.Definition;
using Extension.Struct;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;

namespace DataManager.Controls.Services
{
    /// <summary>
    /// 日志实际流程（对标 WPFControl 的 DataQueryService：持有 <see cref="LogPageModel"/> 并读写它）。
    /// 职责：Model 初始化、日志实例生命周期（指纹重建）、写入、文件枚举、查询（含结果集回填）。
    /// </summary>
    public sealed class LogWorkspace
    {
        /// <summary>日志文件名前缀（logyyyyMMdd，无扩展名，三种实现一致）。</summary>
        public const string LogFilePattern = "log*";

        private readonly LogManager _logManager;
        private readonly Random _random = new();
        private ILog? _log;
        private string? _currentKey;

        /// <summary>页状态（单例 Workspace 持有，VM 直接暴露）。</summary>
        public LogPageModel Model { get; } = new();

        public LogWorkspace(LogManager logManager)
        {
            _logManager = logManager;
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

        public async Task WriteAsync()
        {
            var param = Model.CurrentProfile();
            var (log, _) = EnsureLog(param);
            await log.AddAsync(Model.Message, Model.SelectedLevel);
        }

        public async Task WriteManyAsync(int count = 100)
        {
            var param = Model.CurrentProfile();
            var (log, _) = EnsureLog(param);
            for (int i = 1; i <= count; i++)
            {
                await log.AddAsync($"第 {i} 条批量测试日志 - {_random.Next(1000, 9999)}", (ELogLevel)(i % 5));
            }
        }

        public void RefreshFiles()
        {
            var files = ListLogFiles(Model.CurrentProfile().FilePath);
            DispatcherEx.UIInvokeSafe(() =>
            {
                Model.Files = new ObservableCollection<string>(files);
                Model.SelectedFile = Model.Files.LastOrDefault();
            });
        }

        public async Task QueryByFileAsync()
        {
            var file = Model.SelectedFile ?? throw new InvalidOperationException("请先刷新并选择日志文件");
            var param = Model.CurrentProfile();
            var (log, _) = EnsureLog(param);
            var data = await Task.Run(() => log.Query(file).ToList());
            DispatcherEx.UIInvokeSafe(() => Model.Results = new ObservableCollection<LogInfo>(data));
        }

        public async Task QueryByRangeAsync()
        {
            var param = Model.CurrentProfile();
            var (log, _) = EnsureLog(param);
            var start = Model.StartDate;
            var end = Model.EndDate;
            var data = await Task.Run(() => log.Query(start, end).ToList());
            DispatcherEx.UIInvokeSafe(() => Model.Results = new ObservableCollection<LogInfo>(data));
        }

        public async Task QueryMultiAsync()
        {
            var file = Model.SelectedFile ?? throw new InvalidOperationException("请先刷新并选择日志文件");
            var param = Model.CurrentProfile();
            var (log, _) = EnsureLog(param);
            var lines = await Task.Run(() => log.Query_Multi(file).ToList());
            DispatcherEx.UIInvokeSafe(() => Model.MultiText = string.Join(Environment.NewLine, lines));
        }

        public void OpenLogFolder()
        {
            var dir = Model.CurrentProfile().FilePath;
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }

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
