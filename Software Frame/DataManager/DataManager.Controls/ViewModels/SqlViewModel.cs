// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：SqlViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Controls、DataManager.Core、CommunityToolkit.Mvvm.ComponentModel、Extension.UI.Controls、CommunityToolkit.Mvvm.Input、DataManager.Controls.Models。导入不代表所有类型都会在运行时加载。
using DataManager.Controls;
using DataManager.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using Extension.UI.Controls;
using CommunityToolkit.Mvvm.Input;
using DataManager.Controls.Models;
using DataManager.Controls.Services;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL.Definition;

namespace DataManager.Controls.ViewModels
{
    /// <summary>
    /// SQL 测试页 ViewModel：只暴露 <see cref="SqlPageModel{T}"/> + 装配命令（对标 WPFControls 用法）。
    /// 行实体由宿主提供（如工作台的演示实体），此处只约束 <see cref="ISqlEntity"/>。
    /// 状态在 Model，流程在 Workspace；运行输出/忙指示/计时是纯展示层反馈，留在此处。
    /// </summary>
    public partial class SqlViewModel<T> : ObservableObject
        where T : class, ISqlEntity, new()
    {
        private readonly SqlWorkspace<T> _workspace;

        /// <summary>页状态（Workspace 持有）。</summary>
        public SqlPageModel<T> Model => _workspace.Model;

        /// <summary>
        /// 初始化 SqlViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 nameof、NotifyCanExecuteChanged；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 执行断开连接操作；对应入口为 Disconnect。
        /// 正文中的关键协作调用包括 Run；具体条件和先后次序见方法体。
        /// </summary>
        [RelayCommand]
        private void Disconnect() => Run(_workspace.Disconnect);

        /// <summary>
        /// 执行添加操作；对应入口为 AddAsync。
        /// 正文中的关键协作调用包括 RunWork；具体条件和先后次序见方法体。
        /// </summary>
        [RelayCommand]
        private Task AddAsync() => RunWork("单条添加", _workspace.AddNew);

        /// <summary>
        /// 执行添加操作；对应入口为 AddRangeAsync。
        /// 正文中的关键协作调用包括 RunWork、AddNewBatch；具体条件和先后次序见方法体。
        /// </summary>
        [RelayCommand]
        private Task AddRangeAsync() => RunWork("批量添加 10 条", () => _workspace.AddNewBatch(10));

        /// <summary>
        /// 执行查询操作；对应入口为 QueryAllAsync。
        /// 正文中的关键协作调用包括 RunAsync；具体条件和先后次序见方法体。
        /// </summary>
        [RelayCommand]
        private Task QueryAllAsync() => RunAsync("查询全部", _workspace.QueryAllAsync);

        /// <summary>
        /// 更新当前实现管理的数据或状态；对应入口为 UpdateSelectedAsync。
        /// 正文中的关键协作调用包括 nameof、RunWork、UpdateSelected；具体条件和先后次序见方法体。
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanOperateOnSelection))]
        private Task UpdateSelectedAsync() => RunWork("更新选中行", () =>
        {
            var sel = Model.SelectedEntity ?? throw new InvalidOperationException("请先选中一行数据");
            _workspace.UpdateSelected(sel);
        });

        /// <summary>
        /// 执行删除操作；对应入口为 DeleteSelectedAsync。
        /// 正文中的关键协作调用包括 nameof、RunWork、DeleteSelected；具体条件和先后次序见方法体。
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanOperateOnSelection))]
        private Task DeleteSelectedAsync() => RunWork("删除选中行", () =>
        {
            var sel = Model.SelectedEntity ?? throw new InvalidOperationException("请先选中一行数据");
            _workspace.DeleteSelected(sel);
        });

        /// <summary>
        /// 执行删除操作；对应入口为 DeleteAllAsync。
        /// 正文中的关键协作调用包括 RunAsync；具体条件和先后次序见方法体。
        /// </summary>
        [RelayCommand]
        private Task DeleteAllAsync() => RunAsync("清空表", _workspace.DeleteAllAsync);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 SplitTables。
        /// 正文中的关键协作调用包括 Run、GetSplitTableNames、Log、SelectedProfile、Join；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 TestConnectionAsync。
        /// 正文中的关键协作调用包括 RunAsync；具体条件和先后次序见方法体。
        /// </summary>
        [RelayCommand]
        private Task TestConnectionAsync() => RunAsync("连接测试", _workspace.TestConnectionAsync);

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

        /// <summary>
        /// P9-5：取消在途数据库操作（宿主在关闭工作台/离开页面时调用；UI 上不新增控件）。
        /// 取消属正常流程——不弹系统错误，只记一行“已取消”。
        /// </summary>
        public void CancelInFlight() => _workspace.CancelInFlight();

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
                catch (OperationCanceledException)
                {
                    // P9-5：取消不是失败——不按错误记录，也不弹系统错误。
                    sw.Stop();
                    LastElapsed = $"{sw.ElapsedMilliseconds} ms";
                    Log($"… {title} 已取消 ({LastElapsed})");
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
        /// 运行当前处理流程；对应入口为 RunAsync。
        /// 正文中的关键协作调用包括 StartNew、action、Stop、Log；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
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
            catch (OperationCanceledException)
            {
                // P9-5：取消不是失败——不按错误记录，也不弹系统错误。
                sw.Stop();
                LastElapsed = $"{sw.ElapsedMilliseconds} ms";
                Log($"… {title} 已取消 ({LastElapsed})");
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
            catch (OperationCanceledException)
            {
                Log("… 操作已取消");
            }
            catch (Exception ex)
            {
                Log($"✗ {ex.Message}");
            }
        }
    }
}
