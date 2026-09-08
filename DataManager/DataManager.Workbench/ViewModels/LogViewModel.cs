using DataManager.Log;
using DataManager.Log.Definition;
using Extension.Struct;
using Prism.Commands;
using Prism.Mvvm;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;

namespace DataManager.Workbench.ViewModels
{
    /// <summary>
    /// 日志测试页：通过 DataManager 的 LogManager / ILog 完成全部日志操作
    /// </summary>
    public class LogViewModel : BindableBase
    {
        private readonly LogManager _logManager;
        private ILog? _log;
        private string? _currentKey;

        public LogViewModel(LogManager logManager)
        {
            _logManager = logManager;

            InitLogCommand = new DelegateCommand(() => Run(() =>
            {
                ResetLog(CurrentParam());
                Log($"✓ 日志已初始化 ({SelectedLogType}): {FilePath}");
            }));

            WriteCommand = new DelegateCommand(async () => await RunAsync("写入日志", () =>
            {
                var log = EnsureLog();
                return log.AddAsync(Message, SelectedLevel);
            }));

            WriteManyCommand = new DelegateCommand(async () => await RunAsync("批量写入 100 条", async () =>
            {
                var log = EnsureLog();
                for (int i = 1; i <= 100; i++)
                {
                    await log.AddAsync($"第 {i} 条批量测试日志 - {_random.Next(1000, 9999)}", (ELogLevel)(i % 5));
                }
            }));

            RefreshFilesCommand = new DelegateCommand(() => Run(() =>
            {
                // 日志文件名为 logyyyyMMdd（无扩展名，三种实现一致），不能用 *.txt 过滤
                Files = new ObservableCollection<string>(
                    Directory.Exists(FilePath)
                        ? Directory.GetFiles(FilePath, "log*").Select(f => Path.GetFileName(f)!)
                        : Enumerable.Empty<string>());
                SelectedFile = Files.LastOrDefault();
                Log($"✓ 共 {Files.Count} 个日志文件");
            }));

            QueryByFileCommand = new DelegateCommand(async () => await RunAsync("按文件查询", () =>
            {
                var log = EnsureLog();
                var file = SelectedFile ?? throw new InvalidOperationException("请先刷新并选择日志文件");
                return Task.Run(() =>
                {
                    var infos = log.Query(file);
                    Infrastructure.UiThread.Invoke(() => Results = new ObservableCollection<LogInfo>(infos));
                });
            }));

            QueryByRangeCommand = new DelegateCommand(async () => await RunAsync("按时间范围查询", () =>
            {
                var log = EnsureLog();
                return Task.Run(() =>
                {
                    var infos = log.Query(StartDate, EndDate);
                    Infrastructure.UiThread.Invoke(() => Results = new ObservableCollection<LogInfo>(infos));
                });
            }));

            QueryMultiCommand = new DelegateCommand(async () => await RunAsync("多行读取", () =>
            {
                var log = EnsureLog();
                var file = SelectedFile ?? throw new InvalidOperationException("请先刷新并选择日志文件");
                return Task.Run(() =>
                {
                    var lines = log.Query_Multi(file);
                    var text = string.Join(Environment.NewLine, lines);
                    Infrastructure.UiThread.Invoke(() => MultiText = text);
                });
            }));

            OpenFolderCommand = new DelegateCommand(() => Run(() =>
            {
                Directory.CreateDirectory(FilePath);
                Process.Start(new ProcessStartInfo { FileName = FilePath, UseShellExecute = true });
            }));
        }

        private readonly Random _random = new();

        // ---------- 配置 ----------
        public Array LogTypes { get; } = Enum.GetValues(typeof(ELogType));
        public Array Levels { get; } = Enum.GetValues(typeof(ELogLevel));

        public string LogName { get; set; } = "workbench";
        public string FilePath { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        public string Separator { get; set; } = "||";
        public int SaveDays { get; set; } = 180;

        private ELogType _selectedLogType = ELogType.SeriLog;
        public ELogType SelectedLogType
        {
            get => _selectedLogType;
            set => SetProperty(ref _selectedLogType, value);
        }

        // ---------- 写入 ----------
        public string Message { get; set; } = "这是一条测试日志";

        private ELogLevel _selectedLevel = ELogLevel.Information;
        public ELogLevel SelectedLevel
        {
            get => _selectedLevel;
            set => SetProperty(ref _selectedLevel, value);
        }

        // ---------- 查询 ----------
        public string? SelectedFile { get; set; }

        private ObservableCollection<string> _files = new();
        public ObservableCollection<string> Files
        {
            get => _files;
            set => SetProperty(ref _files, value);
        }

        private DateTime _startDate = DateTime.Today;
        public DateTime StartDate
        {
            get => _startDate;
            set => SetProperty(ref _startDate, value);
        }

        private DateTime _endDate = DateTime.Today;
        public DateTime EndDate
        {
            get => _endDate;
            set => SetProperty(ref _endDate, value);
        }

        // ---------- 结果 ----------
        private ObservableCollection<LogInfo> _results = new();
        public ObservableCollection<LogInfo> Results
        {
            get => _results;
            set => SetProperty(ref _results, value);
        }

        private string _multiText = string.Empty;
        public string MultiText
        {
            get => _multiText;
            set => SetProperty(ref _multiText, value);
        }

        private string _outputText = string.Empty;
        public string OutputText
        {
            get => _outputText;
            set => SetProperty(ref _outputText, value);
        }

        // ---------- 命令 ----------
        public DelegateCommand InitLogCommand { get; }
        public DelegateCommand WriteCommand { get; }
        public DelegateCommand WriteManyCommand { get; }
        public DelegateCommand RefreshFilesCommand { get; }
        public DelegateCommand QueryByFileCommand { get; }
        public DelegateCommand QueryByRangeCommand { get; }
        public DelegateCommand QueryMultiCommand { get; }
        public DelegateCommand OpenFolderCommand { get; }

        // ---------- 私有方法 ----------
        /// <summary>
        /// 连接指纹：全部配置要素参与，任一改动都重建实例（与 SqlViewModel.BuildDbKey 同款）。
        /// </summary>
        public static string BuildLogKey(LogParam param) =>
            $"{param.LogType}:{param.Name}:{param.FilePath}:{param.Separator}:{param.SaveDays}";

        private LogParam CurrentParam() => new()
        {
            Name = LogName,
            FilePath = FilePath,
            Separator = Separator,
            SaveDays = SaveDays,
            LogType = SelectedLogType,
        };

        private ILog ResetLog(LogParam param)
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

        private ILog EnsureLog()
        {
            var param = CurrentParam();
            var key = BuildLogKey(param);
            if (_log == null || _currentKey != key)
            {
                var changed = _log != null;
                ResetLog(param);
                if (changed) Log("配置已变更，日志实例已重建");
            }
            return _log ?? throw new InvalidOperationException("日志初始化失败");
        }

        private void Log(string msg) => Infrastructure.UiThread.Invoke(
            () => OutputText = $"[{DateTime.Now:HH:mm:ss}] {msg}\n" + OutputText);

        private async Task RunAsync(string title, Func<Task> action)
        {
            try
            {
                await action();
                Log("✓ " + title);
            }
            catch (Exception ex)
            {
                Log($"✗ {title}: {ex.Message}");
            }
        }

        private void Run(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log($"✗ {ex.Message}");
            }
        }
    }
}
