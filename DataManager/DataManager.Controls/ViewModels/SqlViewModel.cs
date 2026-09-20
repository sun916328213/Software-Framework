using CommunityToolkit.Mvvm.ComponentModel;
using Extension.UI.Controls;
using CommunityToolkit.Mvvm.Input;
using DataManager.Controls.Models;
using DataManager.Controls.Services;
using DataManager.SQL.Definition;

namespace DataManager.Controls.ViewModels
{
    /// <summary>
    /// SQL 测试页 ViewModel：只暴露 <see cref="SqlPageModel{T}"/> + 装配命令（对标 WPFControl 用法）。
    /// 行实体由宿主提供（如工作台的演示实体），此处只约束 <see cref="ISqlEntity"/>。
    /// 状态在 Model，流程在 Workspace；运行输出/忙指示/计时是纯展示层反馈，留在此处。
    /// </summary>
    public partial class SqlViewModel<T> : ObservableObject
        where T : class, ISqlEntity, new()
    {
        private readonly SqlWorkspace<T> _workspace;

        /// <summary>页状态（Workspace 持有）。</summary>
        public SqlPageModel<T> Model => _workspace.Model;

        public SqlViewModel(SqlWorkspace<T> workspace)
        {
            _workspace = workspace;
            // Model 与 VM 同生命周期（单例 Workspace 常驻），此处订阅仅刷新命令可用性。
            Model.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(Model.SelectedEntity))
                {
                    UpdateSelectedCommand.NotifyCanExecuteChanged();
                    DeleteSelectedCommand.NotifyCanExecuteChanged();
                }
            };
        }

        /// <summary>供 XAML 绑定的增加回调（命名 + 选中；保存由控件保存按钮完成）。</summary>
        public Action ConnectionAddedAction => OnConnectionAddedExternally;

        /// <summary>供 XAML 绑定的删除回调（保底至少留一个）。</summary>
        public Action ConnectionDeletedAction => OnConnectionDeletedExternally;

        /// <summary>供 XAML 绑定的保存回调（控件已落盘，此处记日志）。</summary>
        public Action ProfilesSavedAction => OnProfilesSavedExternally;

        // ---------- 运行反馈（展示层） ----------
        [ObservableProperty]
        private string outputText = string.Empty;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string lastElapsed = "-";

        private bool CanOperateOnSelection => Model.SelectedEntity is not null;

        // ---------- 命令（全部转调 Workspace，计时/日志由 Run 系列包装） ----------
        [RelayCommand]
        private Task ConnectAsync() => RunAsync("连接", _workspace.ConnectAsync);

        [RelayCommand]
        private void Disconnect() => Run(_workspace.Disconnect);

        [RelayCommand]
        private Task AddAsync() => RunWork("单条添加", _workspace.AddNew);

        [RelayCommand]
        private Task AddRangeAsync() => RunWork("批量添加 10 条", () => _workspace.AddNewBatch(10));

        [RelayCommand]
        private Task QueryAllAsync() => RunAsync("查询全部", _workspace.QueryAllAsync);

        [RelayCommand(CanExecute = nameof(CanOperateOnSelection))]
        private Task UpdateSelectedAsync() => RunWork("更新选中行", () =>
        {
            var sel = Model.SelectedEntity ?? throw new InvalidOperationException("请先选中一行数据");
            _workspace.UpdateSelected(sel);
        });

        [RelayCommand(CanExecute = nameof(CanOperateOnSelection))]
        private Task DeleteSelectedAsync() => RunWork("删除选中行", () =>
        {
            var sel = Model.SelectedEntity ?? throw new InvalidOperationException("请先选中一行数据");
            _workspace.DeleteSelected(sel);
        });

        [RelayCommand]
        private Task DeleteAllAsync() => RunWork("清空表", _workspace.DeleteAll);

        [RelayCommand]
        private void SplitTables() => Run(() =>
        {
            try
            {
                var tables = _workspace.GetSplitTableNames();
                Log($"✓ 共 {tables.Count} 张分表({Model.SelectedProfile().ToolType}): {string.Join(", ", tables)}");
            }
            catch (NotSupportedException ex)
            {
                Log($"⚠ {ex.Message}");
            }
        });

        [RelayCommand]
        private Task TestConnectionAsync() => RunAsync("连接测试", _workspace.TestConnectionAsync);

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
                    FileName = $"sql-output-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
                };
                if (dialog.ShowDialog() != true)
                    return;
                System.IO.File.WriteAllText(dialog.FileName, OutputText);
                Log($"✓ 输出已导出 {OutputText.Length} 字 → {dialog.FileName}");
            }
            catch (Exception ex)
            {
                Log($"✗ 导出失败: {ex.Message}");
            }
        }

        /// <summary>
        /// ConfigManagerView 增加按钮回调：控件已用 Activator 加了空行，此处命名 + 选中。
        /// </summary>
        public void OnConnectionAddedExternally() => Run(() =>
        {
            var profile = Model.DataCollection.LastOrDefault(p => string.IsNullOrWhiteSpace(p.Name))
                ?? new SQLParam { Name = $"连接{Model.DataCollection.Count + 1}" };
            if (!Model.DataCollection.Contains(profile))
                Model.DataCollection.Add(profile);
            else
                profile.Name = $"连接{Model.DataCollection.Count}";
            Model.SelectedConnection = profile;
            Log($"✓ 已添加连接档案：{profile.Name}（点保存落盘）");
        });

        /// <summary>
        /// ConfigManagerView 删除按钮回调：控件已移除选中行，此处保底至少留一个。
        /// </summary>
        public void OnConnectionDeletedExternally() => Run(() =>
        {
            if (Model.DataCollection.Count == 0)
            {
                var fallback = new SQLParam { Name = "默认连接" };
                Model.DataCollection.Add(fallback);
                Model.SelectedConnection = fallback;
            }
            else if (Model.SelectedConnection is null || !Model.DataCollection.Contains(Model.SelectedConnection))
            {
                Model.SelectedConnection = Model.DataCollection[^1];
            }
        });

        /// <summary>ConfigManagerView 保存按钮回调：控件已写文件，此处记日志。</summary>
        public void OnProfilesSavedExternally() => Run(() =>
            Log($"✓ 已保存 {Model.DataCollection.Count} 个连接档案 → {Model.FullPath}"));

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
