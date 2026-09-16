using DataManager.Controls.Models;
using DataManager.Controls.Services;
using DataManager.Controls.ViewModels;
using DataManager.Controls.Views;
using DataManager.SQL;
using DataManager.Workbench;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Xunit.Abstractions;

namespace DataManager.Tests.Workbench
{
    /// <summary>ConfigManager 控件保存整链：FilePath/FileName/ItemsSource 绑定 + 保存按钮落盘。</summary>
    [Collection("WorkbenchAppBoot")]
    public class ControlSaveTests(WorkbenchAppFixture host, ITestOutputHelper output)
    {
        private readonly WorkbenchAppFixture _host = host;
        private readonly ITestOutputHelper _output = output;

        [Fact]
        public void Probe_ControlSave_WritesProfileFile()
        {
            var cfgPath = new SqlPageModel<DataManager.Workbench.Models.TestEntity>().FullPath!;
            // 与 SqlConnectionStoreTests 并行时会抢同一份配置文件，Exists→Read 之间可能被删，读不到就按「无备份」处理。
            var cfgBackup = TryReadAllText(cfgPath);
            try
            {
                _host.Invoke(() =>
                {
                    if (File.Exists(cfgPath)) File.Delete(cfgPath);

                    var vm = new SqlViewModel<DataManager.Workbench.Models.TestEntity>(
                        new SqlWorkspace<DataManager.Workbench.Models.TestEntity>(new SQLManager()));
                    var view = new SqlView { DataContext = vm };
                    var window = new Window
                    {
                        Content = view, Width = 1400, Height = 900,
                        WindowStyle = WindowStyle.None, ShowInTaskbar = false, ShowActivated = false,
                    };
                    try
                    {
                        window.Show();
                        window.UpdateLayout();
                        Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);

                        var mgr = FindChild<WPFCustomControls.Controls.ConfigManagerView>(view);
                        Assert.NotNull(mgr);
                        _output.WriteLine($"FilePath='{mgr!.FilePath}' FileName='{mgr.FileName}'");
                        Assert.Equal(vm.Model.FilePath, mgr.FilePath);
                        Assert.Equal(vm.Model.FileName, mgr.FileName);
                        Assert.Same(vm.Model.DataCollection, mgr.ItemsSource);

                        mgr.SaveCommand.Execute(null);
                        Assert.True(File.Exists(cfgPath));
                        _output.WriteLine(File.ReadAllText(cfgPath)[..Math.Min(200, File.ReadAllText(cfgPath).Length)]);
                    }
                    finally
                    {
                        window.Close();
                    }
                });
            }
            finally
            {
                if (cfgBackup is null) { try { File.Delete(cfgPath); } catch { } }
                else File.WriteAllText(cfgPath, cfgBackup);
            }
        }

        private static string? TryReadAllText(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch
            {
                return null;
            }
        }

        private static T? FindChild<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T t) return t;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var found = FindChild<T>(VisualTreeHelper.GetChild(root, i));
                if (found is not null) return found;
            }
            return null;
        }
    }
}
