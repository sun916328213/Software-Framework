using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataManager.Log.Definition;
using DataManager.Workbench.Infrastructure;
using DataManager.Workbench.Services;
using Extension.Struct;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;

namespace DataManager.Workbench.ViewModels
{
    /// <summary>
    /// 日志测试页：配置直绑 <see cref="LogParam"/> 行模型（单行），持久化走 ConfigManager 控件
    /// 保存按钮（<see cref="ProfilePath"/>）；实际流程在 <see cref="LogWorkspace"/>。
    /// </summary>
    public partial class LogViewModel : ObservableObject
    {
        private readonly LogWorkspace _workspace;

        /// <summary>日志档案文件完整路径。</summary>
        public static string ProfilePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "LogConfig.json");

        /// <summary>档案目录（XAML 控件 FilePath 绑定用）。</summary>
        public string ConfigDir => Path.GetDirectoryName(ProfilePath)!;

        public LogViewModel(LogWorkspace workspace)
        {
            _workspace = workspace;

            // 新文件 → 老 workbench-config.json 导入 → 默认行，保证界面永不为空。
            var profiles = JsonConfigStore.ReadList<LogParam>(ProfilePath) ?? TryImportLegacy();
            if (profiles is { Count: > 0 })
            {
                foreach (var p in profiles)
                    LogProfiles.Add(p);
            }
            if (LogProfiles.Count == 0)
            {
                LogProfiles.Add(new LogParam
                {
                    Name = "workbench",
                    FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"),
                });
            }
        }

        // ---------- 日志配置档案（单行；增删按钮在 XAML 收起） ----------
        public ObservableCollection<LogParam> LogProfiles { get; } = new();

        /// <summary>供 XAML 绑定的保存回调（控件已落盘，此处记日志）。</summary>
        public Action ProfilesSavedAction => OnProfilesSavedExternally;

        /// <summary>ConfigManagerView 保存按钮回调。</summary>
        public void OnProfilesSavedExternally() => Run(() =>
            Log($"✓ 已保存日志配置 → {ProfilePath}"));

        // ---------- 写入（非档案，保留表单） ----------
        public Array Levels { get; } = Enum.GetValues(typeof(ELogLevel));

        [ObservableProperty]
        private string message = "这是一条测试日志";

        [ObservableProperty]
        private ELogLevel selectedLevel = ELogLevel.Information;

        // ---------- 查询 ----------
        [ObservableProperty]
        private string? selectedFile;

        [ObservableProperty]
        private ObservableCollection<string> files = new();

        [ObservableProperty]
        private DateTime startDate = DateTime.Today;

        [ObservableProperty]
        private DateTime endDate = DateTime.Today;

        // ---------- 结果 ----------
        [ObservableProperty]
        private ObservableCollection<LogInfo> results = new();

        [ObservableProperty]
        private string multiText = string.Empty;

        [ObservableProperty]
        private string outputText = string.Empty;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string lastElapsed = "-";

        // ---------- 命令 ----------
        [RelayCommand]
        private void InitLog() => Run(() =>
        {
            _workspace.ResetLog(CurrentProfile());
            Log($"✓ 日志已初始化 ({CurrentProfile().LogType}): {CurrentProfile().FilePath}");
        });

        [RelayCommand]
        private async Task WriteAsync() => await RunAsync("写入日志", () =>
            _workspace.WriteAsync(CurrentProfile(), Message, SelectedLevel));

        [RelayCommand]
        private async Task WriteManyAsync() => await RunAsync("批量写入 100 条", () =>
            _workspace.WriteManyAsync(CurrentProfile()));

        [RelayCommand]
        private void RefreshFiles() => Run(() =>
        {
            Files = new ObservableCollection<string>(_workspace.ListLogFiles(CurrentProfile().FilePath));
            SelectedFile = Files.LastOrDefault();
            Log($"✓ 共 {Files.Count} 个日志文件");
        });

        [RelayCommand]
        private async Task QueryByFileAsync() => await RunAsync("按文件查询", async () =>
        {
            var file = SelectedFile ?? throw new InvalidOperationException("请先刷新并选择日志文件");
            var data = await _workspace.QueryByFileAsync(CurrentProfile(), file);
            Results = new ObservableCollection<LogInfo>(data);
        });

        [RelayCommand]
        private async Task QueryByRangeAsync() => await RunAsync("按时间范围查询", async () =>
        {
            var data = await _workspace.QueryByRangeAsync(CurrentProfile(), StartDate, EndDate);
            Results = new ObservableCollection<LogInfo>(data);
        });

        [RelayCommand]
        private async Task QueryMultiAsync() => await RunAsync("多行读取", async () =>
        {
            var file = SelectedFile ?? throw new InvalidOperationException("请先刷新并选择日志文件");
            var lines = await _workspace.QueryMultiAsync(CurrentProfile(), file);
            MultiText = string.Join(Environment.NewLine, lines);
        });

        [RelayCommand]
        private void OpenFolder() => Run(() =>
        {
            var dir = CurrentProfile().FilePath;
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        });

        [RelayCommand]
        private void ClearOutput() => OutputText = string.Empty;

        [RelayCommand]
        private void ExportOutput()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "文本文件 (*.txt)|*.txt",
                    FileName = $"log-output-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
                };
                if (dialog.ShowDialog() != true)
                    return;
                File.WriteAllText(dialog.FileName, OutputText);
                Log($"✓ 输出已导出 {OutputText.Length} 字 → {dialog.FileName}");
            }
            catch (Exception ex)
            {
                Log($"✗ 导出失败: {ex.Message}");
            }
        }

        // ---------- 私有方法 ----------
        /// <summary>当前档案（首行；空即报“请先添加”）。</summary>
        private LogParam CurrentProfile() =>
            LogProfiles.FirstOrDefault() ?? throw new InvalidOperationException("请先添加日志配置档案");

        /// <summary>单次导入：老 workbench-config.json 的 Log 节 → 新文件。</summary>
        private static List<LogParam>? TryImportLegacy()
        {
            try
            {
                var path = JsonConfigStore.FileFullPath;
                if (!File.Exists(path))
                    return null;
                if (JsonNode.Parse(File.ReadAllText(path))?.AsObject()?["Log"] is not JsonObject n)
                    return null;

                var result = new List<LogParam>
                {
                    new()
                    {
                        Name = (string?)n["LogName"] ?? "workbench",
                        FilePath = (string?)n["FilePath"] ?? string.Empty,
                        Separator = (string?)n["Separator"] ?? "||",
                        SaveDays = (int?)n["SaveDays"] ?? 180,
                        LogType = Enum.TryParse((string?)n["LogType"], out ELogType t) ? t : ELogType.SeriLog,
                    }
                };
                JsonConfigStore.WriteList(ProfilePath, result);
                JsonConfigStore.RemoveLegacySections("Log");
                return result;
            }
            catch
            {
                return null;
            }
        }

        private void Log(string msg) => UiThread.Invoke(
            () => OutputText = $"[{DateTime.Now:HH:mm:ss}] {msg}\n" + OutputText);

        private async Task RunAsync(string title, Func<Task> action)
        {
            IsBusy = true;
            LastElapsed = "执行中…";
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await action();
                sw.Stop();
                LastElapsed = $"{sw.ElapsedMilliseconds} ms";
                Log($"✓ {title} ({LastElapsed})");
            }
            catch (Exception ex)
            {
                sw.Stop();
                LastElapsed = $"{sw.ElapsedMilliseconds} ms";
                Log($"✗ {title}: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
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
