// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：MvvmInteractionTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Prism.Ioc、MotionControl.Controls.Services、MotionControl.Controls.ViewModels、System.Windows、System.Windows.Controls、System.Windows.Media。导入不代表所有类型都会在运行时加载。
using Prism.Ioc;
using MotionControl.Controls.Services;
using MotionControl.Controls.ViewModels;
﻿using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using MotionControl.Controls.Views;
using MotionControl.Workbench.Views;
using MotionControl.Workbench.ViewModels;

namespace MotionControl.Workbench.Tests;

/// <summary>
/// MvvmInteractionTests 的类型定义。MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
[Collection("WorkbenchAppBoot")]
public sealed class MvvmInteractionTests(WorkbenchAppFixture host)
{
    /// <summary>
    /// 测试相关入口 RealButtons_ConnectServoAndDisconnect_ReachBoundAxis；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Show、IsType、Single、OfType；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void RealButtons_ConnectServoAndDisconnect_ReachBoundAxis()
    {
        host.Invoke(() =>
        {
            using var workspace = new MotionWorkspace();
            using var pageVm = new MotionViewModel(workspace);
            var page = new MotionView { DataContext = pageVm };
            var window = new Window { Content = page, Width = 1100, Height = 900 };
            try
            {
                window.Show();
                var vm = Assert.IsType<MotionViewModel>(page.DataContext);
                var servoButton = Descendants(page).OfType<Button>().Single(b => Equals(b.Content, "Servo On"));
                Assert.False(servoButton.IsEnabled);
                Invoke(page, "连接");
                Assert.True(vm.IsConnected);
                Assert.True(servoButton.IsEnabled);
                var status = (AxisStatusControl)page.FindName("StatusControl");
                var operation = (AxisOperationControl)page.FindName("OperationControl");
                Assert.Same(vm.SelectedCard, status.MotionCard);
                Assert.Same(vm.SelectedCard, operation.MotionCard);
                Assert.Same(vm, status.DataContext);
                Assert.Same(vm, operation.DataContext);
                Invoke(page, "Servo On");
                Assert.True(vm.SelectedCard!.ReadAxisStatus(0).Value.Enabled);
                Invoke(page, "Servo Off");
                Assert.False(vm.SelectedCard.ReadAxisStatus(0).Value.Enabled);
                Invoke(page, "断开");
                Assert.False(vm.IsConnected);
                Assert.Null(status.MotionCard);
                Assert.Null(operation.MotionCard);
                Assert.False(servoButton.IsEnabled);
            }
            finally { window.Close(); }
        });
    }

    /// <summary>
    /// 测试相关入口 SidebarSelection_NavigatesDirectControlsPageAndKeepsSharedWorkspace；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、IsType、Resolve、Single、Same；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void SidebarSelection_NavigatesDirectControlsPageAndKeepsSharedWorkspace()
    {
        host.Invoke(() =>
        {
            var window = Application.Current.MainWindow;
            var shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
            var regions = Prism.Ioc.ContainerLocator.Container.Resolve<Prism.Regions.IRegionManager>();
            var region = regions.Regions[MainWindowViewModel.MainRegion];
            var page = Assert.IsType<MotionView>(Assert.Single(region.ActiveViews));
            var vm = Assert.IsType<MotionViewModel>(page.DataContext);
            Assert.Same(Prism.Ioc.ContainerLocator.Container.Resolve<MotionWorkspace>(), vm.Workspace);
            Assert.Equal(nameof(MotionView), shell.SelectedNav!.ViewName);
            var list = Descendants(window).OfType<ListBox>().Single(control => ReferenceEquals(control.ItemsSource, shell.NavItems));
            Assert.Same(shell.SelectedNav, list.SelectedItem);
            list.SelectedIndex = -1;
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.Null(shell.SelectedNav);
            var item = Assert.Single(shell.NavItems);
            var peer = new ListBoxItemAutomationPeer(item, new ListBoxAutomationPeer(list));
            ((ISelectionItemProvider)peer.GetPattern(PatternInterface.SelectionItem)!).Select();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.Same(item, shell.SelectedNav);
            var active = Assert.IsType<MotionView>(Assert.Single(region.ActiveViews));
            Assert.Same(vm.Workspace, Assert.IsType<MotionViewModel>(active.DataContext).Workspace);
            shell.SelectFirst();
            Assert.Single(region.Views);
            Assert.False(vm.IsConnected);
        });
    }

    /// <summary>
    /// 测试相关入口 Invoke；准备条件、协作调用与结果检查以方法正文为准。
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
    /// 测试相关入口 Descendants；准备条件、协作调用与结果检查以方法正文为准。
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
