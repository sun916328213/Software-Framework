// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：MvvmInteractionTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Windows、System.Windows.Controls、System.Windows.Media、System.Windows.Threading、System.Windows.Automation.Peers、System.Windows.Automation.Provider。导入不代表所有类型都会在运行时加载。
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using Prism.Ioc;
using Prism.Regions;
using SecsGem.Workbench.ViewModels;

namespace SecsGem.Workbench.Tests;

/// <summary>
/// MvvmInteractionTests 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
[Collection("WorkbenchAppBoot")]
public sealed class MvvmInteractionTests(WorkbenchAppFixture host)
{
    /// <summary>
    /// 验证 RealSampleAndVariableButtons_UpdateRuntimeThroughBindings 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Invoke、Home、IsType、Single、OfType；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void RealSampleAndVariableButtons_UpdateRuntimeThroughBindings()
    {
        host.Invoke(() =>
        {
            var page = Home();
            var vm = Assert.IsType<SecsWorkbenchViewModel>(page.DataContext);
            Invoke(page, "载入内存示例");
            var tabs = Descendants(page).OfType<TabControl>().Single();
            tabs.SelectedIndex = 1;
            page.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            var grid = (DataGrid)page.FindName("VariableGrid");
            grid.SelectedItem = vm.Variables.Single(v => v.Name == "LotId");
            ((TextBox)page.FindName("VariableValueBox")).SetCurrentValue(TextBox.TextProperty, "MVVM-LOT");
            Invoke(page, "写入选中变量");
            Assert.Equal("MVVM-LOT", vm.Variables.Single(v => v.Name == "LotId").Value);
            Assert.Contains(vm.Messages, m => m.Contains("LotId = MVVM-LOT"));
            tabs.SelectedIndex = 0;
        });
    }

    /// <summary>
    /// 验证 InvalidPort_IsReportedByAsyncCommand_WithoutConnecting 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Invoke、IsType、Home、ExecuteAsync、Contains；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task InvalidPort_IsReportedByAsyncCommand_WithoutConnecting()
    {
        var task = host.Invoke(() =>
        {
            var vm = Assert.IsType<SecsWorkbenchViewModel>(Home().DataContext);
            vm.PortText = "invalid";
            return vm.ConnectCommand.ExecuteAsync(null);
        });
        await task;
        host.Invoke(() =>
        {
            var vm = Assert.IsType<SecsWorkbenchViewModel>(Home().DataContext);
            Assert.Contains(vm.Messages, m => m.Contains("[错误]") && m.Contains("连接:"));
            Assert.Equal("未连接", vm.ConnectionStatus);
            vm.PortText = "5000";
        });
    }

    /// <summary>
    /// 验证 Home 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 IsType、Single、Resolve；具体条件和先后次序见方法体。
    /// </summary>
    private static SecsWorkbenchView Home() => Assert.IsType<SecsWorkbenchView>(
        Assert.Single(ContainerLocator.Container.Resolve<IRegionManager>().Regions["SecsRegion"].ActiveViews));

    /// <summary>
    /// 验证 Invoke 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Single、OfType、Descendants、Equals、NotNull；具体条件和先后次序见方法体。
    /// </summary>
    private static void Invoke(FrameworkElement root, string caption)
    {
        var button = Descendants(root).OfType<Button>().Single(b => Equals(b.Content, caption));
        Assert.NotNull(button.Command);
        ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)!).Invoke();
        root.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// 验证 Descendants 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 GetChildrenCount、GetChild、Descendants；具体条件和先后次序见方法体。
    /// </summary>
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
