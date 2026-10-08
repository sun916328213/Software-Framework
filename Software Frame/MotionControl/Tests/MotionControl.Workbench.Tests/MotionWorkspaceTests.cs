// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：MotionWorkspaceTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MotionControl.Controls.Services、MotionControl.Controls.ViewModels、System.Windows、System.Windows.Controls、System.Windows.Threading、System.Windows.Input。导入不代表所有类型都会在运行时加载。
using MotionControl.Controls.Services;
using MotionControl.Controls.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Media;
using MotionControl.Controls.Views;
using MotionControl.Core.Definitions;
using MotionControl.Runtime.Services;
using Prism.Ioc;

namespace MotionControl.Workbench.Tests;

/// <summary>
/// MotionWorkspaceTests 的类型定义。MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
[Collection("WorkbenchAppBoot")]
public sealed class MotionWorkspaceTests(WorkbenchAppFixture host)
{
    /// <summary>
    /// 测试相关入口 ControlsAndPage_ShareAxisModel_AndPreserveInputAcrossAxisSwitch；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Connect、IsAssignableFrom、Same、True；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ControlsAndPage_ShareAxisModel_AndPreserveInputAcrossAxisSwitch()
    {
        host.Invoke(() =>
        {
            using var workspace = new MotionWorkspace();
            workspace.Connect();
            var card = Assert.IsAssignableFrom<MotionControl.Core.Abstractions.IMotionCard>(workspace.Model.SelectedCard);
            var status = new AxisStatusControl { MotionCard = card };
            var jog = new AxisJogControl { MotionCard = card };
            var operation = new AxisOperationControl { MotionCard = card };
            var overview = new MotionOverviewControl { MotionCard = card };
            var model = workspace.Model.Axes[0];
            Assert.Same(model, status.ViewModel!.Model);
            Assert.Same(model, jog.ViewModel!.Model);
            Assert.Same(model, operation.ViewModel!.Model);
            Assert.Same(model, overview.Axes[0].Model);
            jog.ViewModel.SpeedInput = "24";
            Assert.True(operation.ViewModel.SetServo(true).IsSuccess);
            Assert.True(status.ViewModel.Enabled);
            Assert.True(overview.Axes[0].Enabled);
            jog.AxisIndex = 1;
            Assert.NotSame(model, jog.ViewModel.Model);
            jog.AxisIndex = 0;
            Assert.Equal("24", jog.ViewModel.SpeedInput);
            workspace.Disconnect();
            Assert.False(model.IsConnected);
            Assert.Null(model.CommandPosition);
            Assert.False(model.Enabled);
        });
    }

    /// <summary>
    /// 测试相关入口 RefreshSubscriptions_FollowLoadReloadAndCardChange；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Connect、For、Add、Show；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void RefreshSubscriptions_FollowLoadReloadAndCardChange()
    {
        host.Invoke(() =>
        {
            using var workspace = new MotionWorkspace();
            workspace.Connect();
            var card = workspace.Model.SelectedCard!;
            var axis = AxisWorkspace.For(card, 0);
            var status = new AxisStatusControl { MotionCard = card };
            var overview = new MotionOverviewControl { MotionCard = card };
            var panel = new StackPanel();
            panel.Children.Add(status);
            panel.Children.Add(overview);
            var window = new Window { Content = panel, Width = 900, Height = 700 };
            try
            {
                window.Show();
                Drain();
                Assert.Equal(2, axis.RefreshSubscriberCount);
                status.RefreshIntervalMs = 100;
                Assert.Equal(2, axis.RefreshSubscriberCount);
                status.MotionCard = null;
                Assert.Equal(1, axis.RefreshSubscriberCount);
                panel.Children.Remove(overview);
                Drain();
                Assert.Equal(0, axis.RefreshSubscriberCount);
                panel.Children.Add(overview);
                Drain();
                Assert.Equal(1, axis.RefreshSubscriberCount);
            }
            finally { window.Close(); Drain(); }
            Assert.Equal(0, axis.RefreshSubscriberCount);
        });
    }

    /// <summary>
    /// 测试相关入口 Workspace_UsesInjectedFactory_AndLeavesHostCardsConnected；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Add、Create、True、ConnectResult；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Workspace_UsesInjectedFactory_AndLeavesHostCardsConnected()
    {
        host.Invoke(() =>
        {
            using var manager = new MotionManager();
            var parameter = new MotionCardParam { Name = "Host", CardType = EMotionCardType.Simulation };
            parameter.Axises.Add(new AxisParam { Name = "HostAxis" });
            var hostCard = manager.Create(parameter);
            Assert.True(hostCard.ConnectResult().IsSuccess);
            using (var workspace = new MotionWorkspace(manager))
            {
                workspace.Connect();
                Assert.True(workspace.Model.IsConnected);
                Assert.Equal(2, manager.Count);
                workspace.Disconnect();
                Assert.Equal(1, manager.Count);
                Assert.True(hostCard.IsConnected);
            }
            manager.Register(EMotionCardType.Simulation, _ => throw new InvalidOperationException("injected-factory"));
            using var failing = new MotionWorkspace(manager);
            failing.Connect();
            Assert.Contains("injected-factory", failing.Model.StatusMessage);
            Assert.True(hostCard.IsConnected);
            Assert.Equal(1, manager.Count);
        });
    }

    /// <summary>
    /// 测试相关入口 Navigation_ResolvesControlsPageAndInjectedWorkspace_WithoutConnecting；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、IsType、nameof、SetAutoWireViewModel；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Navigation_ResolvesControlsPageAndInjectedWorkspace_WithoutConnecting()
    {
        host.Invoke(() =>
        {
            var container = ContainerLocator.Container;
            var workspace = container.Resolve<MotionWorkspace>();
            var page = Assert.IsType<MotionView>(container.Resolve<object>(nameof(MotionView)));
            Prism.Mvvm.ViewModelLocator.SetAutoWireViewModel(page, true);
            var vm = Assert.IsType<MotionViewModel>(page.DataContext);
            Assert.Same(workspace, vm.Workspace);
            Assert.Same(workspace.Model, vm.Model);
            var region = container.Resolve<Prism.Regions.IRegionManager>().Regions["MotionRegion"];
            var home = Assert.IsType<MotionView>(Assert.Single(region.ActiveViews));
            var homeVm = Assert.IsType<MotionViewModel>(home.DataContext);
            Assert.Same(workspace, homeVm.Workspace);
            Assert.False(vm.IsConnected);
        });
    }

    /// <summary>
    /// 测试相关入口 ActiveJog_StopsOriginalAxis_OnAxisSwitchOrLostCapture；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Connect、Show、Drain、True；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ActiveJog_StopsOriginalAxis_OnAxisSwitchOrLostCapture(bool switchAxis)
    {
        host.Invoke(() =>
        {
            using var workspace = new MotionWorkspace();
            workspace.Connect();
            var card = workspace.Model.SelectedCard!;
            var jog = new AxisJogControl { MotionCard = card };
            var window = new Window { Content = jog, Width = 700, Height = 500 };
            try
            {
                window.Show();
                Drain();
                Assert.True(jog.ViewModel!.SetServo(true).IsSuccess);
                var button = Descendants(jog).OfType<Button>().Single(b => Equals(b.Tag, "Positive"));
                window.Activate();
                button.Focus();
                Drain();
                var captureEvents = new List<string>();
                button.GotMouseCapture += (_, _) => captureEvents.Add("GotCapture");
                button.LostMouseCapture += (_, _) => captureEvents.Add("LostCapture");
                button.MouseLeave += (_, _) => captureEvents.Add("MouseLeave");
                button.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                {
                    RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent,
                });
                Assert.True(card.ReadAxisStatus(0).Value.Moving, $"Active={window.IsActive}, Captured={button.IsMouseCaptured}, Events={string.Join(",", captureEvents)}, Message={jog.ViewModel.LastMessage}, Enabled={card.ReadAxisStatus(0).Value.Enabled}");
                if (switchAxis) jog.AxisIndex = 1;
                else button.ReleaseMouseCapture();
                Assert.False(card.ReadAxisStatus(0).Value.Moving);
            }
            finally { window.Close(); Drain(); }
        });
    }

    /// <summary>捕获鼠标时立即失捕，点动必须保持停止且不抛出空引用。</summary>
    [Fact]
    public void JogCaptureLostDuringMouseDown_DoesNotStartMotionOrThrow()
    {
        host.Invoke(() =>
        {
            using var workspace = new MotionWorkspace();
            workspace.Connect();
            var card = workspace.Model.SelectedCard!;
            var jog = new AxisJogControl { MotionCard = card };
            var window = new Window { Content = jog, Width = 700, Height = 500 };
            try
            {
                window.Show();
                Drain();
                Assert.True(jog.ViewModel!.SetServo(true).IsSuccess);
                var button = Descendants(jog).OfType<Button>().Single(b => Equals(b.Tag, "Positive"));
                button.GotMouseCapture += (_, _) => button.ReleaseMouseCapture();
                button.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                {
                    RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent,
                });
                Assert.False(button.IsMouseCaptured);
                Assert.False(card.ReadAxisStatus(0).Value.Moving);
            }
            finally { window.Close(); Drain(); }
        });
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

    /// <summary>
    /// 测试相关入口 Drain；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke；具体条件和先后次序见方法体。
    /// </summary>
    private static void Drain() => Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
}
