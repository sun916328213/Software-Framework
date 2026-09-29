using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Media;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using System.Xml.Linq;
using FlowEditor.Behaviors;
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
            var host = Assert.IsType<Viewbox>(view.FindName("DataConnectionsToggleHost"));
            Assert.IsType<ToggleButton>(view.FindName("DataConnectionsToggle"));

            Assert.Equal(40, host.Width);
            Assert.Equal(22, host.Height);
            Assert.Equal(Stretch.Uniform, host.Stretch);
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

    /// <summary>验证输出栏提供绑定到当前标签页的一键清除按钮。</summary>
    [Fact]
    public void OutputPane_ProvidesClearCurrentOutputCommand()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var document = XDocument.Load(Path.Combine(root, "FlowEditor", "Views", "FlowOutputPaneView.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var button = Assert.Single(document.Descendants(presentation + "Button"),
            element => (string?)element.Attribute("Name") == "ClearOutputButton" ||
                       (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "ClearOutputButton");

        Assert.Equal("{Binding ActiveTab.ClearOutputCommand}", (string?)button.Attribute("Command"));
        Assert.Equal("清除", (string?)button.Attribute("Content"));
    }

    /// <summary>验证输出栏可以独立加载并拥有清除按钮和日志文本框。</summary>
    [Fact]
    public void OutputPane_LoadsAsIndependentView()
    {
        WpfTestHost.Run(() =>
        {
            var pane = new FlowOutputPaneView();

            Assert.NotNull(pane.Content);
            Assert.IsType<Button>(pane.FindName("ClearOutputButton"));
            Assert.IsType<TextBox>(pane.FindName("OutputTextBox"));
            Assert.IsType<TextBox>(pane.FindName("OutputSearchBox"));
            Assert.IsType<ComboBox>(pane.FindName("OutputLevelFilter"));
            Assert.IsType<CheckBox>(pane.FindName("AutoScrollToggle"));
        });
    }

    /// <summary>验证导出按钮通过明确事件通知拥有活动画布的宿主视图。</summary>
    [Fact]
    public void ExportPngButton_WhenInvoked_NotifiesHost()
    {
        WpfTestHost.Run(() =>
        {
            var view = new FlowCommandBarView();
            var context = new ExportCommandContext();
            view.DataContext = context;
            var received = false;
            view.ExportPngRequested += (_, _) => received = true;

            view.Measure(new Size(1600, 100));
            view.Arrange(new Rect(0, 0, 1600, 100));
            view.UpdateLayout();
            var button = Assert.IsType<Button>(view.FindName("ExportPngButton"));
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasActiveTab)));
            }
        }

        /// <summary>指示测试命令栏是否具有活动标签页。</summary>
        public bool HasActiveTab => ActiveTab != null;

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
            Assert.Equal(11, nodeMenu.Items.Count);
            Assert.Equal("从此节点继续执行", Assert.IsType<MenuItem>(nodeMenu.Items[0]).Header);
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
