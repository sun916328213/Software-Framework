// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：LogViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Controls、DataManager.Core、CommunityToolkit.Mvvm.ComponentModel、Extension.UI.Controls、CommunityToolkit.Mvvm.Input、DataManager.Controls.Models。导入不代表所有类型都会在运行时加载。
using DataManager.Controls;
using DataManager.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using Extension.UI.Controls;
using CommunityToolkit.Mvvm.Input;
using DataManager.Controls.Models;
using DataManager.Controls.Services;
using DataManager.Core.Log.Definition;
using System.IO;

namespace DataManager.Controls.ViewModels
{
    /// <summary>
    /// 日志测试页 ViewModel：只暴露 <see cref="LogPageModel"/> + 装配命令（对标 WPFControls 用法）。
    /// </summary>
    public partial class LogViewModel : ObservableObject
    {
        private readonly LogWorkspace _workspace;

        /// <summary>页状态（Workspace 持有）。</summary>
        public LogPageModel Model => _workspace.Model;

        /// <summary>
        /// 初始化 LogViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public LogViewModel(LogWorkspace workspace)
        {
            _workspace = workspace;
        }

        /// <summary>供 XAML 绑定的保存回调（控件已落盘，此处记日志）。</summary>
        public Action ProfilesSavedAction => OnProfilesSavedExternally;

        /// <summary>供 XAML 绑定的增加回调（命名 + 选中；保存由控件保存按钮完成）。</summary>
        public Action LogAddedAction => OnLogAddedExternally;

        /// <summary>供 XAML 绑定的删除回调（保底至少留一个）。</summary>
        public Action LogDeletedAction => OnLogDeletedExternally;

        /// <summary>ConfigManagerView 保存按钮回调。</summary>
        public void OnProfilesSavedExternally() => Run(() =>
            Log($"✓ 已保存 {Model.DataCollection.Count} 个日志配置 → {Model.FullPath}"));

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnLogAddedExternally。
        /// 正文中的关键协作调用包括 Run、LastOrDefault、IsNullOrWhiteSpace、Combine、Contains；具体条件和先后次序见方法体。
        /// </summary>
        public void OnLogAddedExternally() => Run(() =>
        {
            var profile = Model.DataCollection.LastOrDefault(p => string.IsNullOrWhiteSpace(p.Name))
                ?? new DataManager.Core.Log.Definition.LogParam { Name = $"日志{Model.DataCollection.Count + 1}", FilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs") };
            if (!Model.DataCollection.Contains(profile))
                Model.DataCollection.Add(profile);
            else
                profile.Name = $"日志{Model.DataCollection.Count}";
            Model.SelectedLogParam = profile;
            Log($"✓ 已添加日志档案：{profile.Name}（点保存落盘）");
        });

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnLogDeletedExternally。
        /// 正文中的关键协作调用包括 Run、Combine、Add、Contains；具体条件和先后次序见方法体。
        /// </summary>
        public void OnLogDeletedExternally() => Run(() =>
        {
            if (Model.DataCollection.Count == 0)
            {
                var fallback = new DataManager.Core.Log.Definition.LogParam { Name = "workbench", FilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs") };
                Model.DataCollection.Add(fallback);
                Model.SelectedLogParam = fallback;
            }
            else if (Model.SelectedLogParam is null || !Model.DataCollection.Contains(Model.SelectedLogParam))
            {
                Model.SelectedLogParam = Model.DataCollection[^1];
            }
        });

        // ---------- 写入等级下拉（非档案，纯展示源） ----------
        public Array Levels { get; } = Enum.GetValues(typeof(ELogLevel));

        // ---------- 运行反馈（展示层） ----------
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
            _workspace.ResetLog(Model.CurrentProfile());
            Log($"✓ 日志已初始化 ({Model.CurrentProfile().LogType}): {Model.CurrentProfile().FilePath}");
        });

        /// <summary>
        /// 执行写入操作；对应入口为 WriteAsync。
        /// 正文中的关键协作调用包括 RunAsync；具体条件和先后次序见方法体。
        /// </summary>
        [RelayCommand]
        private Task WriteAsync() => RunAsync("写入日志", _workspace.WriteAsync);

        /// <summary>
        /// 执行写入操作；对应入口为 WriteManyAsync。
        /// 正文中的关键协作调用包括 RunAsync、WriteManyAsync；具体条件和先后次序见方法体。
        /// </summary>
        [RelayCommand]
        private Task WriteManyAsync() => RunAsync("批量写入 100 条", () => _workspace.WriteManyAsync());

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 RefreshFiles。
        /// 正文中的关键协作调用包括 Run、RefreshFiles、Log；具体条件和先后次序见方法体。
        /// </summary>
        [RelayCommand]
        private void RefreshFiles() => Run(() =>
        {
            _workspace.RefreshFiles();
            Log($"✓ 共 {Model.Files.Count} 个日志文件");
        });

        /// <summary>
        /// 执行查询操作；对应入口为 QueryByFileAsync。
        /// 正文中的关键协作调用包括 RunAsync；具体条件和先后次序见方法体。
        /// </summary>
        [RelayCommand]
        private Task QueryByFileAsync() => RunAsync("按文件查询", _workspace.QueryByFileAsync);

        /// <summary>
        /// 执行查询操作；对应入口为 QueryByRangeAsync。
        /// 正文中的关键协作调用包括 RunAsync；具体条件和先后次序见方法体。
        /// </summary>
        [RelayCommand]
        private Task QueryByRangeAsync() => RunAsync("按时间范围查询", _workspace.QueryByRangeAsync);

        /// <summary>
        /// 执行查询操作；对应入口为 QueryMultiAsync。
        /// 正文中的关键协作调用包括 RunAsync；具体条件和先后次序见方法体。
        /// </summary>
        [RelayCommand]
        private Task QueryMultiAsync() => RunAsync("多行读取", _workspace.QueryMultiAsync);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 OpenFolder。
        /// 正文中的关键协作调用包括 Run；具体条件和先后次序见方法体。
        /// </summary>
        [RelayCommand]
        private void OpenFolder() => Run(_workspace.OpenLogFolder);

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
            catch (Exception ex)
            {
                Log($"✗ {ex.Message}");
            }
        }
    }
}
