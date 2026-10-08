// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ControlsWorkspaceTests、ObservedRuntime。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Windows.Controls、SecsGem.Runtime.Variable、Extension.UI.Theme、Prism.Ioc、SecsGem.Controls.Services、SecsGem.Controls.Terminal。导入不代表所有类型都会在运行时加载。
using System.Windows.Controls;
using SecsGem.Runtime.Variable;
using Extension.UI.Theme;
using Prism.Ioc;
using SecsGem.Controls.Services;
using SecsGem.Controls.Terminal;
using SecsGem.Controls.ViewModels;
using SecsGem.Controls.Views;
using SecsGem.Runtime;
using SecsGem.Runtime.Manager;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace SecsGem.Workbench.Tests;

/// <summary>
/// ControlsWorkspaceTests 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
[Collection("WorkbenchAppBoot")]
public sealed class ControlsWorkspaceTests(WorkbenchAppFixture host)
{
    /// <summary>
    /// 测试相关入口 MultiplePages_ShareState_DisposingOneLeavesWorkspaceUsable；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、nameof、NotNull、NotSame；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void MultiplePages_ShareState_DisposingOneLeavesWorkspaceUsable()
    {
        host.Invoke(() =>
        {
            var container = ContainerLocator.Container;
            var workspace = container.Resolve<SecsWorkspace>();
            var first = container.Resolve<object>(nameof(SecsView)) as SecsView;
            var second = container.Resolve<object>(nameof(SecsView)) as SecsView;
            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.NotSame(first, second);
            var vm = Assert.IsType<SecsViewModel>(first.DataContext);
            var other = Assert.IsType<SecsViewModel>(second.DataContext);
            Assert.Same(workspace.Model, vm.Model);
            Assert.Same(vm.Model, other.Model);
            var original = vm.Sml;
            try
            {
                vm.Sml = "'S1F3' W";
                Assert.Equal(vm.Sml, other.Sml);
                first.Dispose();
                using var lease = other.Subscribe();
                other.RefreshState();
                Assert.Equal(workspace.Model.ConnectionStatus, other.ConnectionStatus);
            }
            finally { workspace.Model.Sml = original; first.Dispose(); second.Dispose(); }
        });
    }

    /// <summary>
    /// 测试相关入口 WorkspaceDispose_DoesNotDisposeInjectedRuntime；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、Subscribe、Dispose、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void WorkspaceDispose_DoesNotDisposeInjectedRuntime()
    {
        host.Invoke(() =>
        {
            var container = ContainerLocator.Container;
            using var runtime = new ObservedRuntime(container);
            var workspace = new SecsWorkspace(runtime, container.Resolve<VIDManager>(), Dispatcher.CurrentDispatcher);
            using var lease = workspace.Subscribe();
            workspace.Dispose();
            workspace.Dispose();
            Assert.Equal(0, runtime.DisposeCount);
            Assert.Throws<ObjectDisposedException>(() => workspace.Subscribe());
        });
    }

    /// <summary>
    /// 测试相关入口 CommandsAndRealButtons_FollowConnectionParametersAndSelection；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、Create、Measure、Arrange；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void CommandsAndRealButtons_FollowConnectionParametersAndSelection()
    {
        host.Invoke(() =>
        {
            var container = ContainerLocator.Container;
            using var runtime = new ObservedRuntime(container);
            using var workspace = new SecsWorkspace(runtime, container.Resolve<VIDManager>(), Dispatcher.CurrentDispatcher);
            using var vm = new SecsViewModel(workspace);
            using var page = SecsView.Create(vm);
            page.Measure(new Size(1400, 900));
            page.Arrange(new Rect(0, 0, 1400, 900));
            page.UpdateLayout();
            Button ButtonFor(string caption) => Descendants(page).OfType<Button>().Single(b => Equals(b.Content, caption));
            void Flush() => page.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.True(ButtonFor("连接").IsEnabled);
            Assert.False(ButtonFor("断开").IsEnabled);
            Assert.False(ButtonFor("建立通信 S1F13").IsEnabled);
            Assert.False(ButtonFor("发送").IsEnabled);
            vm.PortText = "0";
            Flush();
            Assert.False(ButtonFor("连接").IsEnabled);
            vm.PortText = "5000";
            vm.DeviceIdText = "32768";
            Flush();
            Assert.False(ButtonFor("连接").IsEnabled);
            vm.DeviceIdText = "0";
            workspace.Model.IsConnected = true;
            Flush();
            Assert.False(ButtonFor("连接").IsEnabled);
            Assert.True(ButtonFor("断开").IsEnabled);
            Assert.True(ButtonFor("建立通信 S1F13").IsEnabled);
            Assert.True(ButtonFor("发送").IsEnabled);
            vm.Sml = " ";
            Flush();
            Assert.False(ButtonFor("发送").IsEnabled);
            Assert.False(vm.ReportEventCommand.CanExecute(null));
            vm.SelectedEvent = new CEIDInfo { ID = "1", Name = "Test" };
            vm.SelectedAlarm = new AlarmInfo { ID = "1", Description = "Test" };
            Assert.True(vm.ReportEventCommand.CanExecute(null));
            Assert.True(vm.RaiseAlarmCommand.CanExecute(null));
            Assert.True(vm.ResetAlarmCommand.CanExecute(null));
            workspace.Model.IsConnected = false;
            Flush();
            Assert.False(ButtonFor("断开").IsEnabled);
            Assert.False(vm.ReportEventCommand.CanExecute(null));
            Assert.False(vm.RaiseAlarmCommand.CanExecute(null));
            Assert.False(vm.SetVariableCommand.CanExecute(null));
            vm.SelectedVariable = new SecsGem.Controls.Models.VariableRow("SVID", new VIDInfo { ID = "1", Name = "Test" });
            Assert.True(vm.SetVariableCommand.CanExecute(null)); // 本地变量编辑不要求连接。
            Assert.True(vm.LoadSampleCommand.CanExecute(null));
        });
    }

    /// <summary>
    /// 测试相关入口 DisposedBorrower_DisablesOnlyOwnCommands_ClosedWorkspaceDisablesAll；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、Add、Dispose、Throws；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void DisposedBorrower_DisablesOnlyOwnCommands_ClosedWorkspaceDisablesAll()
    {
        host.Invoke(() =>
        {
            var container = ContainerLocator.Container;
            using var runtime = new ObservedRuntime(container);
            using var workspace = new SecsWorkspace(runtime, container.Resolve<VIDManager>(), Dispatcher.CurrentDispatcher);
            using var first = new SecsViewModel(workspace);
            using var second = new SecsViewModel(workspace);
            workspace.Messages.Add("Keep");
            first.Dispose();
            Assert.Throws<ObjectDisposedException>(() => first.Subscribe());
            Assert.False(first.ConnectCommand.CanExecute(null));
            Assert.False(first.ClearLogCommand.CanExecute(null));
            first.ClearLogCommand.Execute(null);
            Assert.Contains("Keep", workspace.Messages);
            Assert.True(second.ClearLogCommand.CanExecute(null));
            Assert.True(second.ConnectCommand.CanExecute(null));
            workspace.Dispose();
            Assert.False(second.ConnectCommand.CanExecute(null));
            Assert.False(second.ClearLogCommand.CanExecute(null));
            workspace.ClearLog();
            Assert.Contains("Keep", workspace.Messages);
            Assert.Equal(0, runtime.DisposeCount);
        });
    }

    /// <summary>
    /// ObservedRuntime 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：SecsGemRuntime。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class ObservedRuntime(IContainerProvider container) : SecsGemRuntime(container)
    {
        public int DisposeCount { get; private set; }
        /// <summary>
        /// 测试相关入口 Dispose；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Dispose；具体条件和先后次序见方法体。
        /// </summary>
        protected override void Dispose(bool disposing) { DisposeCount++; base.Dispose(disposing); }
    }

    /// <summary>
    /// 测试相关入口 TerminalPrompt_FollowsLightAndDarkForeground；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、Show、Initialize、IsAssignableFrom；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void TerminalPrompt_FollowsLightAndDarkForeground()
    {
        host.Invoke(() =>
        {
            var theme = ContainerLocator.Container.Resolve<ThemeService>();
            var original = (theme.IsDark, theme.Primary, theme.Secondary);
            var prompt = new MessagePrompt("Theme title", "Theme message");
            try
            {
                prompt.Show();
                foreach (var dark in new[] { false, true })
                {
                    theme.Initialize(dark, original.Primary, original.Secondary);
                    prompt.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    var expected = Assert.IsAssignableFrom<SolidColorBrush>(prompt.FindResource("MaterialDesign.Brush.Foreground")).Color;
                    var texts = Descendants(prompt).OfType<TextBlock>().Where(t => t.Text is "Theme title" or "Theme message").ToArray();
                    Assert.Equal(2, texts.Length);
                    foreach (var text in texts)
                        Assert.Equal(expected, Assert.IsAssignableFrom<SolidColorBrush>(text.Foreground).Color);
                }
            }
            finally { prompt.Close(); theme.Initialize(original.IsDark, original.Primary, original.Secondary); }
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
            foreach (var item in Descendants(child)) yield return item;
        }
    }
}
