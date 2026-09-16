using CommunityToolkit.Mvvm.ComponentModel;
using Extension.Core.ControlEx;
using CommunityToolkit.Mvvm.Input;
using DataManager.Controls.Models;
using DataManager.Controls.Services;
using DataManager.Log.Definition;
using System.IO;

namespace DataManager.Controls.ViewModels
{
    /// <summary>
    /// 日志测试页 ViewModel：只暴露 <see cref="LogPageModel"/> + 装配命令（对标 WPFControl 用法）。
    /// </summary>
    public partial class LogViewModel : ObservableObject
    {
        private readonly LogWorkspace _workspace;

        /// <summary>页状态（Workspace 持有）。</summary>
        public LogPageModel Model => _workspace.Model;

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

        public void OnLogAddedExternally() => Run(() =>
        {
            var profile = Model.DataCollection.LastOrDefault(p => string.IsNullOrWhiteSpace(p.Name))
                ?? new DataManager.Log.Definition.LogParam { Name = $"日志{Model.DataCollection.Count + 1}", FilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs") };
            if (!Model.DataCollection.Contains(profile))
                Model.DataCollection.Add(profile);
            else
                profile.Name = $"日志{Model.DataCollection.Count}";
            Model.SelectedLogParam = profile;
            Log($"✓ 已添加日志档案：{profile.Name}（点保存落盘）");
        });

        public void OnLogDeletedExternally() => Run(() =>
        {
            if (Model.DataCollection.Count == 0)
            {
                var fallback = new DataManager.Log.Definition.LogParam { Name = "workbench", FilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs") };
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

        [RelayCommand]
        private Task WriteAsync() => RunAsync("写入日志", _workspace.WriteAsync);

        [RelayCommand]
        private Task WriteManyAsync() => RunAsync("批量写入 100 条", () => _workspace.WriteManyAsync());

        [RelayCommand]
        private void RefreshFiles() => Run(() =>
        {
            _workspace.RefreshFiles();
            Log($"✓ 共 {Model.Files.Count} 个日志文件");
        });

        [RelayCommand]
        private Task QueryByFileAsync() => RunAsync("按文件查询", _workspace.QueryByFileAsync);

        [RelayCommand]
        private Task QueryByRangeAsync() => RunAsync("按时间范围查询", _workspace.QueryByRangeAsync);

        [RelayCommand]
        private Task QueryMultiAsync() => RunAsync("多行读取", _workspace.QueryMultiAsync);

        [RelayCommand]
        private void OpenFolder() => Run(_workspace.OpenLogFolder);

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
