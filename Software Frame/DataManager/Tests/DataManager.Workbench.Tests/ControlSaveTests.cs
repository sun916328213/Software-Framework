// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ControlSaveTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Controls.Models、DataManager.Controls.Services、DataManager.Controls.ViewModels、DataManager.Controls.Views、DataManager.Core.SQL。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Controls.Models;
using DataManager.Controls.Services;
using DataManager.Controls.ViewModels;
using DataManager.Controls.Views;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using DataManager.Workbench;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Xunit.Abstractions;

namespace DataManager.Workbench.Tests
{
    /// <summary>ConfigManager 控件保存整链：FilePath/FileName/ItemsSource 绑定 + 保存按钮落盘。</summary>
    [Collection("WorkbenchAppBoot")]
    public class ControlSaveTests(WorkbenchAppFixture host, ITestOutputHelper output)
    {
        private readonly WorkbenchAppFixture _host = host;
        private readonly ITestOutputHelper _output = output;

        /// <summary>
        /// 测试相关入口 Probe_ControlSave_WritesProfileFile；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 TryReadAllText、Invoke、Exists、Delete、Show；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
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

        /// <summary>
        /// 测试相关入口 TryReadAllText；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Exists、ReadAllText；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
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

        /// <summary>
        /// 测试相关入口 FindChild；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 GetChildrenCount、FindChild、GetChild；具体条件和先后次序见方法体。
        /// </summary>
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
