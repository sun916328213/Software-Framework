using CommunityToolkit.Mvvm.ComponentModel;
using Extension.UI.Controls;
using CommunityToolkit.Mvvm.Input;
using DataManager.Controls.Models;
using DataManager.Controls.Services;
using DataManager.Excel.Definition;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace DataManager.Controls.ViewModels
{
    /// <summary>
    /// Excel 测试页 ViewModel：只暴露 <see cref="ExcelPageModel{T}"/> + 装配命令（对标 WPFControl 用法）。
    /// 行实体由宿主提供，只约束 <see cref="IExcelTestEntity"/>。
    /// 追加/更新仅 EPPlus 可用（行内 ToolType 变更经 INPC 联动命令可用性）。
    /// </summary>
    public partial class ExcelViewModel<T> : ObservableObject
        where T : class, IExcelTestEntity, new()
    {
        private readonly ExcelWorkspace<T> _workspace;

        /// <summary>页状态（Workspace 持有）。</summary>
        public ExcelPageModel<T> Model => _workspace.Model;

        public ExcelViewModel(ExcelWorkspace<T> workspace)
        {
            _workspace = workspace;
            // Model 与 VM 同生命周期，此处订阅仅刷新命令可用性。
            AttachRow(Model.SelectedExcelParam ?? Model.DataCollection.FirstOrDefault());
            Model.DataCollection.CollectionChanged += (_, _) =>
            {
                AttachRow(Model.SelectedExcelParam ?? Model.DataCollection.FirstOrDefault());
                RefreshEpplusCommands();
            };
            Model.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(Model.SelectedEntity))
                    UpdateSelectedCommand.NotifyCanExecuteChanged();
                if (e.PropertyName == nameof(Model.SelectedExcelParam))
                {
                    AttachRow(Model.SelectedExcelParam);
                    RefreshEpplusCommands();
                }
            };
        }

        /// <summary>供 XAML 绑定的保存回调（控件已落盘，此处记日志）。</summary>
        public Action ProfilesSavedAction => OnProfilesSavedExternally;

        /// <summary>供 XAML 绑定的增加回调。</summary>
        public Action ExcelAddedAction => OnExcelAddedExternally;

        /// <summary>供 XAML 绑定的删除回调。</summary>
        public Action ExcelDeletedAction => OnExcelDeletedExternally;

        /// <summary>ConfigManagerView 保存按钮回调。</summary>
        public void OnProfilesSavedExternally() => Run(() =>
            Log($"✓ 已保存 {Model.DataCollection.Count} 个文件配置 → {Model.FullPath}"));

        public void OnExcelAddedExternally() => Run(() =>
        {
            var profile = Model.DataCollection.LastOrDefault(p => string.IsNullOrWhiteSpace(p.Name))
                ?? new ExcelParam { Name = $"文件{Model.DataCollection.Count + 1}", FilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "excel"), FileName = "workbench.xlsx" };
            if (!Model.DataCollection.Contains(profile))
                Model.DataCollection.Add(profile);
            else
                profile.Name = $"文件{Model.DataCollection.Count}";
            Model.SelectedExcelParam = profile;
            Log($"✓ 已添加文件档案：{profile.Name}（点保存落盘）");
        });

        public void OnExcelDeletedExternally() => Run(() =>
        {
            if (Model.DataCollection.Count == 0)
            {
                var fallback = new ExcelParam { Name = "workbench", FilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "excel"), FileName = "workbench.xlsx" };
                Model.DataCollection.Add(fallback);
                Model.SelectedExcelParam = fallback;
            }
            else if (Model.SelectedExcelParam is null || !Model.DataCollection.Contains(Model.SelectedExcelParam))
            {
                Model.SelectedExcelParam = Model.DataCollection[^1];
            }
            AttachRow(Model.SelectedExcelParam);
            RefreshEpplusCommands();
        });

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

        private bool IsEPPlus => (Model.SelectedExcelParam ?? Model.DataCollection.FirstOrDefault())?.ToolType == ExcelType.EPPlus;
        private bool CanUseEPPlus => IsEPPlus;
        private bool CanUpdateSelected => Model.SelectedEntity is not null && IsEPPlus;

        // ---------- 运行反馈（展示层） ----------
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
            _workspace.ApplyProfile();
            var p = Model.CurrentProfile();
            Log($"✓ Excel 实例已创建: {p.ToolType} → {p.FileName}");
        });

        [RelayCommand]
        private Task Write10Async() => RunWork("覆盖写入 10 条", () => _workspace.OverwriteWithSample(10));

        [RelayCommand(CanExecute = nameof(CanUseEPPlus))]
        private Task Add1Async() => RunWork("追加 1 条", _workspace.AppendSample);

        [RelayCommand(CanExecute = nameof(CanUseEPPlus))]
        private Task Add10Async() => RunWork("追加 10 条", () => _workspace.AppendSampleBatch(10));

        [RelayCommand]
        private Task QueryAllAsync() => RunWork("查询全部", _workspace.QueryAll);

        [RelayCommand]
        private Task QueryConditionAsync() => RunWork("条件查询 Age>25", _workspace.QueryAdults);

        [RelayCommand(CanExecute = nameof(CanUpdateSelected))]
        private Task UpdateSelectedAsync() => RunWork("更新选中行", () =>
        {
            var sel = Model.SelectedEntity ?? throw new InvalidOperationException("请先选中一行数据");
            _workspace.UpdateSelected(sel);
        });

        [RelayCommand]
        private void OpenFolder() => Run(() =>
        {
            var dir = Model.CurrentProfile().FilePath;
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

        // ---------- 展示层：日志输出/计时/忙指示 ----------
        private void Log(string msg) => DispatcherEx.UIInvokeSafe(
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
