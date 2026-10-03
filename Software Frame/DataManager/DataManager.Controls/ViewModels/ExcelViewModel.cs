// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：ExcelViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Controls、DataManager.Core、CommunityToolkit.Mvvm.ComponentModel、Extension.UI.Controls、CommunityToolkit.Mvvm.Input、DataManager.Controls.Models。导入不代表所有类型都会在运行时加载。
using DataManager.Controls;
using DataManager.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using Extension.UI.Controls;
using CommunityToolkit.Mvvm.Input;
using DataManager.Controls.Models;
using DataManager.Controls.Services;
using DataManager.Core.Excel.Definition;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace DataManager.Controls.ViewModels
{
    /// <summary>
    /// Excel 测试页 ViewModel：只暴露 <see cref="ExcelPageModel{T}"/> + 装配命令（对标 WPFControls 用法）。
    /// 行实体由宿主提供，只约束 <see cref="IExcelTestEntity"/>。
    /// 追加/更新仅 EPPlus 可用（行内 ToolType 变更经 INPC 联动命令可用性）。
    /// </summary>
    public partial class ExcelViewModel<T> : ObservableObject
        where T : class, IExcelTestEntity, new()
    {
        private readonly ExcelWorkspace<T> _workspace;

        /// <summary>页状态（Workspace 持有）。</summary>
        public ExcelPageModel<T> Model => _workspace.Model;

        /// <summary>
        /// 初始化 ExcelViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 AttachRow、FirstOrDefault、RefreshEpplusCommands、nameof、NotifyCanExecuteChanged；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnExcelAddedExternally。
        /// 正文中的关键协作调用包括 Run、LastOrDefault、IsNullOrWhiteSpace、Combine、Contains；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnExcelDeletedExternally。
        /// 正文中的关键协作调用包括 Run、Combine、Add、Contains、AttachRow；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 AttachRow。
        /// </summary>
        private void AttachRow(ExcelParam? row)
        {
            if (_currentRow is not null)
                _currentRow.PropertyChanged -= OnRowPropertyChanged;
            _currentRow = row;
            if (_currentRow is not null)
                _currentRow.PropertyChanged += OnRowPropertyChanged;
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnRowPropertyChanged。
        /// 正文中的关键协作调用包括 nameof、RefreshEpplusCommands；具体条件和先后次序见方法体。
        /// </summary>
        private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ExcelParam.ToolType))
                RefreshEpplusCommands();
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 RefreshEpplusCommands。
        /// 正文中的关键协作调用包括 NotifyCanExecuteChanged；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 执行写入操作；对应入口为 Write10Async。
        /// 正文中的关键协作调用包括 RunWork、OverwriteWithSample；具体条件和先后次序见方法体。
        /// </summary>
        [RelayCommand]
        private Task Write10Async() => RunWork("覆盖写入 10 条", () => _workspace.OverwriteWithSample(10));

        /// <summary>
        /// 执行添加操作；对应入口为 Add1Async。
        /// 正文中的关键协作调用包括 nameof、RunWork；具体条件和先后次序见方法体。
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanUseEPPlus))]
        private Task Add1Async() => RunWork("追加 1 条", _workspace.AppendSample);

        /// <summary>
        /// 执行添加操作；对应入口为 Add10Async。
        /// 正文中的关键协作调用包括 nameof、RunWork、AppendSampleBatch；具体条件和先后次序见方法体。
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanUseEPPlus))]
        private Task Add10Async() => RunWork("追加 10 条", () => _workspace.AppendSampleBatch(10));

        /// <summary>
        /// 执行查询操作；对应入口为 QueryAllAsync。
        /// 正文中的关键协作调用包括 RunWork；具体条件和先后次序见方法体。
        /// </summary>
        [RelayCommand]
        private Task QueryAllAsync() => RunWork("查询全部", _workspace.QueryAll);

        /// <summary>
        /// 执行查询操作；对应入口为 QueryConditionAsync。
        /// 正文中的关键协作调用包括 RunWork；具体条件和先后次序见方法体。
        /// </summary>
        [RelayCommand]
        private Task QueryConditionAsync() => RunWork("条件查询 Age>25", _workspace.QueryAdults);

        /// <summary>
        /// 更新当前实现管理的数据或状态；对应入口为 UpdateSelectedAsync。
        /// 正文中的关键协作调用包括 nameof、RunWork、UpdateSelected；具体条件和先后次序见方法体。
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanUpdateSelected))]
        private Task UpdateSelectedAsync() => RunWork("更新选中行", () =>
        {
            var sel = Model.SelectedEntity ?? throw new InvalidOperationException("请先选中一行数据");
            _workspace.UpdateSelected(sel);
        });

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 OpenFolder。
        /// 正文中的关键协作调用包括 Run、CurrentProfile、Start；具体条件和先后次序见方法体。
        /// </summary>
        [RelayCommand]
        private void OpenFolder() => Run(() =>
        {
            var dir = Model.CurrentProfile().FilePath;
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        });

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ClearOutput。
        /// </summary>
        [RelayCommand]
        private void ClearOutput() => OutputText = string.Empty;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ExportOutput。
        /// 正文中的关键协作调用包括 ShowDialog、WriteAllText、Log；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
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

        /// <summary>
        /// 运行当前处理流程；对应入口为 RunWork。
        /// 正文中的关键协作调用包括 Run、StartNew、action、Stop、Log；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
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

        /// <summary>
        /// 运行当前处理流程；对应入口为 Run。
        /// 正文中的关键协作调用包括 action、Log；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
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
