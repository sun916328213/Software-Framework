using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataManager.Excel.Definition;
using DataManager.Workbench.Infrastructure;
using DataManager.Workbench.Models;
using DataManager.Workbench.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;

namespace DataManager.Workbench.ViewModels
{
    /// <summary>
    /// Excel 测试页：配置直绑 <see cref="ExcelParam"/> 行模型（单行），持久化走 ConfigManager 控件
    /// 保存按钮（<see cref="ProfilePath"/>）；实际流程在 <see cref="ExcelWorkspace"/>。
    /// 追加/更新仅 EPPlus 可用（行内 ToolType 变更经 INPC 联动命令可用性）。
    /// </summary>
    public partial class ExcelViewModel : ObservableObject
    {
        private readonly ExcelWorkspace _workspace;

        /// <summary>文件档案完整路径。</summary>
        public static string ProfilePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "ExcelConfig.json");

        /// <summary>档案目录（XAML 控件 FilePath 绑定用）。</summary>
        public string ConfigDir => Path.GetDirectoryName(ProfilePath)!;

        public ExcelViewModel(ExcelWorkspace workspace)
        {
            _workspace = workspace;

            // 新文件 → 老 workbench-config.json 导入 → 默认行，保证界面永不为空。
            var profiles = JsonConfigStore.ReadList<ExcelParam>(ProfilePath) ?? TryImportLegacy();
            if (profiles is { Count: > 0 })
            {
                foreach (var p in profiles)
                    ExcelProfiles.Add(p);
            }
            if (ExcelProfiles.Count == 0)
            {
                ExcelProfiles.Add(new ExcelParam
                {
                    Name = "workbench",
                    FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "excel"),
                    FileName = "workbench.xlsx",
                });
            }

            AttachRow(ExcelProfiles.FirstOrDefault());
            ExcelProfiles.CollectionChanged += (_, _) =>
            {
                AttachRow(ExcelProfiles.FirstOrDefault());
                RefreshEpplusCommands();
            };
        }

        // ---------- 文件配置档案（单行；增删按钮在 XAML 收起） ----------
        public ObservableCollection<ExcelParam> ExcelProfiles { get; } = new();

        /// <summary>供 XAML 绑定的保存回调（控件已落盘，此处记日志）。</summary>
        public Action ProfilesSavedAction => OnProfilesSavedExternally;

        /// <summary>ConfigManagerView 保存按钮回调。</summary>
        public void OnProfilesSavedExternally() => Run(() =>
            Log($"✓ 已保存文件配置 → {ProfilePath}"));

        private ExcelParam? _currentRow;

        private void AttachRow(ExcelParam? row)
        {
            if (_currentRow is not null)
                _currentRow.PropertyChanged -= OnRowPropertyChanged;
            _currentRow = row;
            if (_currentRow is not null)
                _currentRow.PropertyChanged += OnRowPropertyChanged;
        }

        private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ExcelParam.ToolType))
                RefreshEpplusCommands();
        }

        private void RefreshEpplusCommands()
        {
            Add1Command.NotifyCanExecuteChanged();
            Add10Command.NotifyCanExecuteChanged();
            UpdateSelectedCommand.NotifyCanExecuteChanged();
        }

        private bool IsEPPlus => ExcelProfiles.FirstOrDefault()?.ToolType == ExcelType.EPPlus;
        private bool CanUseEPPlus => IsEPPlus;
        private bool CanUpdateSelected => SelectedEntity is not null && IsEPPlus;

        // ---------- 结果 ----------
        [ObservableProperty]
        private ObservableCollection<ExcelPerson> results = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(UpdateSelectedCommand))]
        private ExcelPerson? selectedEntity;

        [ObservableProperty]
        private string outputText = string.Empty;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string lastElapsed = "-";

        // ---------- 命令 ----------
        [RelayCommand]
        private void Apply() => Run(() =>
        {
            var p = CurrentProfile();
            _workspace.ResetExcel(p);
            Log($"✓ Excel 实例已创建: {p.ToolType} → {p.FileName}");
        });

        [RelayCommand]
        private Task Write10Async() => RunWork("覆盖写入 10 条", () =>
        {
            var data = _workspace.OverwriteWithSample(CurrentProfile(), 10);
            UiThread.Invoke(() => Results = new ObservableCollection<ExcelPerson>(data));
        });

        [RelayCommand(CanExecute = nameof(CanUseEPPlus))]
        private Task Add1Async() => RunWork("追加 1 条", () =>
        {
            var data = _workspace.AppendSample(CurrentProfile());
            UiThread.Invoke(() => Results = new ObservableCollection<ExcelPerson>(data));
        });

        [RelayCommand(CanExecute = nameof(CanUseEPPlus))]
        private Task Add10Async() => RunWork("追加 10 条", () =>
        {
            var data = _workspace.AppendSampleBatch(CurrentProfile(), 10);
            UiThread.Invoke(() => Results = new ObservableCollection<ExcelPerson>(data));
        });

        [RelayCommand]
        private Task QueryAllAsync() => RunWork("查询全部", () =>
        {
            var data = _workspace.QueryAll(CurrentProfile());
            UiThread.Invoke(() => Results = new ObservableCollection<ExcelPerson>(data));
        });

        [RelayCommand]
        private Task QueryConditionAsync() => RunWork("条件查询 Age>25", () =>
        {
            var data = _workspace.QueryAdults(CurrentProfile());
            UiThread.Invoke(() => Results = new ObservableCollection<ExcelPerson>(data));
        });

        [RelayCommand(CanExecute = nameof(CanUpdateSelected))]
        private Task UpdateSelectedAsync() => RunWork("更新选中行", () =>
        {
            var sel = SelectedEntity ?? throw new InvalidOperationException("请先选中一行数据");
            var data = _workspace.UpdateSelected(CurrentProfile(), sel);
            UiThread.Invoke(() => Results = new ObservableCollection<ExcelPerson>(data));
        });

        [RelayCommand]
        private void OpenFolder() => Run(() =>
        {
            var dir = CurrentProfile().FilePath;
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
                    FileName = $"excel-output-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
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
        private ExcelParam CurrentProfile() =>
            ExcelProfiles.FirstOrDefault() ?? throw new InvalidOperationException("请先添加文件配置档案");

        /// <summary>单次导入：老 workbench-config.json 的 Excel 节 → 新文件。</summary>
        private static List<ExcelParam>? TryImportLegacy()
        {
            try
            {
                var path = JsonConfigStore.FileFullPath;
                if (!File.Exists(path))
                    return null;
                if (JsonNode.Parse(File.ReadAllText(path))?.AsObject()?["Excel"] is not JsonObject n)
                    return null;

                var result = new List<ExcelParam>
                {
                    new()
                    {
                        Name = "workbench",
                        FilePath = (string?)n["FilePath"] ?? string.Empty,
                        FileName = (string?)n["FileName"] ?? "workbench.xlsx",
                        HasHeader = (bool?)n["HasHeader"] ?? true,
                        ToolType = Enum.TryParse((string?)n["ToolType"], out ExcelType t) ? t : ExcelType.MiniExcel,
                    }
                };
                JsonConfigStore.WriteList(ProfilePath, result);
                JsonConfigStore.RemoveLegacySections("Excel");
                return result;
            }
            catch
            {
                return null;
            }
        }

        private void Log(string msg) => UiThread.Invoke(
            () => OutputText = $"[{DateTime.Now:HH:mm:ss}] {msg}\n" + OutputText);

        private Task RunWork(string title, Action action)
        {
            IsBusy = true;
            LastElapsed = "执行中…";
            return Task.Run(() =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    action();
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
            });
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
