using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Media;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using System.Xml.Linq;
using FlowEditor.Behaviors;
using FlowEditor.Presentation.Commands;
using FlowEditor.Views;
using Xunit;

namespace FlowEditorTest;

/// <summary>验证流程表现层子视图、路由命令和附加行为。</summary>
public class FlowCommandBarViewTests
{
    /// <summary>验证命令栏在应用资源就绪时可以完成 XAML 初始化。</summary>
    [Fact]
    public void Constructor_WithApplicationResources_LoadsXaml()
    {
        WpfTestHost.Run(() =>
        {
            var view = new FlowCommandBarView();

            Assert.NotNull(view.Content);
        });
    }

    [Fact]
    public void ShortcutHelpButton_BindsToHelpCommand()
    {
        WpfTestHost.Run(() =>
        {
            var view = new FlowCommandBarView();
            var button = Assert.IsType<Button>(view.FindName("ShortcutHelpButton"));
            var binding = BindingOperations.GetBinding(button, Button.CommandProperty);

            Assert.Equal("ShowShortcutHelpCommand", binding?.Path.Path);
            Assert.Contains("快捷键", button.ToolTip?.ToString());
        });
    }

    [Fact]
    public void DataConnectionsToggle_UsesCompactToolbarDimensions()
    {
        WpfTestHost.Run(() =>
        {
            var view = new FlowCommandBarView();
            var toggle = Assert.IsType<ToggleButton>(view.FindName("DataConnectionsToggle"));

            Assert.Equal(32, toggle.Width);
            Assert.Equal(28, toggle.Height);
            Assert.Equal(new Thickness(1, 0, 1, 0), toggle.Margin);
        });
    }

    [Fact]
    public void CtrlSaveAndOpen_TargetCurrentSingleFlowCommands()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var document = XDocument.Load(Path.Combine(root, "FlowEditor", "Views", "FlowChartContainerView.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var bindings = document.Descendants(presentation + "KeyBinding").ToList();

        var save = Assert.Single(bindings, binding => (string?)binding.Attribute("Key") == "S");
        var open = Assert.Single(bindings, binding => (string?)binding.Attribute("Key") == "O");

        Assert.Equal("{Binding ActiveTab.SaveCommand}", (string?)save.Attribute("Command"));
        Assert.Equal("{Binding ActiveTab.LoadCommand}", (string?)open.Attribute("Command"));
    }

    /// <summary>验证点击真实导出按钮时，PNG 命令可以沿视觉树路由到宿主。</summary>
    [Fact]
    public void ExportPngCommand_WhenExecuted_RoutesToHost()
    {
        WpfTestHost.Run(() =>
        {
            var view = new FlowCommandBarView();
            var context = new ExportCommandContext();
            view.DataContext = context;
            var host = new Grid { Children = { view } };
            var received = false;
            host.CommandBindings.Add(new CommandBinding(
                FlowChartRoutedCommands.ExportPng,
                (_, _) => received = true,
                (_, e) => e.CanExecute = e.Parameter != null));

            host.Measure(new Size(1600, 100));
            host.Arrange(new Rect(0, 0, 1600, 100));
            host.UpdateLayout();
            var button = FindVisualDescendants<Button>(view)
                .Single(candidate => ReferenceEquals(candidate.Command, FlowChartRoutedCommands.ExportPng));
            Assert.False(button.IsEnabled);

            context.ActiveTab = new object();
            button.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
            Assert.True(button.IsEnabled);

            var peer = new ButtonAutomationPeer(button);
            var invokeProvider = Assert.IsAssignableFrom<IInvokeProvider>(
                peer.GetPattern(PatternInterface.Invoke));

            invokeProvider.Invoke();
            button.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

            Assert.True(received);
        });
    }

    /// <summary>递归枚举指定视觉树中的目标类型元素。</summary>
    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
                yield return match;

            foreach (var descendant in FindVisualDescendants<T>(child))
                yield return descendant;
        }
    }

    /// <summary>模拟活动标签页延迟就绪的命令栏数据上下文。</summary>
    private sealed class ExportCommandContext : INotifyPropertyChanged
    {
        private object? _activeTab;

        /// <summary>在活动标签页变化时通知命令参数绑定。</summary>
        public object? ActiveTab
        {
            get => _activeTab;
            set
            {
                _activeTab = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveTab)));
            }
        }

        /// <summary>在测试数据属性变化时发生。</summary>
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>验证工具箱可以在共享应用资源下独立加载。</summary>
    [Fact]
    public void ToolboxConstructor_WithApplicationResources_LoadsXaml()
    {
        WpfTestHost.Run(() =>
        {
            var view = new FlowToolboxView();

            Assert.NotNull(view.Content);
        });
    }

    /// <summary>验证输出文本框的自动滚动行为能够独立启用和关闭。</summary>
    [Fact]
    public void TextBoxAutoScrollBehavior_WhenToggled_TracksAttachedState()
    {
        WpfTestHost.Run(() =>
        {
            var textBox = new TextBox();

            TextBoxAutoScrollBehavior.SetIsEnabled(textBox, true);
            textBox.Text = "最新日志";
            Assert.True(TextBoxAutoScrollBehavior.GetIsEnabled(textBox));

            TextBoxAutoScrollBehavior.SetIsEnabled(textBox, false);
            Assert.False(TextBoxAutoScrollBehavior.GetIsEnabled(textBox));
        });
    }

    /// <summary>验证画布与节点菜单由 XAML 资源完整加载，而不是在代码中逐项拼装。</summary>
    [Fact]
    public void FlowChartView_ContextMenus_LoadFromXamlResources()
    {
        WpfTestHost.Run(() =>
        {
            var view = new FlowChartView();

            var canvasMenu = Assert.IsType<ContextMenu>(view.FindResource("CanvasContextMenu"));
            var nodeMenu = Assert.IsType<ContextMenu>(view.FindResource("NodeContextMenu"));
            Assert.Equal(4, canvasMenu.Items.Count);
            Assert.Equal(9, nodeMenu.Items.Count);
        });
    }

    /// <summary>验证从 TabControl 内容树而非 TabItem 标签头树定位活动流程画布。</summary>
    [Fact]
    public void ActiveFlowChartLookup_FromTabControl_FindsSelectedContent()
    {
        WpfTestHost.Run(() =>
        {
            var activeTab = new object();
            var flowChart = new FlowChartView { DataContext = activeTab };
            var tabItem = new TabItem { Header = "流程", Content = flowChart };
            var tabControl = new TabControl { Items = { tabItem }, SelectedIndex = 0 };
            var host = new ContentControl { Content = tabControl };
            host.Measure(new Size(800, 600));
            host.Arrange(new Rect(0, 0, 800, 600));
            host.UpdateLayout();

            var incorrectlyScoped = FindVisualDescendants<FlowChartView>(tabItem).SingleOrDefault();
            var found = FlowChartContainerView.FindActiveFlowChart(tabControl, activeTab);

            Assert.Null(incorrectlyScoped);
            Assert.Same(flowChart, found);
        });
    }
}
