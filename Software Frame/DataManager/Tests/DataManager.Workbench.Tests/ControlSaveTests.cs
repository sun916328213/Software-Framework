// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ControlSaveTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Controls.Models、DataManager.Controls.Services、DataManager.Controls.ViewModels、DataManager.Controls.Views、DataManager.Core.SQL。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Controls.Configuration;
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
    /// <summary>
    /// ConfigManager 控件保存整链：FilePath/FileName/ItemsSource 绑定 + 保存按钮落盘。
    /// 配置目录由 <see cref="ConfigRoot.CreateTemporary"/> 隔离（本测试独占一份），
    /// 不再备份/删写真实工作区 Config，也不再与 SqlConnectionStoreTests 抢同一份文件。
    /// </summary>
    [Collection("WorkbenchAppBoot")]
    public class ControlSaveTests(WorkbenchAppFixture host, ITestOutputHelper output)
    {
        private readonly WorkbenchAppFixture _host = host;
        private readonly ITestOutputHelper _output = output;

        /// <summary>
        /// 测试相关入口 Probe_ControlSave_WritesProfileFile；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 CreateTemporary、CreateDirectory、Invoke、Exists、Show；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        [Fact]
        public void Probe_ControlSave_WritesProfileFile()
        {
            // 本测试独占一份临时配置目录：不再读写共享 Config，也不需要备份/还原真实文件。
            var configRoot = ConfigRoot.CreateTemporary();
            var cfgPath = new SqlPageModel<DataManager.Workbench.Models.TestEntity>(configRoot).FullPath!;
            Directory.CreateDirectory(configRoot.Directory);
            try
            {
                _host.Invoke(() =>
                {
                    var vm = new SqlViewModel<DataManager.Workbench.Models.TestEntity>(
                        new SqlWorkspace<DataManager.Workbench.Models.TestEntity>(new SQLManager(), configRoot));
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
                // 只清本测试自己的临时目录；清理失败不覆盖断言抛出的原异常。
                try
                {
                    if (Directory.Exists(configRoot.Directory))
                        Directory.Delete(configRoot.Directory, recursive: true);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
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
